using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using deeplynx.models.ResponseDTOs;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class ProvenanceBusiness : IProvenanceBusiness
{
    private readonly DeeplynxContext _context;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProvenanceBusiness" /> class
    /// </summary>
    /// <param name="context">Database context used for provenance operations</param>
    public ProvenanceBusiness(DeeplynxContext context)
    {
        _context = context;
    }
    
    /// <summary>
    ///     Retrieve a single provenance record by its database ID.
    /// </summary>
    /// <param name="provenanceRecordId">The database ID of the provenance record</param>
    /// <returns>The matching provenance record</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no matching provenance record is found</exception>
    public async Task<ProvenanceRecordResponseDto> GetProvenanceRecord(long provenanceRecordId)
    {
        var provenanceRecord = await _context.ProvenanceRecords
            .Where(p => p.Id == provenanceRecordId)
            .FirstOrDefaultAsync();

        if (provenanceRecord is null)
            throw new KeyNotFoundException(
                $"Provenance record with id {provenanceRecordId} not found");

        return new ProvenanceRecordResponseDto
        {
            Id = provenanceRecord.Id,
            RecordId = provenanceRecord.RecordId,
            HistoricalRecordId = provenanceRecord.HistoricalRecordId,
            OrganizationId = provenanceRecord.OrganizationId,
            ProjectId = provenanceRecord.ProjectId,
            ProvId = provenanceRecord.ProvId,
            ProvenanceJson = provenanceRecord.ProvenanceJson,
            FileContentHash = provenanceRecord.FileContentHash,
            Signature = provenanceRecord.Signature,
            CreatedAt = provenanceRecord.CreatedAt
        };
    }
    
    /// <summary>
    ///     Retrieve all provenance records associated with a given (non-historical) record ID,
    ///     ordered most-recent first.
    /// </summary>
    /// <param name="recordId">The ID of the record whose provenance history is being retrieved</param>
    /// <returns>List of provenance records for the given record ID, most recent first</returns>
    public async Task<ProvenanceHistoryResponseDto> GetProvenanceHistory(long recordId)
    {
        // check if a record with the supplied record ID exists
        var recordExists = await _context.Records.AnyAsync(r => r.Id == recordId);
        if (!recordExists)
            throw new KeyNotFoundException($"Record with id {recordId} not found");
        
        // order the records newest to oldest
        var records = await _context.ProvenanceRecords
            .Where(p => p.RecordId == recordId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        if (records.Count == 0)
            return new ProvenanceHistoryResponseDto
            {
                Message = $"No provenance history exists yet for record {recordId}",
                Records = new List<ProvenanceRecordResponseDto>()
            };

        return new ProvenanceHistoryResponseDto
        {
            Records = records.Select(r => new ProvenanceRecordResponseDto
            {
                Id = r.Id,
                RecordId = r.RecordId,
                HistoricalRecordId = r.HistoricalRecordId,
                OrganizationId = r.OrganizationId,
                ProjectId = r.ProjectId,
                ProvId = r.ProvId,
                ProvenanceJson = r.ProvenanceJson,
                FileContentHash = r.FileContentHash,
                Signature = r.Signature,
                CreatedAt = r.CreatedAt
            }).ToList()
        };
    }

    /// <summary>
    ///     Create a provenance record based on the supplied information.
    /// </summary>
    /// <param name="recordId">The ID of the record for which provenance is being updated</param>
    /// <param name="action">The action that triggered the provenance update</param>
    /// <param name="currentUserId">The user that triggered the provenance update</param>
    /// <param name="aiConfigId">(Optional) AI model information, if an embedding event</param>
    /// <returns>Boolean- if false, throw an error at the caller level</returns>
    public async Task<bool> CreateProvenanceRecord(
        long recordId,
        string action,
        long currentUserId,
        long? aiConfigId)
    {
        // get the latest historical record
        var historicalRecord = await _context.HistoricalRecords
            .Where(r => r.RecordId == recordId)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        if (historicalRecord is null)
        {
            // no historical record exists for the record ID. Throw an error at the caller level
            return false;
        }

        // if aiConfigId is passed in, include model information in the record
        AiModelConfig? aiConfig = null;
        if (aiConfigId is not null)
        {
            aiConfig = await _context.AiModelConfigs
                .Where(a => a.Id == aiConfigId)
                .FirstOrDefaultAsync();
        }

        // build the provenance record
        var provenanceJson = BuildProvenanceRecord(new BuildProvenanceRecordDto
        {
            RecordId = recordId,
            HistoricalRecordId = historicalRecord.Id,
            Action = action,
            ActorId = currentUserId,
            OrganizationId = historicalRecord.OrganizationId,
            ProjectId = historicalRecord.ProjectId,
            FileUri = historicalRecord.Uri,
            FileHash = historicalRecord.FileContentHash,
            FileSize = historicalRecord.FileSize,
            FileType = historicalRecord.FileType,
            AiConfigId = aiConfigId,
            AiModelProvider = aiConfig?.ModelProvider,
            AiModelName = aiConfig?.ModelName,
            AiModelType = aiConfig?.ModelType,
            AiServerUrl = aiConfig?.ServerUrl
        }, out var provId);

        // save the provenance JSON and relevant extracted fields to the DB
        var provenanceRecord = new ProvenanceRecord
        {
            RecordId = recordId,
            HistoricalRecordId = historicalRecord.Id,
            OrganizationId = historicalRecord.OrganizationId,
            ProjectId = historicalRecord.ProjectId,
            ProvId = provId,
            FileContentHash = historicalRecord.FileContentHash,
            ProvenanceJson = provenanceJson,
            // leaving null for now until we get hashing and signatures implemented
            Signature = null,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        _context.ProvenanceRecords.Add(provenanceRecord);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Bulk Create provenance records based on a supplied set of record IDs
    /// </summary>
    /// <param name="recordIds">The IDs of the records for which provenance is being updated</param>
    /// <param name="action">The action that triggered the provenance update</param>
    /// <param name="currentUserId">The user that triggered the provenance update</param>
    /// <param name="aiConfigId">(Optional) AI model information, if an embedding event</param>
    /// <returns>Boolean- if false, throw an error at the caller level</returns>
    public async Task<bool> BulkCreateProvenanceRecords(
        List<long> recordIds, 
        string action, 
        long currentUserId, 
        long? aiConfigId)
    {
        if (recordIds == null || !recordIds.Any())
            return true;
        
        // deduplicate requested records
        var distinctRecordIds = recordIds.Distinct().ToList();
        
        // get the latest historical record per record_id in a single trip
        var historicalRecords = await _context.HistoricalRecords
            .FromSqlInterpolated($@"
                SELECT DISTINCT ON (record_id) *
                FROM deeplynx.historical_records
                WHERE record_id = ANY({distinctRecordIds.ToArray()})
                ORDER BY record_id, last_updated_at DESC, id DESC")
            .ToListAsync();

        if (historicalRecords.Count != distinctRecordIds.Count)
            // some of the supplied records don't have historical records associated
            return false;
        
        // if aiConfig is passed in, fetch once and reuse for each prov record
        AiModelConfig? aiConfig = null;
        if (aiConfigId is not null)
        {
            aiConfig = await _context.AiModelConfigs
                .Where(a => a.Id == aiConfigId)
                .FirstOrDefaultAsync();
        }
        
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var provenanceRecords = new List<ProvenanceRecord>(historicalRecords.Count);

        foreach (var histRecord in historicalRecords)
        {
            var provenanceJson = BuildProvenanceRecord(new BuildProvenanceRecordDto
            {
                RecordId = histRecord.RecordId,
                HistoricalRecordId = histRecord.Id,
                Action = action,
                ActorId = currentUserId,
                OrganizationId = histRecord.OrganizationId,
                ProjectId = histRecord.ProjectId,
                FileUri = histRecord.Uri,
                FileHash = histRecord.FileContentHash,
                FileSize = histRecord.FileSize,
                FileType = histRecord.FileType,
                AiConfigId = aiConfigId,
                AiModelProvider = aiConfig?.ModelProvider,
                AiModelName = aiConfig?.ModelName,
                AiModelType = aiConfig?.ModelType,
                AiServerUrl = aiConfig?.ServerUrl
            }, out var provId);
            
            provenanceRecords.Add(new ProvenanceRecord
            {
                RecordId = histRecord.RecordId,
                HistoricalRecordId = histRecord.Id,
                OrganizationId = histRecord.OrganizationId,
                ProjectId = histRecord.ProjectId,
                ProvId = provId,
                FileContentHash = histRecord.FileContentHash,
                ProvenanceJson = provenanceJson,
                // leaving null for now until we get hashing and signatures implemented
                Signature = null,
                CreatedAt = now
            });
        }
        
        // save all bulk-created prov records in a single DB trip
        _context.ProvenanceRecords.AddRange(provenanceRecords);
        await _context.SaveChangesAsync();
        
        return true;
    }

    /// <summary>
    ///     Build a W3C PROV-O JSON-LD record for a given activity.
    /// </summary>
    /// <param name="dto">All the fields needed to build the provenance graph</param>
    /// <param name="provId">Outputs the unique urn id generated for this provenance document</param>
    /// <returns>The provenance JSON graph and the provenance ID</returns>
    private static string BuildProvenanceRecord(BuildProvenanceRecordDto dto, out string provId)
    {
        var baseUrl = Environment.GetEnvironmentVariable("HOSTED_LINK") ?? "http://localhost:5000";

        string provNamespace = "http://www.w3.org/ns/prov#";
        string nexusNamespace = $"{baseUrl.TrimEnd('/')}/ns/provenance#";

        var historicalRecordUrn = $"urn:deeplynx:historical-record:{dto.HistoricalRecordId}";
        var recordUrn = $"urn:deeplynx:record:{dto.RecordId}";
        // note that the activity URN is just the historical record with the prefix
        // "activity". This is due to the fact that the historical record is uniquely
        // created by this activity (a new activity would create a new hist. record)
        var activityUrn = $"urn:deeplynx:activity:historical-record:{historicalRecordUrn}";
        var userUrn = $"urn:deeplynx:user:{dto.ActorId}";

        // used in prov-o format to reference another prov-o graph object
        static Dictionary<string, object> Ref(string id)
        {
            return new() { ["@id"] = id };
        }

        // this will purge the input dictionary of any null values
        static Dictionary<string, object> Compact(Dictionary<string, object?> node)
        {
            return node.Where(keyValPair => keyValPair.Value is not null)
                .ToDictionary(keyValPair => keyValPair.Key, keyValPair => keyValPair.Value!);
        }

        var graph = new List<object>
        {
            // the deeplynx record, independent of version
            new Dictionary<string, object?>
            {
                ["@id"] = recordUrn,
                // specifying the entity type is essential to prov-o standards
                ["@type"] = "prov:Entity",
                ["nexus:entityType"] = "record",
                ["nexus:recordId"] = dto.RecordId,
                ["nexus:organizationId"] = dto.OrganizationId,
                ["nexus:projectId"] = dto.ProjectId,
            },

            // the historical record, specific to this point in time
            Compact(new Dictionary<string, object?>
            {
                ["@id"] = historicalRecordUrn,
                // specifying the entity type is essential to prov-o standards
                ["@type"] = "prov:Entity",
                ["nexus:entityType"] = "historical_record",
                ["nexus:historicalRecordId"] = dto.HistoricalRecordId,
                ["nexus:recordId"] = dto.RecordId,
                ["nexus:organizationId"] = dto.OrganizationId,
                ["nexus:projectId"] = dto.ProjectId,
                // these relationships are essential components of prov-o
                // as they show related entities, activities and agents
                ["prov:specializationOf"] = Ref(recordUrn),
                ["prov:wasGeneratedBy"] = Ref(activityUrn),
                ["prov:wasAttributedTo"] = Ref(userUrn),
            }),

            // the action taken to produce this version of the historical record
            new Dictionary<string, object?>
            {
                ["@id"] = activityUrn,
                // specifying activity type is essential to prov-o standards
                ["@type"] = "prov:Activity",
                ["nexus:action"] = dto.Action,
                ["prov:wasAssociatedWith"] = Ref(userUrn),
            },

            // the user who performed the action
            // this will eventually count agents using service user IDs
            new Dictionary<string, object?>
            {
                ["@id"] = userUrn,
                ["@type"] = "prov:Agent",
                ["nexus:agentType"] = "user",
                ["nexus:userId"] = dto.ActorId,
            },
        };

        if (dto.FileUri is not null)
        {
            graph.Add(Compact(new Dictionary<string, object?>
            {
                ["@id"] = $"urn:deeplynx:file:{dto.HistoricalRecordId}",
                ["@type"] = "prov:Entity",
                ["nexus:entityType"] = "file",
                ["nexus:fileUri"] = dto.FileUri,
                ["nexus:fileHash"] = dto.FileHash,
                ["nexus:fileSizeBytes"] = dto.FileSize,
                ["nexus:fileType"] = dto.FileType,
                // show the connection between the metadata (hist rec) and the data (file)
                ["prov:wasDerivedFrom"] = Ref(historicalRecordUrn),
                ["prov:wasAttributedTo"] = Ref(userUrn),
            }));
        }

        if (dto.AiConfigId is not null)
        {
            var embeddingEntityUrn = $"urn:deeplynx:embedding:{dto.HistoricalRecordId}";
            var embeddingAgentUrn = $"urn:deeplynx:agent:ai-model:{dto.AiConfigId}";
            var embeddingActivityUrn = $"urn:deeplynx:activity:embedding-generation:{historicalRecordUrn}";

            graph.Add(
                // the embedding itself
                Compact(new Dictionary<string, object?>
                {
                    ["@id"] = embeddingEntityUrn,
                    ["@type"] = "prov:Entity",
                    ["nexus:entityType"] = "embedding",
                    ["nexus:RecordId"] = dto.RecordId,
                    ["prov:wasGeneratedBy"] = Ref(embeddingActivityUrn)
                }));

            graph.Add(
                // the embedding activity
                new Dictionary<string, object?>
                {
                    ["@id"] = embeddingActivityUrn,
                    ["@type"] = "prov:Activity",
                    ["nexus:action"] = "embedding_generation",
                    ["prov:wasAssociatedWith"] = Ref(embeddingAgentUrn)
                });

            graph.Add(
                // the AI model that carreid out the embedding
                Compact(new Dictionary<string, object?>
                {
                    ["@id"] = embeddingAgentUrn,
                    ["@type"] = "prov:Agent",
                    ["nexus:agentType"] = "ai_model",
                    ["nexus:aiModelProvider"] = dto.AiModelProvider,
                    ["nexus:aiModelName"] = dto.AiModelName,
                    ["nexus:aiModelType"] = dto.AiModelType,
                    ["nexus:aiServerUrl"] = dto.AiServerUrl
                }));
        }

        // add a unique identifier for the provenance json itself
        // just in case there is a need for external publishing of provenance
        provId = $"urn:deeplynx:provenance:{Guid.NewGuid():N}";

        var document = new Dictionary<string, object>
        {
            // add a unique identifier for the provenance json itself
            // just in case there is a need for external publishing
            ["@id"] = provId,
            ["@context"] = new Dictionary<string, string>
            {
                ["prov"] = provNamespace,
                ["nexus"] = nexusNamespace,
            },
            ["@graph"] = graph
        };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }
}