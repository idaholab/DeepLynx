using deeplynx.models;

namespace deeplynx.interfaces;

public interface IGraphBusiness
{
    Task<List<RelatedRecordsResponseDto>> GetEdgesByRecord(
        long currentUserId, long organizationId, long projectId, long recordId, bool isOrigin, int page, int pageSize);

    Task<GraphResponse> GetGraphDataForRecord(
        long organizationId, long projectId, long recordId, long userId, int depth,
        bool isSysAdmin = false, bool isOrgAdmin = false);
}