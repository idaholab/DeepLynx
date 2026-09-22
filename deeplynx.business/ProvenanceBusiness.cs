using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using deeplynx.models.ResponseDTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace deeplynx.business;

public class ProvenanceBusiness : IProvenanceBusiness
{
    private const int MaxChainConflictRetries = 3;

    private readonly DeeplynxContext _context;
    private readonly ILogger<ProvenanceBusiness> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProvenanceBusiness" /> class
    /// </summary>
    /// <param name="context">Database context used for provenance operations</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public ProvenanceBusiness(DeeplynxContext context, ILogger<ProvenanceBusiness> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    ///     Look up the chain hash of the most recent chained provenance record for a given
    ///     record_id, defaulting to the genesis sentinel if the record has no chained history yet
    ///     (either no provenance records exist, or only pre-chain legacy rows exist).
    /// </summary>
    private async Task<string> GetLatestChainHashAsync(long recordId)
    {
        var latest = await _context.ProvenanceRecords
            .Where(p => p.RecordId == recordId && p.ChainHash != null)
            .OrderByDescending(p => p.Id)
            .Select(p => p.ChainHash)
            .FirstOrDefaultAsync();

        return latest ?? ProvenanceChainEnvelope.GenesisHash;
    }

    /// <summary>
    ///     Bulk variant of <see cref="GetLatestChainHashAsync" />: looks up the latest chain hash
    ///     per distinct record_id in a single query. Record IDs with no chained history are simply
    ///     absent from the result; callers should default those to the genesis sentinel.
    /// </summary>
    private async Task<Dictionary<long, string>> GetLatestChainHashesAsync(IEnumerable<long> recordIds)
    {
        var ids = recordIds.Distinct().ToArray();

        var latest = await _context.ProvenanceRecords
            .FromSqlInterpolated($@"
                SELECT DISTINCT ON (record_id) *
                FROM deeplynx.provenance_records
                WHERE record_id = ANY({ids}) AND chain_hash IS NOT NULL
                ORDER BY record_id, id DESC")
            .AsNoTracking()
            .ToListAsync();

        return latest.ToDictionary(p => p.RecordId, p => p.ChainHash!);
    }

    /// <summary>
    ///     True if the given exception is a unique-violation on the chain-start/chain-link
    ///     partial unique index, meaning a concurrent writer won the race to extend this
    ///     record's chain first and the caller should retry with a freshly-read previous hash.
    /// </summary>
    private static bool IsChainConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg &&
        pg.ConstraintName == "ux_provenance_records_record_id_previous_hash";

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
            PreviousHash = provenanceRecord.PreviousHash,
            ChainHash = provenanceRecord.ChainHash,
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
                PreviousHash = r.PreviousHash,
                ChainHash = r.ChainHash,
                CreatedAt = r.CreatedAt
            }).ToList()
        };
    }

    /// <summary>
    ///     Retrieve every provenance record ever created for a project, most recent first,
    ///     including provenance for records/historical records that have since been deleted.
    ///     Unlike <see cref="GetProvenanceHistory" />, this does not check whether any
    ///     individual record still exists — only that the project does.
    /// </summary>
    /// <param name="projectId">The ID of the project whose provenance history is being retrieved</param>
    /// <param name="paginatedRequestDto">Pagination parameters</param>
    /// <returns>A paginated list of provenance records for the given project, most recent first</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no matching project is found</exception>
    public async Task<PaginatedResponse<ProvenanceRecordResponseDto>> GetProjectProvenanceHistory(
        long projectId, PaginatedRequestDto paginatedRequestDto)
    {
        var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
            throw new KeyNotFoundException($"Project with id {projectId} not found");

        // ThenByDescending(Id) breaks ties deterministically: BulkCreateProvenanceRecords
        // stamps one shared CreatedAt across a whole batch, so CreatedAt alone isn't unique
        // enough to page through without skipping or duplicating rows.
        return await _context.ProvenanceRecords
            .Where(p => p.ProjectId == projectId)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Select(p => new ProvenanceRecordResponseDto
            {
                Id = p.Id,
                RecordId = p.RecordId,
                HistoricalRecordId = p.HistoricalRecordId,
                OrganizationId = p.OrganizationId,
                ProjectId = p.ProjectId,
                ProvId = p.ProvId,
                ProvenanceJson = p.ProvenanceJson,
                FileContentHash = p.FileContentHash,
                Signature = p.Signature,
                PreviousHash = p.PreviousHash,
                ChainHash = p.ChainHash,
                CreatedAt = p.CreatedAt
            })
            .ToPaginatedAsync(paginatedRequestDto);
    }

    /// <summary>
    ///     Recompute the hash chain for a record's provenance history and compare it against
    ///     what's stored, to detect tampering. No external calls, no keys — a local, synchronous
    ///     recomputation using <see cref="ProvenanceChainEnvelope" />.
    /// </summary>
    /// <param name="recordId">The ID of the record whose provenance chain is being verified</param>
    /// <param name="checkpointRecordId">
    ///     (Optional) A previously-verified provenance record ID to resume verification from,
    ///     instead of walking the whole chain from genesis. Its stored chain hash is trusted as
    ///     the starting previous-hash; only later records in the same record's chain are checked.
    /// </param>
    /// <returns>A report describing whether the chain (or the portion after the checkpoint) is intact</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no matching record is found</exception>
    /// <exception cref="InvalidRequestException">
    ///     Thrown if <paramref name="checkpointRecordId" /> doesn't reference a chained provenance
    ///     record belonging to <paramref name="recordId" />
    /// </exception>
    public async Task<ProvenanceChainVerificationResponseDto> VerifyProvenanceChain(
        long recordId, long? checkpointRecordId = null)
    {
        var recordExists = await _context.Records.AnyAsync(r => r.Id == recordId);
        if (!recordExists)
            throw new KeyNotFoundException($"Record with id {recordId} not found");

        var previousHash = ProvenanceChainEnvelope.GenesisHash;
        var query = _context.ProvenanceRecords
            .Where(p => p.RecordId == recordId && p.ChainHash != null);

        if (checkpointRecordId is not null)
        {
            var checkpoint = await _context.ProvenanceRecords
                .FirstOrDefaultAsync(p => p.Id == checkpointRecordId);

            if (checkpoint is null || checkpoint.RecordId != recordId || checkpoint.ChainHash is null)
                throw new InvalidRequestException(
                    $"Checkpoint provenance record {checkpointRecordId} is not a chained record for record {recordId}");

            previousHash = checkpoint.ChainHash;
            query = query.Where(p => p.Id > checkpoint.Id);
        }

        var chain = await query.OrderBy(p => p.Id).ToListAsync();

        var response = new ProvenanceChainVerificationResponseDto
        {
            RecordId = recordId,
            CheckpointRecordId = checkpointRecordId,
            IsValid = true
        };

        foreach (var row in chain)
        {
            var expectedChainHash = ProvenanceChainEnvelope.HashBase64(row, previousHash);

            if (row.PreviousHash != previousHash || expectedChainHash != row.ChainHash)
            {
                response.IsValid = false;
                response.FirstInvalidProvenanceRecordId = row.Id;
                response.ExpectedChainHash = expectedChainHash;
                response.ActualChainHash = row.ChainHash;
                response.Message =
                    $"Chain verification failed at provenance record {row.Id}: stored hash does not " +
                    "match the recomputed value";
                return response;
            }

            previousHash = row.ChainHash!;
            response.RecordsVerified++;
        }

        response.Message = response.RecordsVerified == 0
            ? $"No chained provenance records exist for record {recordId}"
            : $"Chain verified: {response.RecordsVerified} record(s), no tampering detected";
        return response;
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

        // chain this record to the most recent one for the same record_id, retrying if a
        // concurrent writer wins the race to extend the chain first
        for (var attempt = 1; ; attempt++)
        {
            var previousHash = await GetLatestChainHashAsync(recordId);
            var provenanceRecord = new ProvenanceRecord
            {
                RecordId = recordId,
                HistoricalRecordId = historicalRecord.Id,
                OrganizationId = historicalRecord.OrganizationId,
                ProjectId = historicalRecord.ProjectId,
                ProvId = provId,
                FileContentHash = historicalRecord.FileContentHash,
                ProvenanceJson = provenanceJson,
                // leaving null for now; a future signing approach can layer a signature over
                // the chain hash without a schema change
                Signature = null,
                PreviousHash = previousHash,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            provenanceRecord.ChainHash = ProvenanceChainEnvelope.HashBase64(provenanceRecord, previousHash);

            _context.ProvenanceRecords.Add(provenanceRecord);
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex) when (IsChainConflict(ex) && attempt < MaxChainConflictRetries)
            {
                _context.ChangeTracker.Clear();
                _logger.LogWarning(ex,
                    "CreateProvenanceRecord: chain conflict for record {RecordId} on attempt {Attempt}, retrying",
                    recordId, attempt);
            }
        }
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
        {
            // some of the supplied records don't have historical records associated
            var foundIds = historicalRecords.Select(h => h.RecordId).ToHashSet();
            var missingIds = distinctRecordIds.Where(id => !foundIds.Contains(id)).ToList();

            _logger.LogWarning(
                "BulkCreateProvenanceRecords: no historical record found for record_ids [{MissingIds}]",
                string.Join(", ", missingIds));

            return false;
        }

        // if aiConfig is passed in, fetch once and reuse for each prov record
        AiModelConfig? aiConfig = null;
        if (aiConfigId is not null)
        {
            aiConfig = await _context.AiModelConfigs
                .Where(a => a.Id == aiConfigId)
                .FirstOrDefaultAsync();
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // build the provenance JSON once per record; only the chain fields depend on the
        // latest chain state, which is re-read on every retry attempt below
        var pending = historicalRecords.Select(histRecord =>
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

            return (histRecord, provenanceJson, provId);
        }).ToList();

        // chain each record to its own most recent provenance entry, retrying the whole batch
        // if a concurrent writer wins the race to extend one of these records' chains first
        for (var attempt = 1; ; attempt++)
        {
            var latestChainHashes = await GetLatestChainHashesAsync(distinctRecordIds);

            var provenanceRecords = pending.Select(p =>
            {
                var previousHash = latestChainHashes.GetValueOrDefault(
                    p.histRecord.RecordId, ProvenanceChainEnvelope.GenesisHash);

                var provenanceRecord = new ProvenanceRecord
                {
                    RecordId = p.histRecord.RecordId,
                    HistoricalRecordId = p.histRecord.Id,
                    OrganizationId = p.histRecord.OrganizationId,
                    ProjectId = p.histRecord.ProjectId,
                    ProvId = p.provId,
                    FileContentHash = p.histRecord.FileContentHash,
                    ProvenanceJson = p.provenanceJson,
                    // leaving null for now; a future signing approach can layer a signature over
                    // the chain hash without a schema change
                    Signature = null,
                    PreviousHash = previousHash,
                    CreatedAt = now
                };
                provenanceRecord.ChainHash = ProvenanceChainEnvelope.HashBase64(provenanceRecord, previousHash);
                return provenanceRecord;
            }).ToList();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // save all bulk-created prov records in a single DB trip
                _context.ProvenanceRecords.AddRange(provenanceRecords);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch (DbUpdateException ex) when (IsChainConflict(ex) && attempt < MaxChainConflictRetries)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                _logger.LogWarning(ex,
                    "BulkCreateProvenanceRecords: chain conflict on attempt {Attempt}, retrying whole batch",
                    attempt);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                var failedRecordIds = ex.Entries
                    .Select(e => e.Entity)
                    .OfType<ProvenanceRecord>()
                    .Select(p => p.RecordId)
                    .ToList();

                _logger.LogError(ex,
                    "BulkCreateProvenanceRecords failed while saving provenance records for record_ids [{FailedIds}]",
                    string.Join(", ", failedRecordIds));

                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "BulkCreateProvenanceRecords failed unexpectedly for record_ids [{RecordIds}]",
                    string.Join(", ", distinctRecordIds));
                throw;
            }
        }
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