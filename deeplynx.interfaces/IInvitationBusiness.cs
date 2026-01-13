namespace deeplynx.interfaces;

public interface IInvitationBusiness
{
    Task<bool> InviteAndAddUserToHierarchy(long organizationId, long? projectId, long? roleId, string userEmail, string? userName);
}