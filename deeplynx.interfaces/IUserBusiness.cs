using deeplynx.models;

namespace deeplynx.interfaces;

public interface IUserBusiness
{
    [Obsolete("V1-only. Used by deprecated v1 user endpoints. Superseded by GetAllUsersPaginated. " +
                "Remove once v1 user endpoints are sunset.", error: false)]
    Task<IEnumerable<UserResponseDto>> GetAllUsers(long? projectId, long? organizationId, bool includeArchived = false, bool includeServiceAccounts = false, bool includeTestAccounts = false);
    Task<PaginatedResponse<UserResponseDto>> GetAllUsersPaginated(
        PaginatedRequestDto dto, long? projectId, long? organizationId,
        bool includeArchived = false, bool includeServiceAccounts = false,
        bool includeTestAccounts = false);
    Task<UserActivityCountsDto> GetActiveUserCounts(long? projectId, long? organizationId, bool includeServiceAccounts = false);
    Task<UserActivityUsersDto> GetActiveUsers(long? projectId, long? organizationId, bool includeServiceAccounts = false);
    Task<UserResponseDto> GetUser(long userId);
    Task<UserAdminInfoDto> GetUserAdminInfo(long userId, long? organizationId = null, long? projectId = null);
    Task<UserResponseDto> GetLocalDevUser();
    Task<UserResponseDto> CreateUser(CreateUserRequestDto dto);
    Task<UserResponseDto> CreateTestAccount(string name);
    Task<UserResponseDto> UpdateUser(long userId, UpdateUserRequestDto dto);
    Task<bool> DeleteUser(long userId);
    Task<bool> ArchiveUser(long userId);
    Task<DataOverviewDto> GetUserOverview(long userId);
    Task<bool> UnarchiveUser(long userId);
    Task<bool> SetSysAdmin(long authorizerId, long candidateId, bool? isAdmin = true);
    Task<UserResponseDto> GetUserBySsoId(string ssoId);
    Task<UserResponseDto> GetUserByEmail(string email);
}
