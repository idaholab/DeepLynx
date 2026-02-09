using deeplynx.models;

namespace deeplynx.interfaces;

public interface IOrganizationBusiness
{
    Task<IEnumerable<OrganizationResponseDto>> GetAllOrganizations(bool hideArchived = true);
    Task<IEnumerable<OrganizationResponseDto>> GetAllOrganizationsForUser(long currentUserId, bool hideArchived = true);
    Task<OrganizationResponseDto> GetOrganization(long organizationId, bool hideArchived = true);

    Task<OrganizationResponseDto> CreateOrganization(long currentUserId, CreateOrganizationRequestDto dto,
        bool isDefault = false);

    Task<OrganizationResponseDto> UpdateOrganization(long currentUserId, long organizationId,
        UpdateOrganizationRequestDto dto);

    Task<bool> ArchiveOrganization(long currentUserId, long organizationId);
    Task<bool> UnarchiveOrganization(long currentUserId, long organizationId);
    Task<bool> DeleteOrganization(long organizationId);
    Task<bool> AddUserToOrganization(long organizationId, long userId, bool isAdmin = false);
    Task<bool> SetOrganizationAdminStatus(long organizationId, long userId, bool isAdmin = false);
    Task<bool> RemoveUserFromOrganization(long organizationId, long userId);
}