using System;
using deeplynx.models;

namespace deeplynx.interfaces;

public interface IRelationshipBusiness
{
    [Obsolete("V1-only. Used by deprecated v1 relationship endpoints. Superseded by GetAllRelationshipsPaginated. " +
              "Remove once v1 relationship endpoints are sunset.", error: false)]
    Task<List<RelationshipResponseDto>> GetAllRelationships(long organizationId, long[]? projectIds, bool hideArchived);

    Task<PaginatedResponse<RelationshipResponseDto>> GetAllRelationshipsPaginated(long organizationId,
        long[]? projectIds, PaginatedRequestDto paginatedRequestDto, bool hideArchived = true);

    Task<RelationshipResponseDto> GetRelationship(long organizationId, long? projectId, long relationshipId,
        bool hideArchived);

    Task<List<RelationshipResponseDto>> BulkCreateRelationships(long currentUserId, long organizationId,
        long? projectId,
        List<CreateRelationshipRequestDto> relationshipRequestDtos);

    Task<RelationshipResponseDto> CreateRelationship(long currentUserId, long organizationId, long? projectId,
        CreateRelationshipRequestDto dto);

    Task<RelationshipResponseDto> UpdateRelationship(long currentUserId, long organizationId, long? projectId,
        long relationshipId, UpdateRelationshipRequestDto dto);

    Task<bool> DeleteRelationship(long currentUserId, long organizationId, long? projectId, long relationshipId);
    Task<bool> ArchiveRelationship(long currentUserId, long organizationId, long? projectId, long relationshipId);
    Task<bool> UnarchiveRelationship(long currentUserId, long organizationId, long? projectId, long relationshipId);

    Task<List<RelationshipResponseDto>> GetRelationshipsByName(long organizationId, long? projectId,
        List<string> relationshipNames);

    Task<List<LatticeRelationshipDto>> GetLatticeRelationships(long organizationId, long projectId);
}