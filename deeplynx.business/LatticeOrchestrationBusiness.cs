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
    private readonly string _nexusServiceToken;

    public LatticeOrchestrationBusiness(
        DeeplynxContext context,
        IExtractionBusiness extractionBusiness,
        LatticeServiceClient latticeClient)
    {
        _context = context;
        _extractionBusiness = extractionBusiness;
        _latticeClient = latticeClient;
        _nexusBaseUrl = Environment.GetEnvironmentVariable("NEXUS_BASE_URL")
            ?? throw new InvalidOperationException("NEXUS_BASE_URL environment variable is not set.");
        _nexusServiceToken = Environment.GetEnvironmentVariable("NEXUS_SERVICE_TOKEN")
            ?? throw new InvalidOperationException("NEXUS_SERVICE_TOKEN environment variable is not set.");
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
        // check record exists before proceeding 
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
                    Token = _nexusServiceToken
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
