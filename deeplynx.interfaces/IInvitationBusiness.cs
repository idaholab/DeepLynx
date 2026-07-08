namespace deeplynx.interfaces;

public interface IInvitationBusiness
{
    Task<bool> InviteAndAddUserToHierarchy(long organizationId, long? projectId, long? groupId,
        long? roleId, long? userId, string? userEmail);

    Task<bool> CreateAndAddServiceAccountToProject(long organizationId, long projectId, string name, long? roleId,
        bool makeProjectAdmin = false);
}