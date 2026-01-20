using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class InvitationBusiness: IInvitationBusiness
{
    private readonly DeeplynxContext _context;
    private readonly INotificationBusiness _notificationBusiness;
    private readonly IProjectBusiness _projectBusiness;
    private readonly IOrganizationBusiness _organizationBusiness;
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
    /// Invites user and adds them to the organization and/or project. If the user exists,
    /// it will just add them to the org/project with no role. 
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="roleId"></param>
    /// <param name="userEmail"></param>
    /// <param name="userName"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<bool> InviteAndAddUserToHierarchy(long organizationId, long? projectId, long? roleId, string userEmail, string? userName)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
    
        if (user != null)
        {
            // Check if user is already in organization
            var userInOrg = await _context.OrganizationUsers
                .AnyAsync(ou => ou.OrganizationId == organizationId && ou.UserId == user.Id);
        
            if (!userInOrg)
            {
                await _organizationBusiness.AddUserToOrganization(organizationId, user.Id);
            }

            if (projectId.HasValue)
            {
                var userInProject = await _context.ProjectMembers
                    .Include(pm => pm.Group)
                    .AnyAsync(pm => pm.ProjectId == projectId.Value && 
                                    (pm.UserId == user.Id || pm.Group.Users.Any(u => u.Id == user.Id)));
            
                if (!userInProject)
                {
                    await _projectBusiness.AddMemberToProject(projectId.Value, roleId, user.Id, null);
                }
            }
        
            return true;
        }
        
        var emailResult = await _notificationBusiness.SendEmail(userEmail, userName);
        if (!emailResult)
        {
            throw new Exception($"Email not sent, check for valid email address: {userEmail}");
        }

        var createUserDto = new CreateUserRequestDto
        {
            Name = userEmail,
            Email = userEmail,
        };
    
        var createdUserResponseDto = await _userBusiness.CreateUser(createUserDto);
    
        await _organizationBusiness.AddUserToOrganization(organizationId, createdUserResponseDto.Id);

        if (projectId.HasValue)
        {
            await _projectBusiness.AddMemberToProject(projectId.Value, roleId, createdUserResponseDto.Id, null);
        }
    
        return true;
    }
}