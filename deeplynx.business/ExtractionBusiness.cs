using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;

namespace deeplynx.business;

public class ExtractionBusiness : IExtractionBusiness
{
    private readonly DeeplynxContext _context;
    private readonly StagingContext _stagingContext;

    public ExtractionBusiness(DeeplynxContext context, StagingContext stagingContext)
    {
        _context = context;
        _stagingContext = stagingContext;
    }

    /// <summary>
    ///     Creates extraction job and inserts staged records, classes, edges, and relationships
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">
    ///     The ID of the organization to which the staged classes, records, edges and relationships
    ///     belong
    /// </param>
    /// <param name="projectId">The ID of the project to which the staged classes, records, edges and relationships belong</param>
    /// <param name="dataSourceId">The ID of the datasource to which the staged records and edges belong</param>
    /// <param name="dto">
    ///     CreateExtractionRequestDTO that contains the CreateDTOs for Classes, Records, Edges, and
    ///     Relationships as well as extraction configurations
    /// </param>
    /// <returns>ExtractionResponseDto which contains counts of staged entities</returns>
    /// <exception cref="Exception">Returned if error occurs during extraction transaction</exception>
    public async Task<ExtractionResponseDto> CreateExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long dataSourceId,
        CreateExtractionRequestDto dto)
    {
        var extraction = new Extraction
        {
            Properties = dto.Properties?.ToJsonString(),
            CreatedBy = currentUserId
        };
        _context.Extractions.Add(extraction);
        await _context.SaveChangesAsync();

        await using var stagingTransaction = await _stagingContext.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            // Maps used to resolve cross-references within this payload
            var classNameToStagingId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var relationshipNameToStagingId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var originalIdToStagingRecordId = new Dictionary<string, long>();
            
            foreach (var classDto in dto.Classes ?? [])
            {
                var stagingClass = new Class
                {
                    Name = classDto.Name,
                    Description = classDto.Description,
                    Properties = classDto.Properties?.ToJsonString(),
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    LastUpdatedAt = now,
                    LastUpdatedBy = currentUserId,
                    ExtractionId = extraction.Id
                };
                _stagingContext.Classes.Add(stagingClass);
                await _stagingContext.SaveChangesAsync();
                classNameToStagingId[stagingClass.Name] = stagingClass.Id;
            }
            
            // OriginId/DestinationId are class IDs — resolve from this payload's staging classes only.
            // If the class only exists in deeplynx, leave the ID null and store the name as a shadow
            // property so promotion can resolve it by name.
            foreach (var relDto in dto.Relationships ?? [])
            {
                var originId = relDto.OriginId ?? ResolveClassId(relDto.OriginName, classNameToStagingId);
                var destinationId = relDto.DestinationId ?? ResolveClassId(relDto.DestinationName, classNameToStagingId);

                var stagingRelationship = new Relationship
                {
                    Name = relDto.Name,
                    Description = relDto.Description,
                    Properties = relDto.Properties?.ToJsonString(),
                    OriginId = originId,
                    DestinationId = destinationId,
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    LastUpdatedAt = now,
                    LastUpdatedBy = currentUserId,
                    ExtractionId = extraction.Id
                };
                _stagingContext.Relationships.Add(stagingRelationship);

                // Store unresolved class names as shadow properties for promotion
                if (originId == null && relDto.OriginName != null)
                    _stagingContext.Entry(stagingRelationship).Property("OriginName").CurrentValue = relDto.OriginName;
                if (destinationId == null && relDto.DestinationName != null)
                    _stagingContext.Entry(stagingRelationship).Property("DestinationName").CurrentValue = relDto.DestinationName;

                await _stagingContext.SaveChangesAsync();
                relationshipNameToStagingId[stagingRelationship.Name] = stagingRelationship.Id;
            }

            // ClassId is resolved from this payload's staging classes only.
            // If the class only exists in deeplynx, ClassId stays null and the class name is stored
            // as a shadow property so promotion can resolve it by name.
            foreach (var recordDto in dto.Records ?? [])
            {
                var classId = recordDto.ClassId;
                if (classId == null && recordDto.ClassName != null
                    && classNameToStagingId.TryGetValue(recordDto.ClassName, out var stagingClassId))
                    classId = stagingClassId;

                var stagingRecord = new Record
                {
                    Name = recordDto.Name,
                    OriginalId = recordDto.OriginalId,
                    Properties = recordDto.Properties.ToJsonString(),
                    Description = recordDto.Description ?? string.Empty,
                    Uri = recordDto.Uri,
                    FileType = recordDto.FileType,
                    ObjectStorageId = recordDto.ObjectStorageId,
                    ClassId = classId,
                    DataSourceId = dataSourceId,
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    LastUpdatedAt = now,
                    LastUpdatedBy = currentUserId,
                    ExtractionId = extraction.Id
                };
                _stagingContext.Records.Add(stagingRecord);

                // Store unresolved class name as shadow property for promotion
                if (classId == null && recordDto.ClassName != null)
                    _stagingContext.Entry(stagingRecord).Property("ClassName").CurrentValue = recordDto.ClassName;

                await _stagingContext.SaveChangesAsync();
                originalIdToStagingRecordId[stagingRecord.OriginalId] = stagingRecord.Id;
            }

            // Edges where both endpoints resolve to staging records go into staging.edges (intra-schema FKs enforced).
            // Edges where either endpoint is a deeplynx-only record go into staging.cross_schema_edges,
            // which stores original_id strings resolved at promotion time.
            foreach (var edgeDto in dto.Edges ?? [])
            {
                var originId = ResolveRecordId(edgeDto.OriginOriginalId, originalIdToStagingRecordId)
                               ?? edgeDto.OriginId;
                var destinationId = ResolveRecordId(edgeDto.DestinationOriginalId, originalIdToStagingRecordId)
                                    ?? edgeDto.DestinationId;
                var relationshipId = ResolveRelationshipId(edgeDto.RelationshipName, relationshipNameToStagingId)
                                     ?? edgeDto.RelationshipId;

                var hasCrossSchemaRef = edgeDto.DeeplynxOriginOriginalId != null
                                        || edgeDto.DeeplynxDestinationOriginalId != null
                                        || edgeDto.DeeplynxRelationshipName != null;

                if (originId != null && destinationId != null && !hasCrossSchemaRef)
                {
                    // Both endpoints resolved to staging records — standard staging edge
                    _stagingContext.Edges.Add(new Edge
                    {
                        OriginId = originId.Value,
                        DestinationId = destinationId.Value,
                        RelationshipId = relationshipId,
                        DataSourceId = dataSourceId,
                        OrganizationId = organizationId,
                        ProjectId = projectId,
                        Properties = edgeDto.Properties?.ToJsonString(),
                        LastUpdatedAt = now,
                        LastUpdatedBy = currentUserId,
                        ExtractionId = extraction.Id
                    });
                }
                else if (hasCrossSchemaRef || originId != null || destinationId != null)
                {
                    // At least one endpoint or relationship references a deeplynx entity — cross-schema edge
                    _stagingContext.CrossSchemaEdges.Add(new CrossSchemaEdge
                    {
                        ExtractionId = extraction.Id,
                        DataSourceId = dataSourceId,
                        OrganizationId = organizationId,
                        ProjectId = projectId,
                        Properties = edgeDto.Properties?.ToJsonString(),
                        LastUpdatedAt = now,
                        LastUpdatedBy = currentUserId,
                        OriginOriginalId = edgeDto.OriginOriginalId,
                        DeeplynxOriginOriginalId = edgeDto.DeeplynxOriginOriginalId,
                        DestinationOriginalId = edgeDto.DestinationOriginalId,
                        DeeplynxDestinationOriginalId = edgeDto.DeeplynxDestinationOriginalId,
                        RelationshipName = edgeDto.RelationshipName,
                        DeeplynxRelationshipName = edgeDto.DeeplynxRelationshipName
                    });
                }
                // else: no resolvable origin or destination at all — silently skip
            }

            await _stagingContext.SaveChangesAsync();

            await stagingTransaction.CommitAsync();

            return new ExtractionResponseDto
            {
                Id = extraction.Id,
                Properties = extraction.Properties,
                CreatedBy = currentUserId,
                ClassCount = dto.Classes?.Count ?? 0,
                RelationshipCount = dto.Relationships?.Count ?? 0,
                RecordCount = dto.Records?.Count ?? 0,
                EdgeCount = dto.Edges?.Count ?? 0
            };
        }
        catch
        {
            await stagingTransaction.RollbackAsync();
            _context.Extractions.Remove(extraction);
            await _context.SaveChangesAsync();
            throw;
        }
    }

    private static long? ResolveClassId(string? name, Dictionary<string, long> map)
    {
        return name != null && map.TryGetValue(name, out var id) ? id : null;
    }

    private static long? ResolveRecordId(string? originalId, Dictionary<string, long> map)
    {
        return originalId != null && map.TryGetValue(originalId, out var id) ? id : null;
    }

    private static long? ResolveRelationshipId(string? name, Dictionary<string, long> map)
    {
        return name != null && map.TryGetValue(name, out var id) ? id : null;
    }
}
