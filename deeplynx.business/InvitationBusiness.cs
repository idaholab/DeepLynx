using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class InvitationBusiness : IInvitationBusiness
{
    private readonly DeeplynxContext _context;
    private readonly INotificationBusiness _notificationBusiness;
    private readonly IOrganizationBusiness _organizationBusiness;
    private readonly IProjectBusiness _projectBusiness;
    private readonly IUserBusiness _userBusiness;

    public InvitationBusiness(
        DeeplynxContext context,
        INotificationBusiness notificationBusiness,
        IProjectBusiness projectBusiness,
        IOrganizationBusiness organizationBusiness,
        IUserBusiness userBusiness)
    {
        _context = context;
        _notificationBusiness = notificationBusiness;
        _projectBusiness = projectBusiness;
        _organizationBusiness = organizationBusiness;
        _userBusiness = userBusiness;
    }


    /// <summary>
    ///     Invites user and adds them to the organization and/or project. If the user exists,
    ///     it will just add them to the org/project with no role.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="roleId"></param>
    /// <param name="userEmail"></param>
    /// <param name="userName"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<bool> InviteAndAddUserToHierarchy(long organizationId, long? projectId, long? groupId,
        long? roleId, long? userId, string? userEmail)
    {

        var suppliedCount = (groupId.HasValue ? 1 : 0) +
                            (userId.HasValue ? 1 : 0) +
                            (!string.IsNullOrWhiteSpace(userEmail) ? 1 : 0);

        if (suppliedCount != 1)
            throw new ArgumentException("Exactly one of groupId, userId, or userEmail must be supplied.");

        if (projectId != null)
        {
            if (roleId == null) throw new ArgumentException("roleId is required for user/group.");
        }
        else
        {
            if (roleId != null)
                throw new ArgumentException(
                    "Roles do not exist for organization users, please specify a project the role will apply to.");

            if (userEmail == null || userId != null || groupId != null)
                throw new ArgumentException(
                    "Only userEmail is allowed for organization invitations. userId and groupId are not permitted.");
        }

        // Handle existing user by userId - no transaction, best-effort email
        if (userId != null)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) throw new ArgumentException($"User with id '{userId}' not found.");

            await AddUserToHierarchyWithoutEmail(organizationId, projectId, roleId, user);
            
            // Best-effort email - don't fail if email doesn't send
            await _notificationBusiness.SendEmail(user.Email, user.Name);
            
            return true;
        }

        // Handle user by email
        if (userEmail != null)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == userEmail.ToLower());

            if (user != null)
            {
                // Existing user - no transaction, best-effort email
                await AddUserToHierarchyWithoutEmail(organizationId, projectId, roleId, user);
                
                // Best-effort email - don't fail if email doesn't send
                await _notificationBusiness.SendEmail(userEmail, user.Name);
                
                return true;
            }
            // New user - use transaction and rollback if email fails
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var createUserDto = new CreateUserRequestDto
                {
                    Name = userEmail,
                    Email = userEmail
                };
                var createdUserResponseDto = await _userBusiness.CreateUser(createUserDto);

                await _organizationBusiness.AddUserToOrganization(organizationId, createdUserResponseDto.Id);

                if (projectId != null)
                    await _projectBusiness.AddMemberToProject(projectId.Value, roleId, createdUserResponseDto.Id, null);
            
                user = await _context.Users.FirstOrDefaultAsync(u => u.Id == createdUserResponseDto.Id);

                // Send email - if this fails, rollback everything
                var emailResult = await _notificationBusiness.SendEmail(userEmail, user?.Name);
                if (!emailResult)
                {
                    await transaction.RollbackAsync();
                    throw new Exception($"Failed to send invitation email to {userEmail}. User was not created.");
                }

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // Handle group - NO transaction, best-effort email delivery
        if (groupId != null)
        {
            var group = await _context.Groups.Include(g => g.Users).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) throw new ArgumentException($"Group with id '{groupId}' not found.");

            var groupInProject = await _context.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == projectId && pm.GroupId == group.Id);

            if (!groupInProject)
            {
                await _projectBusiness.AddMemberToProject(projectId.Value, roleId, null, groupId);
            
                // Best-effort email sending - don't fail if some emails don't send
                foreach (var user in group.Users)
                {
                    await _notificationBusiness.SendEmail(user.Email, user.Name);
                }
            }
        }

        return true;
    }
    
    private async Task AddUserToHierarchyWithoutEmail(long organizationId, long? projectId, long? roleId, User user)
    {
        var userInOrg = await _context.OrganizationUsers
            .AnyAsync(ou => ou.OrganizationId == organizationId && ou.UserId == user.Id);

        if (!userInOrg)
            await _organizationBusiness.AddUserToOrganization(organizationId, user.Id);

        if (projectId != null)
        {
            var userInProject = await _context.ProjectMembers
                .Include(pm => pm.Group)
                .AnyAsync(pm => pm.ProjectId == projectId &&
                                (pm.UserId == user.Id || pm.Group.Users.Any(u => u.Id == user.Id)));

            if (!userInProject)
                await _projectBusiness.AddMemberToProject(projectId.Value, roleId, user.Id, null);
        }
    }
}