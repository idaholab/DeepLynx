using deeplynx.models;
using Microsoft.AspNetCore.Http;

namespace deeplynx.interfaces;

public interface IOrganizationBusiness
{
    [Obsolete("V1-only. Used by deprecated v1 organization endpoints. Superseded by GetAllOrganizationsPaginated. " +
              "Remove once v1 organization endpoints are sunset.", error: false)]
    Task<IEnumerable<OrganizationResponseDto>> GetAllOrganizations(long userId, bool hideArchived = true, bool isSysAdmin = false);

    [Obsolete("V1-only. Used by deprecated v1 organization endpoints. Superseded by GetAllOrganizationsForUserPaginated. " +
              "Remove once v1 organization endpoints are sunset.", error: false)]
    Task<IEnumerable<OrganizationResponseDto>> GetAllOrganizationsForUser(long currentUserId, bool hideArchived = true, bool isSysAdmin = false);
    Task<PaginatedResponse<OrganizationResponseDto>> GetAllOrganizationsPaginated(long userId, PaginatedRequestDto paginatedRequestDto, bool hideArchived = true, bool isSysAdmin = false);
    Task<PaginatedResponse<OrganizationResponseDto>> GetAllOrganizationsForUserPaginated(
        long userId,
        PaginatedRequestDto paginatedRequestDto,
        bool hideArchived = true,
        bool isSysAdmin = false);
    Task<OrganizationResponseDto> GetOrganization(long organizationId, bool hideArchived = true);
    Task<OrganizationResponseDto> CreateOrganization(long currentUserId, CreateOrganizationRequestDto dto,
        bool isDefault = false);

    Task<OrganizationResponseDto> UpdateOrganization(long currentUserId, long organizationId,
        UpdateOrganizationRequestDto dto);

    Task<bool> ArchiveOrganization(long currentUserId, long organizationId);
    Task<bool> UnarchiveOrganization(long currentUserId, long organizationId);
    Task<bool> DeleteOrganization(long organizationId);
    Task<bool> AddUserToOrganization(long organizationId, long userId, bool isAdmin = false, bool allowServiceAccounts = false);
    Task<bool> SetOrganizationAdminStatus(long organizationId, long userId, bool isAdmin = false);
    Task<bool> RemoveUserFromOrganization(long organizationId, long userId);
    Task<bool> RemoveLogoFileAsync(long organizationId);
    Task<string> UploadOrganizationLogo(long organizationId, IFormFile logoFile);
    Task<(Stream Stream, string FullPath)?> GetOrganizationLogoStreamAsync(long organizationId);
}