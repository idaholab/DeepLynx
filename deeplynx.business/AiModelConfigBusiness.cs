using deeplynx.datalayer.Models;
using deeplynx.models;

namespace deeplynx.business;

public class AiModelConfigBusiness
{
    private readonly DeeplynxContext _context;

    public AiModelConfigBusiness(DeeplynxContext context)
    {
        _context = context;
    }
    
    // get all
    public async Task<List<AiModelConfigResponseDto>> GetAllAiModelConfigs(
        long organizationId,
        long projectId)
    {
        var query = _context.
    };
    
    // get by Id
    


    // create
    // update
    // delete
// archive
    
}