using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class LatticeOrchestrationBusiness : ILatticeOrchestrationBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IExtractionBusiness _extractionBusiness;
    private readonly IInsightBusiness _insightBusiness;
    private readonly LatticeServiceClient _latticeClient;
    private readonly string _nexusBaseUrl;
    private readonly ITokenBusiness _tokenBusiness;

    public LatticeOrchestrationBusiness(
        DeeplynxContext context,
        IExtractionBusiness extractionBusiness,
        ITokenBusiness tokenBusiness,
        IInsightBusiness insightBusiness,
        LatticeServiceClient latticeClient)
    {
        _context = context;
        _extractionBusiness = extractionBusiness;
        _tokenBusiness = tokenBusiness;
        _insightBusiness = insightBusiness;
        _latticeClient = latticeClient;
        _nexusBaseUrl = Environment.GetEnvironmentVariable("NEXUS_BASE_URL")
                        ?? throw new InvalidOperationException("NEXUS_BASE_URL environment variable is not set.");
    }

    public async Task<long> TriggerLatticeExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long dataSourceId,
        long recordId,
        string mode,
        int similarityLimit)
    {
        var record = await _context.Records
                         .Where(r => r.Id == recordId && r.ProjectId == projectId)
                         .FirstOrDefaultAsync()
                     ?? throw new InvalidOperationException($"Record {recordId} not found in project {projectId}");

        // For strict mode, embeddings must exist before triggering. If they're missing, queue
        // them automatically and fail fast so the caller can retry once they're ready.
        if (mode == ExtractionMode.Strict)
            await EnsureEmbeddingsReady(currentUserId, organizationId, projectId, recordId, record);

        var extraction = new Extraction
        {
            CreatedBy = currentUserId,
            Status = ExtractionStatus.Pending
        };
        _context.Extractions.Add(extraction);
        await _context.SaveChangesAsync();

        try
        {
            var ontologyContext = await _extractionBusiness.SearchOntologySimilarity(
                recordId, projectId, similarityLimit);

            // Generate a short-lived token for the triggering user so Lattice can authenticate
            // its callback. 240 minutes seems generous for a ~45-minute extraction.
            //TODO: Need service user logic to use tokens not tied to user accounts
            var apiKeyPair = await _tokenBusiness.CreateApiKey(currentUserId);
            var callbackToken = await _tokenBusiness.CreateToken(
                apiKeyPair.apiKey, apiKeyPair.apiSecret, 240);

            await _latticeClient.TriggerExtraction(new LatticeExtractionTriggerRequestDto
            {
                ExtractionId = extraction.Id,
                RecordId = recordId,
                DocumentUri = record.Uri,
                FileType = record.FileType,
                Mode = mode,
                OntologyContext = ontologyContext,
                NexusConfig = new LatticeNexusConfigDto
                {
                    OrgId = organizationId,
                    ProjectId = projectId,
                    DataSourceId = dataSourceId,
                    BaseUrl = _nexusBaseUrl,
                    Token = callbackToken
                }
            });

            extraction.Status = ExtractionStatus.Running;
            await _context.SaveChangesAsync();
        }
        catch
        {
            extraction.Status = ExtractionStatus.Failed;
            await _context.SaveChangesAsync();
            throw;
        }

        return extraction.Id;
    }

    /// <summary>
    ///     Checks whether the document record and project ontology are embedded.
    ///     Any missing embeddings are queued automatically.
    ///     Throws <see cref="InvalidOperationException"/> if either is not yet ready,
    ///     so the caller can retry once embedding completes.
    /// </summary>
    private async Task EnsureEmbeddingsReady(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        Record record)
    {
        var recordEmbedded = await _context.Embeddings.AnyAsync(e => e.RecordId == recordId);
        var ontologyEmbedded = await _context.OntologyVectors
            .AnyAsync(ov =>
                _context.Classes.Any(c => c.Id == ov.ClassId && c.ProjectId == projectId) ||
                _context.Relationships.Any(r => r.Id == ov.RelationshipId && r.ProjectId == projectId));

        if (recordEmbedded && ontologyEmbedded) return;

        if (!recordEmbedded)
        {
            var vlmConfig = await _insightBusiness.ResolveModelConfig(
                currentUserId, organizationId, projectId, null, "vlm");
            var embeddingConfig = await _insightBusiness.ResolveModelConfig(
                currentUserId, organizationId, projectId, null, "embedding");
            _insightBusiness.TriggerEmbedding(projectId, recordId, record.Uri!, vlmConfig, embeddingConfig);
        }

        if (!ontologyEmbedded)
        {
            await _insightBusiness.QueueInsightEmbedStrings(currentUserId, organizationId, projectId, null);
        }

        throw new InvalidOperationException(
            $"Embeddings are being generated for this extraction." +
            "Please retry the extraction in a few minutes.");
    }
}
