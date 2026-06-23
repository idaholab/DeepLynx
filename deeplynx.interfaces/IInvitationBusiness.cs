namespace deeplynx.interfaces;

public interface IInvitationBusiness
{
    Task<bool> InviteAndAddUserToHierarchy(long organizationId, long? projectId, long? groupId,
        long? roleId, long? userId, string? userEmail, bool callerIsAdmin = false);

    Task<bool> CreateAndAddServiceAccountToProject(long projectId, long roleId, string name,
        bool makeProjectAdmin = false);
}