using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;

namespace deeplynx.business;

public class MetadataBusiness : IMetadataBusiness
{
    private readonly IClassBusiness _classBusiness;
    private readonly DeeplynxContext _context;
    private readonly IEdgeBusiness _edgeBusiness;
    private readonly IRecordBusiness _recordBusiness;
    private readonly IRelationshipBusiness _relationshipBusiness;
    private readonly ITagBusiness _tagBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MetadataBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context to be used for CRUD operations.</param>
    /// <param name="classBusiness">The class context to be used during metadata parsing.</param>
    /// <param name="relationshipBusiness">The relationship context to be used during metadata parsing.</param>
    /// <param name="tagBusiness">The tag context to be used during metadata parsing.</param>
    /// <param name="recordBusiness">The record context to be used during metadata parsing.</param>
    /// <param name="edgeBusiness">The edge context to be used during metadata parsing.</param>
    public MetadataBusiness(
        DeeplynxContext context,
        IClassBusiness classBusiness,
        IRelationshipBusiness relationshipBusiness,
        ITagBusiness tagBusiness,
        IRecordBusiness recordBusiness,
        IEdgeBusiness edgeBusiness
    )
    {
        _context = context;
        _classBusiness = classBusiness;
        _relationshipBusiness = relationshipBusiness;
        _tagBusiness = tagBusiness;
        _recordBusiness = recordBusiness;
        _edgeBusiness = edgeBusiness;
    }

    /// <summary>
    ///     Call the parse and perform pre-processing and final returned data validation of all metadata.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId">The ID of the project to which the metadata belongs.</param>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="dataSourceId">The ID of the project data source to which some metadata belongs.</param>
    /// <param name="metadataRequestDto">The metadata request data transfer object containing metadata.</param>
    /// <returns>The created metadata response DTO with saved details.</returns>
    /// <exception cref="KeyNotFoundException">If project is not found.</exception>
    /// <exception cref="KeyNotFoundException">If data source is not found.</exception>
    public async Task<MetadataResponseDto> CreateMetadata(
        long currentUserId,
        long projectId,
        long organizationId,
        long dataSourceId,
        CreateMetadataRequestDto metadataRequestDto)
    {
        if (metadataRequestDto == null)
            throw new ArgumentNullException(nameof(metadataRequestDto));

        return await ParseMetadata(currentUserId, metadataRequestDto, dataSourceId, organizationId, projectId);
    }

