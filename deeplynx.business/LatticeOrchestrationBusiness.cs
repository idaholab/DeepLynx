using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class LatticeOrchestrationBusiness : ILatticeOrchestrationBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IExtractionBusiness _extractionBusiness;
    private readonly LatticeServiceClient _latticeClient;
    private readonly string _nexusBaseUrl;
    private readonly ITokenBusiness _tokenBusiness;

    public LatticeOrchestrationBusiness(
        DeeplynxContext context,
        IExtractionBusiness extractionBusiness,
        ITokenBusiness tokenBusiness,
        LatticeServiceClient latticeClient)
    {
        _context = context;
        _extractionBusiness = extractionBusiness;
        _tokenBusiness = tokenBusiness;
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
            // its callback. 240 minutes gives generous headroom for a ~45-minute extraction.
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
}