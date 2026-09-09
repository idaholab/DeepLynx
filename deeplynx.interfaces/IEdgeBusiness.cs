using deeplynx.models;

namespace deeplynx.interfaces;

public interface IEdgeBusiness
{
    [Obsolete("V1-only. Used by deprecated v1 edge endpoints. Superseded by GetAllEdgesPaginated. " +
              "Remove once v1 edge endpoints are sunset.", error: false)]
    Task<List<EdgeResponseDto>> GetAllEdges(
        long currentUserId, long organizationId, long projectId, long? dataSourceId, bool hideArchived);

    Task<PaginatedResponse<EdgeResponseDto>> GetAllEdgesPaginated(
        long currentUserId,
        long organizationId,
        long projectId,
        PaginatedRequestDto paginatedRequestDto,
        long? dataSourceId = null,
        bool hideArchived = true,
        bool isSysAdmin = false,
        bool isOrgAdmin = false);

    Task<EdgeResponseDto> GetEdge(
        long currentUserId, long organizationId, long projectId, long? edgeId, long? originId, long? destinationId, bool hideArchived);

    Task<EdgeResponseDto> CreateEdge(
        long currentUserId, long organizationId, long projectId, long dataSourceId, CreateEdgeRequestDto edge);

    Task<List<EdgeResponseDto>> BulkCreateEdges(
        long currentUserId, long organizationId, long projectId, long dataSourceId,
        List<CreateEdgeRequestDto> edgeRequestDtos);

    Task<EdgeResponseDto> UpdateEdge(
        long currentUserId, long organizationId, long projectId, UpdateEdgeRequestDto edge, long? edgeId,
        long? originId,
        long? destinationId);

    Task<long> DeleteEdge(
        long currentUserId, long organizationId, long projectId, long? edgeId, long? originId, long? destinationId);

    Task<long> ArchiveEdge(
        long currentUserId, long organizationId, long projectId, long? edgeId, long? originId, long? destinationId);

    Task<long> UnarchiveEdge(
        long currentUserId, long organizationId, long projectId, long? edgeId, long? originId, long? destinationId);

    Task<List<LatticeEdgeDto>> GetLatticeEdges(long organizationId, long projectId);
}