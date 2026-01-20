using deeplynx.models;

namespace deeplynx.interfaces;

public interface IEdgeBusiness
{
    Task<List<EdgeResponseDto>> GetAllEdges(
        long organizationId, long projectId, long? dataSourceId, bool hideArchived);

    Task<EdgeResponseDto> GetEdge(
        long organizationId, long projectId, long? edgeId, long? originId, long? destinationId, bool hideArchived);

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