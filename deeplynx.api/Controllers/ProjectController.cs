using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

[ApiController]
[Route("organizations/{organizationId:long}/projects")]
[Authorize]
public class ProjectController : ControllerBase
{
    private readonly ILogger<ProjectController> _logger;
    private readonly IProjectBusiness _projectBusiness;
    private readonly IInvitationBusiness _invitationBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProjectController" /> class
    /// </summary>
    /// <param name="projectBusiness">The business logic interface for handling project operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public ProjectController(
        IProjectBusiness projectBusiness,
        IInvitationBusiness invitationBusiness,
        ILogger<ProjectController> logger)
    {
        _projectBusiness = projectBusiness;
        _invitationBusiness = invitationBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get all projects
    /// </summary>
    /// <param name="organizationId">ID of the organization to list projects from</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived projects from the result (Default true)</param>
    /// <returns>A list of projects</returns>
    [HttpGet(Name = "api_get_all_projects")]
    [Auth("read", "project")]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetAllProjects(
        long organizationId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            // get user ID from the middleware context
            var currentUserId = UserContextStorage.UserId;
            var projects = await _projectBusiness
                .GetAllProjects(currentUserId, organizationId, hideArchived);
            return Ok(projects);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing projects: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    ///     Get all user projects
    /// </summary>
    /// <param name="organizationId">ID of the organization to list projects from</param>
    /// <param name="userId">ID of the user whose projects to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived projects from the result (Default true)</param>
    /// <returns>A list of projects</returns>
    [HttpGet("GetProjectsByUser", Name = "api_get_all_projects_by_user")]
    [Auth("read", "project")]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetAllProjectsByUser(
        long organizationId,
        [FromQuery] long userId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var projects = await _projectBusiness
                .GetAllProjects(userId, organizationId, hideArchived);
            return Ok(projects);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing projects: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get a Project
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID by which to retrieve the project</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived projects from the result (Default true)</param>
    /// <returns>The given project to return</returns>
    [HttpGet("{projectId:long}", Name = "api_get_a_project")]
    [Auth("read", "project")]
    public async Task<ActionResult<ProjectResponseDto>> GetProject(
        long organizationId,
        long projectId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var project = await _projectBusiness.GetProject(organizationId, projectId, hideArchived);
            return Ok(project);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving project {projectId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create a Project
    /// </summary>
    /// <param name="organizationId">The organization to which the project will belong</param>
    /// <param name="dto">A data transfer object with details on the new project to be created.</param>
    /// <returns>The new project which was just created.</returns>
    [HttpPost(Name = "api_create_a_project")]
    [Auth("write", "project")]
    public async Task<ActionResult<ProjectResponseDto>> CreateProject(
        long organizationId,
        [FromBody] CreateProjectRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var project = await _projectBusiness.CreateProject(currentUserId, organizationId, dto);
            return Ok(project);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating project: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Update a Project
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to update</param>
    /// <param name="dto">A data transfer object with details on the project to be updated.</param>
    /// <returns>The project which was just updated.</returns>
    [HttpPut("{projectId:long}", Name = "api_update_a_project")]
    [Auth("write", "project")]
    public async Task<ActionResult<ProjectResponseDto>> UpdateProject(
        long organizationId,
        long projectId,
        [FromBody] UpdateProjectRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var project = await _projectBusiness.UpdateProject(currentUserId, organizationId, projectId, dto);
            return Ok(project);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating project {projectId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete a Project
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to delete.</param>
    /// <returns>Boolean true on successful deletion.</returns>
    [HttpDelete("{projectId:long}", Name = "api_delete_a_project")]
    [Auth("write", "project")]
    public async Task<IActionResult> DeleteProject(long organizationId, long projectId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _projectBusiness.DeleteProject(currentUserId, organizationId, projectId);
            return Ok(new { message = $"Deleted project {projectId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting project {projectId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive a Project
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to archive or unarchive</param>
    /// <param name="archive">True to archive the project, false to unarchive it.</param>
    /// <returns>A message stating the project was successfully archived or unarchived.</returns>
    [HttpPatch("{projectId:long}", Name = "api_archive_project")]
    [Auth("write", "project", includeArchived: true)]
    public async Task<IActionResult> ArchiveProject(
        long organizationId,
        long projectId,
        [FromQuery] bool archive)
    {
        try
        {
            var userId = UserContextStorage.UserId;
            if (archive)
            {
                await _projectBusiness.ArchiveProject(userId, organizationId, projectId);
                return Ok(new { message = $"Archived project {projectId}" });
            }

            await _projectBusiness.UnarchiveProject(userId, organizationId, projectId);
            return Ok(new { message = $"Unarchived project {projectId}" });
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} project {projectId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Project Stats
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to display stats about.</param>
    /// <returns>Project stats</returns>
    [HttpGet("{projectId:long}/stats", Name = "api_get_a_projects_stats")]
    [Auth("read", "project")]
    public async Task<ActionResult<ProjectStatResponseDto>> ProjectStats(long organizationId, long projectId)
    {
        try
        {
            var stats = await _projectBusiness.GetProjectStats(organizationId, projectId);
            return Ok(stats);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving project {projectId} stats: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Project Members
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">(Optional)ID of the project</param>
    /// <returns>A list of groups and users in the project, along with their roles</returns>
    [HttpGet("{projectId:long}/members", Name = "api_get_project_members")]
    [Auth("read", "project")]
    public async Task<ActionResult<ProjectMemberResponseDto>> GetProjectMembers(long organizationId, long projectId)
    {
        try
        {
            var members = await _projectBusiness.GetProjectMembers(projectId);
            return Ok(members);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing project members for project {projectId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Add User or Group to Project
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of project</param>
    /// <param name="roleId">(Optional) ID of member role</param>
    /// <param name="userId">ID of user if user is member</param>
    /// <param name="groupId">ID of group if group is member</param>
    /// <returns></returns>
    [HttpPost("{projectId:long}/members", Name = "api_add_member_to_project")]
    [Auth("write", "project")]
    public async Task<ActionResult> AddMemberToProject(
        long organizationId, long projectId,
        [FromQuery] long? roleId, [FromQuery] long? userId, [FromQuery] long? groupId)
    {
        try
        {
            await _projectBusiness.AddMemberToProject(projectId, roleId, userId, groupId);
            return Ok(new { message = $"Added member {userId ?? groupId} to project {projectId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while adding member {userId ?? groupId} to project {projectId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Update Member Role in Project
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of project</param>
    /// <param name="roleId">ID of role</param>
    /// <param name="userId">ID of user if user is member</param>
    /// <param name="groupId">ID of group if group is member</param>
    /// <returns></returns>
    [HttpPut("{projectId:long}/members", Name = "api_update_project_member_role")]
    [Auth("write", "project")]
    public async Task<ActionResult> UpdateProjectMemberRole(
        long organizationId, long projectId,
        [FromQuery] long roleId, [FromQuery] long? userId, [FromQuery] long? groupId)
    {
        try
        {
            await _projectBusiness.UpdateProjectMemberRole(projectId, roleId, userId, groupId);
            return Ok(new { message = $"Updated member role in project {projectId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating member role in project {projectId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Remove User or Group from Project
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project</param>
    /// <param name="userId">ID of the user if user is member</param>
    /// <param name="groupId">ID of the group if group is member</param>
    /// <returns></returns>
    [HttpDelete("{projectId:long}/members", Name = "api_remove_member_from_project")]
    [Auth("write", "project")]
    public async Task<ActionResult> RemoveMemberFromProject(
        long organizationId,
        long projectId,
        [FromQuery] long? userId,
        [FromQuery] long? groupId)
    {
        try
        {
            await _projectBusiness.RemoveMemberFromProject(projectId, userId, groupId);
            return Ok(new { message = $"Removed member from project {projectId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while removing member from project {projectId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    /// Invite/Add User to Project
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="userEmail"></param>
    /// <param name="userName"></param>
    /// <param name="roleId"></param>
    /// <returns></returns>
    [HttpPost("{projectId:long}/invite", Name = "api_invite_user_to_project")]
    [Auth("write", "user")]
    public async Task<ActionResult> InviteUserToProject(
        long organizationId,
        long projectId,
        [FromQuery] string userEmail,
        [FromQuery] string? userName,
        [FromQuery] long? roleId)
    {
        try
        {
            await _invitationBusiness.InviteAndAddUserToHierarchy(organizationId,projectId, roleId, userEmail, userName);
            return Ok(new { message = $"Invited and added inactive user with email {userEmail} to organization {organizationId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while adding user with email {userEmail} to organization {organizationId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}