using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

[ApiController]

[ApiVersion(2)]
[Route("organizations/{organizationId:long}/projects")]
[Authorize]
public class ProjectController : ControllerBase
{
    private readonly IInvitationBusiness _invitationBusiness;
    private readonly ILogger<ProjectController> _logger;
    private readonly IProjectBusiness _projectBusiness;

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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "project")]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetAllProjects(
        long organizationId,
        [FromQuery] bool hideArchived = true)
    {
        // get user ID from the middleware context
        var currentUserId = UserContextStorage.UserId;
        var projects = await _projectBusiness
            .GetAllProjects(currentUserId, organizationId, hideArchived);
        return Ok(projects);
    }



    /// <summary>
    ///     Get all user projects
    /// </summary>
    /// <param name="organizationId">ID of the organization to list projects from</param>
    /// <param name="userId">ID of the user whose projects to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived projects from the result (Default true)</param>
    /// <returns>A list of projects</returns>
    [HttpGet("GetProjectsByUser", Name = "api_get_all_projects_by_user")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "project")]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetAllProjectsByUser(
        long organizationId,
        [FromQuery] long userId,
        [FromQuery] bool hideArchived = true)
    {
        var projects = await _projectBusiness
            .GetAllProjects(userId, organizationId, hideArchived);
        return Ok(projects);
    }



    /// <summary>
    ///     Get a Project
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID by which to retrieve the project</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived projects from the result (Default true)</param>
    /// <returns>The given project to return</returns>
    [HttpGet("{projectId:long}", Name = "api_get_a_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "project")]
    public async Task<ActionResult<ProjectResponseDto>> GetProject(
        long organizationId,
        long projectId,
        [FromQuery] bool hideArchived = true)
    {
        var project = await _projectBusiness.GetProject(organizationId, projectId, hideArchived);
        return Ok(project);
    }



    /// <summary>
    ///     Create a Project
    /// </summary>
    /// <param name="organizationId">The organization to which the project will belong</param>
    /// <param name="dto">A data transfer object with details on the new project to be created.</param>
    /// <returns>The new project that was just created.</returns>
    [HttpPost(Name = "api_create_a_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ForbidServiceAccounts]
    [OrgMember]
    public async Task<ActionResult<ProjectResponseDto>> CreateProject(
        long organizationId,
        [FromBody] CreateProjectRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var project = await _projectBusiness.CreateProject(currentUserId, organizationId, dto);
        return Ok(project);
    }



    /// <summary>
    ///     Update a Project
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to update</param>
    /// <param name="dto">A data transfer object with details on the project to be updated.</param>
    /// <returns>The project that was just updated.</returns>
    [HttpPut("{projectId:long}", Name = "api_update_a_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ProjectAdmin]
    public async Task<ActionResult<ProjectResponseDto>> UpdateProject(
        long organizationId,
        long projectId,
        [FromBody] UpdateProjectRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var project = await _projectBusiness.UpdateProject(currentUserId, organizationId, projectId, dto);
        return Ok(project);
    }



    /// <summary>
    ///     Delete a Project
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to delete.</param>
    /// <returns>Boolean true on successful deletion.</returns>
    [HttpDelete("{projectId:long}", Name = "api_delete_a_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ProjectAdmin]
    public async Task<IActionResult> DeleteProject(long organizationId, long projectId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _projectBusiness.DeleteProject(currentUserId, organizationId, projectId);
        return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive a Project
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to archive or unarchive</param>
    /// <param name="archive">True to archive the project, false to unarchive it.</param>
    /// <returns> True if the project was successfully archived or unarchived.</returns>
    [HttpPatch("{projectId:long}", Name = "api_archive_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ProjectAdmin(includeArchived: true)]
    public async Task<IActionResult> ArchiveProject(
        long organizationId,
        long projectId,
        [FromQuery] bool archive)
    {
        var userId = UserContextStorage.UserId;
        if (archive)
        {
            var responseA = await _projectBusiness.ArchiveProject(userId, organizationId, projectId);
            return Ok(responseA);
        }

        var response = await _projectBusiness.UnarchiveProject(userId, organizationId, projectId);
        return Ok(response);
    }



    /// <summary>
    ///     Get Project Stats
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to display stats about.</param>
    /// <returns>Project stats</returns>
    [HttpGet("{projectId:long}/stats", Name = "api_get_a_projects_stats")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ProjectAdmin]
    public async Task<ActionResult<ProjectStatResponseDto>> ProjectStats(long organizationId, long projectId)
    {
        var stats = await _projectBusiness.GetProjectStats(organizationId, projectId);
        return Ok(stats);
    }



    /// <summary>
    ///     Get Project Members
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">(Optional)ID of the project</param>
    /// <returns>A list of groups and users in the project, along with their roles</returns>
    [HttpGet("{projectId:long}/members", Name = "api_get_project_members")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "project")]
    [Auth("read", "user")]
    public async Task<ActionResult<IEnumerable<ProjectMemberResponseDto>>> GetProjectMembers(long organizationId, long projectId)
    {
        var members = await _projectBusiness.GetProjectMembers(projectId);
        return Ok(members);
    }



    /// <summary>
    ///     Add User or Group to Project
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of project</param>
    /// <param name="roleId">(Optional) ID of member role</param>
    /// <param name="userId">ID of user if user is member</param>
    /// <param name="groupId">ID of group if group is member</param>
    /// <param name="isProjectAdmin">Whether the member is a project admin. Defaults to false</param>
    /// <returns>True if the member was successfully added to the project.</returns>
    [HttpPost("{projectId:long}/members", Name = "api_add_member_to_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ForbidServiceAccounts]
    [ProjectAdmin]
    public async Task<ActionResult> AddMemberToProject(
        long organizationId, long projectId,
        [FromQuery] long? roleId, [FromQuery] long? userId, [FromQuery] long? groupId,
        [FromQuery] bool isProjectAdmin = false)
    {
        var response = await _projectBusiness.AddMemberToProject(projectId, roleId, userId, groupId, isProjectAdmin);
        return Ok(response);
    }



    /// <summary>
    ///     Update Member Role in Project
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of project</param>
    /// <param name="roleId">ID of role</param>
    /// <param name="userId">ID of user if user is member</param>
    /// <param name="groupId">ID of group if group is member</param>
    /// <param name="isProjectAdmin">(optional) project admin status to set; left unchanged when omitted</param>
    /// <returns>True if the member was successfully updated</returns>
    [HttpPut("{projectId:long}/members", Name = "api_update_project_member_role")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ProjectAdmin]
    public async Task<ActionResult> UpdateProjectMemberRole(
        long organizationId, long projectId,
        [FromQuery] long roleId, [FromQuery] long? userId, [FromQuery] long? groupId,
        [FromQuery] bool? isProjectAdmin = null)
    {
        var response = await _projectBusiness.UpdateProjectMemberRole(projectId, roleId, userId, groupId, isProjectAdmin);
        return Ok(response);
    }



    /// <summary>
    ///     Set Project Admin Status for a Project Member
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of project</param>
    /// <param name="userId">ID of user if user is member</param>
    /// <param name="groupId">ID of group if group is member</param>
    /// <param name="isAdmin">Project admin status to set the member to</param>
    /// <returns>True if the admin status was successfully updated</returns>
    [HttpPut("{projectId:long}/admin", Name = "api_update_project_member_admin_status")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ProjectAdmin]
    public async Task<ActionResult> SetProjectAdminStatus(
        long organizationId, long projectId,
        [FromQuery] long? userId, [FromQuery] long? groupId, [FromQuery] bool isAdmin)
    {
        var response = await _projectBusiness.SetProjectAdminStatus(projectId, userId, groupId, isAdmin);
        return Ok(response);
    }



    /// <summary>
    ///     Remove User or Group from Project
    /// </summary>
    /// <param name="projectId">ID of the project</param>
    /// <param name="userId">ID of the user if user is member</param>
    /// <param name="groupId">ID of the group if group is member</param>
    /// <returns>True if the member was successfully removed from the project</returns>
    [HttpDelete("{projectId:long}/members", Name = "api_remove_member_from_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ProjectAdmin]
    public async Task<ActionResult> RemoveMemberFromProject(
        long projectId,
        [FromQuery] long? userId,
        [FromQuery] long? groupId)
    {
        var currentUserId = UserContextStorage.UserId;
        var result = await _projectBusiness.RemoveMemberFromProject(projectId, userId, groupId, currentUserId);
        return Ok(result);
    }



    /// <summary>
    ///     Invite/Add User to Project
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="userEmail"></param>
    /// <param name="userId"></param>
    /// <param name="groupId"></param>
    /// <param name="roleId"></param>
    /// <returns>True if the user was successfully invited</returns>
    [HttpPost("{projectId:long}/invite", Name = "api_invite_user_to_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ForbidServiceAccounts]
    [ProjectAdmin]
    public async Task<ActionResult> InviteUserToProject(
        long organizationId,
        long projectId,
        [FromQuery] string? userEmail,
        [FromQuery] long? userId,
        [FromQuery] long? groupId,
        [FromQuery] long? roleId)
    {
        var response = await _invitationBusiness.InviteAndAddUserToHierarchy(organizationId, projectId, groupId, roleId, userId,
                 userEmail);
        return Ok(response);
    }



    /// <summary>
    /// Create and add service account to project
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="roleId"></param>
    /// <param name="name"></param>
    /// <param name="makeProjectAdmin"></param>
    /// <returns>True if the service account was successfully created and added to the project</returns>
    [HttpPost("{projectId:long}/invite/serviceAccount", Name = "api_add_service_account")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Tags("Service Accounts")]
    [ForbidServiceAccounts]
    [ProjectAdmin]
    public async Task<ActionResult> CreateAndAddServiceAccountToProject(
        long organizationId,
        long projectId,
        [FromQuery] string name,
        [FromQuery] long? roleId,
        [FromQuery] bool makeProjectAdmin = false)
    {

        var response = await _invitationBusiness.CreateAndAddServiceAccountToProject(organizationId, projectId, name, roleId, makeProjectAdmin);
        return Ok(response);
    }

    /// <summary>
    ///     Upload a Project Logo
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to which the file belongs</param>
    /// <param name="file">The file to upload</param>
    /// <returns>The logo URI.</returns>
    [HttpPost("{projectId}/logo", Name = "api_upload_project_logo")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ProjectAdmin]
    [Sensitivity("upload file")]
    public async Task<IActionResult> UploadProjectLogo(
        long organizationId,
        long projectId,
        long? objectStorageId,
        IFormFile file)
    {
        try
        {
            var logoUri = await _projectBusiness.UploadProjectLogo(organizationId, projectId, objectStorageId, file);
            return Ok(logoUri);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to upload project logo for project {projectId}: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    /// <summary>
    ///     Get a Project Logo
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to which the file belongs</param>
    /// <returns>File stream of the logo bytes</returns>
    [HttpGet("{projectId}/logo/image", Name = "api_get_project_image")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<IActionResult> GetProjectLogoImage(
        long organizationId,
        long projectId,
        long? objectStorageId)
    {
        try
        {
            var result = await _projectBusiness.GetProjectLogoStreamAsync(organizationId, projectId, objectStorageId);
            if (result == null)
                return NotFound();

            var (logoStream, fullPath) = result.Value;
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fullPath, out var contentType))
                contentType = "application/octet-stream";

            return File(logoStream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving logo image for project {projectId}");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    ///     Delete a Project Logo
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to which the file belongs</param>
    /// <returns>True if the file was successfully deleted.</returns>
    [HttpDelete("{projectId}/logo/{fileName}", Name = "api_delete_project_logo")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ProjectAdmin]
    [Sensitivity("delete file")]
    public async Task<IActionResult> RemoveProjectLogo(
        long organizationId,
        long projectId,
        long? objectStorageId)
    {
        try
        {
            var success = await _projectBusiness.RemoveLogoFileAsync(organizationId, projectId, objectStorageId);
            return Ok(success);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to remove active logo file for project {projectId}: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
}