    /// <summary>
    ///     Check file format and file properties and deserialize into our CreateMetadataRequestDto.
    ///     Call the parse and perform pre-processing and final returned data validation of all metadata.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId">The ID of the project to which the metadata belongs.</param>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="dataSourceId">The ID of the project data source to which some metadata belongs.</param>
    /// <param name="file">The .json file containing the metadata contents.</param>
    /// <returns>The created metadata response DTO with saved details.</returns>
    /// <exception cref="KeyNotFoundException">If project is not found.</exception>
    /// <exception cref="KeyNotFoundException">If data source is not found.</exception>
    /// <exception cref="ArgumentNullException">If file is null.</exception>
    /// <exception cref="ArgumentException">If file is empty or not a .json file.</exception>
    /// <exception cref="JsonException">If file cannot be deserialized or contains invalid JSON.</exception>
    public async Task<MetadataResponseDto> CreateMetadataFromFile(
        long currentUserId,
        long projectId,
        long organizationId,
        long dataSourceId,
        IFormFile file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file), "File cannot be null.");

        if (file.Length == 0)
            throw new ArgumentException("File cannot be empty.", nameof(file));

        // Only support for .json as of now
        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (extension != ".json")
            throw new ArgumentException("File must be a .json file.", nameof(file));

        // Convert file to our DTO
        CreateMetadataRequestDto metadataRequestDto;
        try
        {
            using (var stream = file.OpenReadStream())
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                metadataRequestDto = await JsonSerializer.DeserializeAsync<CreateMetadataRequestDto>(stream, options);

                if (metadataRequestDto == null) throw new JsonException("Failed to deserialize metadata from file.");
            }
        }
        catch (Exception ex)
        {
            throw new JsonException($"Error reading JSON from file: {ex.Message}", ex);
        }

        return await ParseMetadata(currentUserId, metadataRequestDto, dataSourceId, organizationId, projectId);
    }

    /// <summary>
    ///     Individually call the bulk create functions of all metadata fields and append to return object.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId">The ID of the project to which the metadata belongs.</param>
    /// <param name="dataSourceId">The ID of the project data source to which the metadata belongs.</param>
    /// <param name="metadataRequestDto">The metadata request data transfer object containing metadata.</param>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <returns>The created metadata response DTO with saved details.</returns>
    private async Task<MetadataResponseDto> ParseMetadata(
        long currentUserId,
        CreateMetadataRequestDto metadataRequestDto,
        long dataSourceId,
        long organizationId,
        long projectId
    )
    {
        var metadataResponseDto = new MetadataResponseDto();

        // Create the respective entity lists
        var classes = metadataRequestDto.Classes ?? new List<CreateClassRequestDto>();
        var relationships = metadataRequestDto.Relationships ?? new List<CreateRelationshipRequestDto>();
        var tags = metadataRequestDto.Tags ?? new List<CreateTagRequestDto>();
        var records = metadataRequestDto.Records ?? new List<CreateRecordRequestDto>();
        var edges = metadataRequestDto.Edges ?? new List<CreateEdgeRequestDto>();

        // Classes
        Dictionary<string, long> classMap = new();
        if (classes.Any() || records.Any())
        {
            // check dependent objects for additional classes and then insert
            var classesToInsert = BuildClasses(classes, records);
            if (classesToInsert.Any())
            {
                classMap = await BulkUpsertClasses(
                    currentUserId, organizationId, projectId, classesToInsert, metadataResponseDto);
                // load class IDs into records objects before insert
                UpdateRecordsWithIds(records, classMap);
            }
        }

        // Relationships
        Dictionary<string, long> relMap = new();
        if (relationships.Any() || edges.Any())
        {
            // check dependent objects for additional relationships and then insert
            var relationshipsToInsert = BuildRelationships(relationships, edges);
            if (relationshipsToInsert.Any())
                relMap = await BulkUpsertRelationships(organizationId, currentUserId, projectId, relationshipsToInsert,
                    metadataResponseDto);
        }

        // Tags
        Dictionary<string, TagResponseDto> tagMap = new();
        if (tags.Any() || records.Any())
        {
            // check dependent objects for additional tags and then insert
            var tagsToInsert = BuildTags(tags, records);
            if (tagsToInsert.Any())
                tagMap = await BulkUpsertTags(organizationId, currentUserId, projectId, tagsToInsert,
                    metadataResponseDto);
        }

        // Records
        Dictionary<string, long> recordMap = new();
        if (records.Any())
        {
            Console.WriteLine("creating record map");
            recordMap = await BulkUpsertRecords(currentUserId, projectId, organizationId, dataSourceId, records,
                metadataResponseDto);

            // Record Tags
            var recordTags = BuildRecordTags(records, tagMap, recordMap);
            if (recordTags.Any())
            {
                await _recordBusiness.BulkAttachTags(recordTags);
                AttachTagsToRecordDtos(metadataResponseDto, recordTags, tagMap);
            }
        }

        // Edges
        if (edges.Any())
        {
            // ensure all origin/destination records exist in the record map; if not, check DB
            CheckRecordsByOriginalId(recordMap, edges);
            // load relationship, origin and destination IDs into classes before insert
            UpdateEdgesWithIds(edges, relMap, recordMap);
            metadataResponseDto.Edges =
                await _edgeBusiness.BulkCreateEdges(currentUserId, organizationId, projectId, dataSourceId, edges);
        }

        return metadataResponseDto;
    }

    /// <summary>
    ///     Add any classes found in the records objects to a list of classes waiting to be inserted
    /// </summary>
    /// <param name="classes"></param>
    /// <param name="records"></param>
    /// <returns>A list of classes to be inserted</returns>
    private List<CreateClassRequestDto> BuildClasses(
        List<CreateClassRequestDto> classes,
        List<CreateRecordRequestDto> records)
    {
        var dict = classes.ToDictionary(c => c.Name);
        foreach (var record in records)
            if (!string.IsNullOrEmpty(record.ClassName))
                dict.TryAdd(record.ClassName, new CreateClassRequestDto { Name = record.ClassName });
        return dict.Values.ToList();
    }

    /// <summary>
    ///     Add any relationships found in the edges objects to a list of relationships waiting to be inserted
    /// </summary>
    /// <param name="relationships"></param>
    /// <param name="edges"></param>
    /// <returns>A list of relationships to be inserted</returns>
    private List<CreateRelationshipRequestDto> BuildRelationships(
        List<CreateRelationshipRequestDto> relationships,
        List<CreateEdgeRequestDto> edges)
    {
        var dict = relationships.ToDictionary(r => r.Name);
        foreach (var edge in edges)
            if (!string.IsNullOrEmpty(edge.RelationshipName))
                dict.TryAdd(edge.RelationshipName, new CreateRelationshipRequestDto { Name = edge.RelationshipName });
        return dict.Values.ToList();
    }

    /// <summary>
    ///     Add any tags found in the records objects to a list of tags waiting to be inserted
    /// </summary>
    /// <param name="tags"></param>
    /// <param name="records"></param>
    /// <returns>A list of tags to be inserted</returns>
    private List<CreateTagRequestDto> BuildTags(
        List<CreateTagRequestDto> tags,
        List<CreateRecordRequestDto> records)
    {
        var dict = tags.ToDictionary(r => r.Name);
        foreach (var record in records)
        {
            if (record.Tags == null) continue;
            foreach (var tag in record.Tags)
                dict.TryAdd(tag, new CreateTagRequestDto { Name = tag });
        }

        return dict.Values.ToList();
    }

    /// <summary>
    ///     Throw error if records are specified by an edge but not specified by a record
    ///     TODO: eventually fetch records from DB by original ID (DL-533)
    /// </summary>
    /// <param name="recordMap"></param>
    /// <param name="edges"></param>
    /// <returns>A list of relationships to be inserted</returns>
    private void CheckRecordsByOriginalId(
        Dictionary<string, long> recordMap,
        List<CreateEdgeRequestDto> edges)
    {
        // Check if recordMap is null
        if (recordMap == null) throw new ArgumentNullException(nameof(recordMap), "Record map cannot be null");

        // Print the contents of recordMap for debugging
        Console.WriteLine("Contents of recordMap:");
        foreach (var kvp in recordMap) Console.WriteLine($"Key: {kvp.Key}, Value: {kvp.Value}");

        var missingOriginalIds = new HashSet<string>();

        // Check if edges are null or empty
        if (edges == null || !edges.Any()) throw new ArgumentException("Edges cannot be null or empty", nameof(edges));

        foreach (var edge in edges)
        {
            // Check for null or empty values for OriginOid and DestinationOid
            if (string.IsNullOrEmpty(edge.OriginOid))
                throw new ArgumentNullException("Origin ID cannot be null or empty");

            if (string.IsNullOrEmpty(edge.DestinationOid))
                throw new ArgumentNullException("Destination ID cannot be null or empty");

            // Print the keys being checked
            Console.WriteLine($"Checking Origin ID: {edge.OriginOid}, Destination ID: {edge.DestinationOid}");

            // Check existence in the recordMap
            if (!recordMap.ContainsKey(edge.OriginOid)) missingOriginalIds.Add(edge.OriginOid);
            if (!recordMap.ContainsKey(edge.DestinationOid)) missingOriginalIds.Add(edge.DestinationOid);
        }

        if (missingOriginalIds.Any())
            throw new Exception($"Records not found matching Original IDs ({string.Join(", ", missingOriginalIds)})");
    }

    /// <summary>
    ///     Creates a list of record-tag pairs to be inserted into the linking table
    /// </summary>
    /// <param name="records"></param>
    /// <param name="tagMap"></param>
    /// <param name="recordMap"></param>
    /// <returns>A list of record_id: tag_id to be inserted into the linking table</returns>
    private List<RecordTagLinkDto> BuildRecordTags(
        List<CreateRecordRequestDto> records,
        Dictionary<string, TagResponseDto> tagMap,
        Dictionary<string, long> recordMap)
    {
        return records
            .Where(r => r.Tags != null && recordMap.ContainsKey(r.OriginalId))
            .SelectMany(r => r.Tags
                .Where(tag => tagMap.ContainsKey(tag))
                .Select(tag => new RecordTagLinkDto
                {
                    RecordId = recordMap[r.OriginalId],
                    TagId = tagMap[tag].Id
                }))
            .ToList();
    }

    /// <summary>
    ///     Bulk upserts classes and returns a mapping of class name to ID
    /// </summary>
    /// <param name="currentUserId"></param>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="organizationId"></param>
    /// <param name="classes"></param>
    /// <param name="metadataResponseDto"></param>
    /// <returns>A mapping of class name to class ID</returns>
    private async Task<Dictionary<string, long>> BulkUpsertClasses(
        long organizationId,
        long currentUserId,
        long projectId,
        List<CreateClassRequestDto> classes,
        MetadataResponseDto metadataResponseDto)
    {
        var inserted = await _classBusiness.BulkCreateClasses(
            organizationId, currentUserId, projectId, classes);
        metadataResponseDto.Classes = inserted;
        return inserted.ToDictionary(c => c.Name, c => c.Id);
    }

    /// <summary>
    ///     Bulk upserts relationships and returns a mapping of relationship name to ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="relationships"></param>
    /// <param name="metadataResponseDto"></param>
    /// <returns>A mapping of relationship name to relationship ID</returns>
    private async Task<Dictionary<string, long>> BulkUpsertRelationships(
        long organizationId,
        long currentUserId,
        long projectId,
        List<CreateRelationshipRequestDto> relationships,
        MetadataResponseDto metadataResponseDto)
    {
        var inserted =
            await _relationshipBusiness.BulkCreateRelationships(currentUserId, organizationId, projectId,
                relationships);
        metadataResponseDto.Relationships = inserted;
        return inserted.ToDictionary(r => r.Name, r => r.Id);
    }

    /// <summary>
    ///     Bulk upserts tags and returns a mapping of tag name to ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId"></param>
    /// <param name="organizationId"></param>
    /// <param name="tags"></param>
    /// <param name="metadataResponseDto"></param>
    /// <returns>A mapping of tag name to tag ID</returns>
    private async Task<Dictionary<string, TagResponseDto>> BulkUpsertTags(
        long organizationId,
        long currentUserId,
        long projectId,
        List<CreateTagRequestDto> tags,
        MetadataResponseDto metadataResponseDto)
    {
        var inserted = await _tagBusiness.BulkCreateTags(organizationId, currentUserId, projectId, tags);
        metadataResponseDto.Tags = inserted;
        return inserted.ToDictionary(t => t.Name, t => t);
    }

    /// <summary>
    ///     Bulk upserts records and returns a mapping of record original ID to database ID
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="projectId"></param>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="dataSourceId"></param>
    /// <param name="records"></param>
    /// <param name="metadataResponseDto"></param>
    /// <returns>A mapping of record name to record ID</returns>
    private async Task<Dictionary<string, long>> BulkUpsertRecords(
        long currentUserId,
        long projectId,
        long organizationId,
        long dataSourceId,
        List<CreateRecordRequestDto> records,
        MetadataResponseDto metadataResponseDto)
    {
        var inserted =
            await _recordBusiness.BulkCreateRecords(currentUserId, organizationId, projectId, dataSourceId, records);
        metadataResponseDto.Records = inserted;
        return inserted.ToDictionary(r => r.OriginalId, r => r.Id);
    }

    /// <summary>
    ///     Add the appropriate class IDs to any records with class names
    /// </summary>
    /// <param name="records"></param>
    /// <param name="classMap"></param>
    private void UpdateRecordsWithIds(
        List<CreateRecordRequestDto> records,
        Dictionary<string, long> classMap)
    {
        foreach (var record in records)
            if (!string.IsNullOrEmpty(record.ClassName)
                && classMap.TryGetValue(record.ClassName, out var classId))
                record.ClassId = classId;
    }

    /// <summary>
    ///     Add the appropriate relationship and record IDs to any edges with relationship names or record original IDs
    /// </summary>
    /// <param name="edges"></param>
    /// <param name="relMap"></param>
    /// <param name="recordMap"></param>
    private void UpdateEdgesWithIds(
        List<CreateEdgeRequestDto> edges,
        Dictionary<string, long> relMap,
        Dictionary<string, long> recordMap)
    {
        foreach (var edge in edges)
        {
            if (!string.IsNullOrEmpty(edge.RelationshipName)
                && relMap.TryGetValue(edge.RelationshipName, out var relationshipId))
                edge.RelationshipId = relationshipId;

            if (!string.IsNullOrEmpty(edge.OriginOid)
                && recordMap.TryGetValue(edge.OriginOid, out var originId))
                edge.OriginId = originId;

            if (!string.IsNullOrEmpty(edge.DestinationOid)
                && recordMap.TryGetValue(edge.DestinationOid, out var destinationId))
                edge.DestinationId = destinationId;
        }
    }

    /// <summary>
    ///     Adjust the returned record objects to include tags
    /// </summary>
    /// <param name="metadataResponseDto"></param>
    /// <param name="recordTags"></param>
    /// <param name="tagMap"></param>
    private void AttachTagsToRecordDtos(
        MetadataResponseDto metadataResponseDto,
        List<RecordTagLinkDto> recordTags,
        Dictionary<string, TagResponseDto> tagMap)
    {
        // create lookup dictionaries for quick reference
        var recordTagLookup = recordTags.ToLookup(rt => rt.RecordId, rt => rt.TagId);
        var tagsById = tagMap.Values.ToDictionary(t => t.Id, t => t);

        foreach (var record in metadataResponseDto.Records)
            record.Tags = recordTagLookup[record.Id]
                .Select(tagId => new RecordTagDto
                {
                    Id = tagsById[tagId].Id,
                    Name = tagsById[tagId].Name
                })
                .ToList();
    }

    /// <summary>
    ///     Determine if datasource exists
    /// </summary>
    /// <param name="datasourceId">The ID of the datasource we are searching for</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived projects from the result (Default true)</param>
    /// <returns>Throws error if datasource does not exist</returns>
    private void DoesDataSourceExist(long datasourceId, bool hideArchived = true)
    {
        var datasource = hideArchived
            ? _context.DataSources.Any(p => p.Id == datasourceId && !p.IsArchived)
            : _context.DataSources.Any(p => p.Id == datasourceId);
        if (!datasource) throw new KeyNotFoundException($"Datasource with id {datasourceId} not found");
    }
}