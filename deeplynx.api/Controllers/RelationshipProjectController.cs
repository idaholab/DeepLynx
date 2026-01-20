using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing relationships at a project level.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve relationship information.
/// </remarks>
[ApiController]
[Route("projects/{projectId:long}/relationships")]
[Authorize]
[Tags("Project - Relationship")]
public class RelationshipProjectController : ControllerBase
{
    private readonly ILogger<RelationshipProjectController> _logger;
    private readonly IRelationshipBusiness _relationshipBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RelationshipProjectController" /> class
    /// </summary>
    /// <param name="relationshipBusiness">The business logic interface for handling relationship operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public RelationshipProjectController(IRelationshipBusiness relationshipBusiness,
        ILogger<RelationshipProjectController> logger)
    {
        _relationshipBusiness = relationshipBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Relationships 
    /// </summary>
    /// <param name="projectId">The ID of the project whose relationships are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived relationships from the result (Default true)</param>
    /// <returns>A list of relationships for the given project.</returns>
    [HttpGet(Name = "api_get_all_relationships_project")]
    [Auth("read", "relationship")]
    public async Task<ActionResult<IEnumerable<RelationshipResponseDto>>> GetAllRelationships(
        long projectId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var relationships =
                await _relationshipBusiness.GetAllRelationships(organizationId, [projectId], hideArchived);
            return Ok(relationships);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing all relationships: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get a Relationship 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the relationship belongs</param>
    /// <param name="relationshipId">The ID of the relationship to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived relationships from the result (Default true)</param>
    /// <returns>The relationship associated with the given ID</returns>
    [HttpGet("{relationshipId:long}", Name = "api_get_a_relationship_project")]
    [Auth("read", "relationship")]
    public async Task<ActionResult<RelationshipResponseDto>> GetRelationship(
        long projectId,
        long relationshipId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var relationship =
                await _relationshipBusiness.GetRelationship(organizationId, projectId, relationshipId, hideArchived);
            return Ok(relationship);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving relationship {relationshipId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create a Relationship 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the relationship belongs</param>
    /// <param name="dto">The relationship request data transfer object containing relationship details</param>
    /// <returns>The created relationship</returns>
    [HttpPost(Name = "api_create_a_relationship_project")]
    [Auth("write", "relationship")]
    public async Task<ActionResult<RelationshipResponseDto>> CreateRelationship(
        long projectId,
        [FromBody] CreateRelationshipRequestDto dto)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var created = await _relationshipBusiness.CreateRelationship(currentUserId, organizationId, projectId, dto);
            return Ok(created);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating relationship: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Bulk Create Relationships 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the relationships belong</param>
    /// <param name="relationships">List of relationship request data transfer objects containing relationship details</param>
    /// <returns>The created relationships</returns>
    [HttpPost("bulk", Name = "api_create_many_relationships_project")]
    [Auth("write", "relationship")]
    public async Task<ActionResult<List<RelationshipResponseDto>>> BulkCreateRelationships(
        long projectId,
        [FromBody] List<CreateRelationshipRequestDto> relationships)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var created =
                await _relationshipBusiness.BulkCreateRelationships(currentUserId, organizationId, projectId,
                    relationships);
            return Ok(created);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating relationships: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Update a Relationship 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the relationship belongs</param>
    /// <param name="relationshipId">The ID of the relationship to update</param>
    /// <param name="dto">The relationship request data transfer object containing updated relationship details</param>
    /// <returns>The updated relationship</returns>
    [HttpPut("{relationshipId:long}", Name = "api_update_a_relationship_project")]
    [Auth("write", "relationship")]
    public async Task<ActionResult<RelationshipResponseDto>> UpdateRelationship(
        long projectId,
        long relationshipId,
        [FromBody] UpdateRelationshipRequestDto dto)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var result =
                await _relationshipBusiness.UpdateRelationship(currentUserId, organizationId, projectId, relationshipId,
                    dto);
            return Ok(result);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating relationship {relationshipId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete a Relationship 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the relationship belongs</param>
    /// <param name="relationshipId">The ID of the relationship to delete</param>
    /// <returns>A message stating the relationship was successfully deleted.</returns>
    [HttpDelete("{relationshipId:long}", Name = "api_delete_a_relationship_project")]
    [Auth("write", "relationship")]
    public async Task<IActionResult> DeleteRelationship(
        long projectId,
        long relationshipId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var organizationId = UserContextStorage.OrganizationId;
            await _relationshipBusiness.DeleteRelationship(currentUserId, organizationId, projectId, relationshipId);
            return Ok(new { message = $"Deleted relationship {relationshipId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting relationship {relationshipId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive a Relationship 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the relationship belongs</param>
    /// <param name="relationshipId">The ID of the relationship to archive or unarchive</param>
    /// <param name="archive">True to archive the relationship, false to unarchive it.</param>
    /// <returns>A message stating the relationship was successfully archived or unarchived.</returns>
    [HttpPatch("{relationshipId:long}", Name = "api_archive_relationship_project")]
    [Auth("write", "relationship")]
    public async Task<IActionResult> ArchiveRelationship(
        long projectId,
        long relationshipId,
        [FromQuery] bool archive)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                await _relationshipBusiness.ArchiveRelationship(currentUserId, organizationId, projectId,
                    relationshipId);
                return Ok(new { message = $"Archived relationship {relationshipId}" });
            }

            await _relationshipBusiness.UnarchiveRelationship(currentUserId, organizationId, projectId, relationshipId);
            return Ok(new { message = $"Unarchived relationship {relationshipId}" });
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} relationship {relationshipId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}