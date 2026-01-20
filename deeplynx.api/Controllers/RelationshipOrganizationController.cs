using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing relationships at the organization level.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve relationship information.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/relationships")]
[Authorize]
[Tags("Organization - Relationship")]
public class RelationshipOrganizationController : ControllerBase
{
    private readonly ILogger<RelationshipOrganizationController> _logger;
    private readonly IRelationshipBusiness _relationshipBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RelationshipOrganizationController" /> class
    /// </summary>
    /// <param name="relationshipBusiness">The business logic interface for handling relationship operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public RelationshipOrganizationController(IRelationshipBusiness relationshipBusiness,
        ILogger<RelationshipOrganizationController> logger)
    {
        _relationshipBusiness = relationshipBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Relationships 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the relationship's project belongs</param>
    /// <param name="projectIds">(Optional)An array of project IDs within the organization to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived relationships from the result (Default true)</param>
    /// <returns>List of relationship response DTOs</returns>
    [HttpGet(Name = "api_get_all_relationships_organization")]
    [Auth("read", "relationship")]
    public async Task<ActionResult<IEnumerable<RelationshipResponseDto>>> GetAllRelationships(
        long organizationId,
        [FromQuery] long[]? projectIds,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var relationships =
                await _relationshipBusiness.GetAllRelationships(organizationId, projectIds, hideArchived);
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
    /// <param name="organizationId">The ID of the organization to which the relationship's project belongs</param>
    /// <param name="relationshipId">The ID of the relationship to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived relationships from the result (Default true)</param>
    /// <returns>Relationship response DTO</returns>
    [HttpGet("{relationshipId:long}", Name = "api_get_a_relationship_organization")]
    [Auth("read", "relationship")]
    public async Task<ActionResult<RelationshipResponseDto>> GetRelationship(
        long organizationId,
        long relationshipId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var relationship =
                await _relationshipBusiness.GetRelationship(organizationId, null, relationshipId, hideArchived);
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
    /// <param name="organizationId">The ID of the organization to which the relationship's project belongs</param>
    /// <param name="dto">The relationship request data transfer object containing relationship details</param>
    /// <returns>The created relationship</returns>
    [HttpPost(Name = "api_create_a_relationship_organization")]
    [Auth("write", "relationship")]
    public async Task<ActionResult<RelationshipResponseDto>> CreateRelationship(
        long organizationId,
        [FromBody] CreateRelationshipRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var created = await _relationshipBusiness.CreateRelationship(currentUserId, organizationId, null, dto);
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
    /// <param name="organizationId">The ID of the organization to which the relationship's project belongs</param>
    /// <param name="relationships">List of relationship request data transfer objects containing relationship details</param>
    /// <returns>The created relationships</returns>
    [HttpPost("bulk", Name = "api_create_many_relationships_organization")]
    [Auth("write", "relationship")]
    public async Task<ActionResult<List<RelationshipResponseDto>>> BulkCreateRelationships(
        long organizationId,
        [FromBody] List<CreateRelationshipRequestDto> relationships)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var created =
                await _relationshipBusiness.BulkCreateRelationships(currentUserId, organizationId, null,
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
    /// <param name="organizationId">The ID of the organization to which the relationship's project belongs</param>
    /// <param name="relationshipId">The ID of the relationship to update</param>
    /// <param name="dto">The relationship request data transfer object containing updated relationship details</param>
    /// <returns>The updated relationship</returns>
    [HttpPut("{relationshipId:long}", Name = "api_update_a_relationship_organization")]
    [Auth("write", "relationship")]
    public async Task<ActionResult<RelationshipResponseDto>> UpdateRelationship(
        long organizationId,
        long relationshipId,
        [FromBody] UpdateRelationshipRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var result =
                await _relationshipBusiness.UpdateRelationship(currentUserId, organizationId, null, relationshipId,
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
    /// <param name="organizationId">The ID of the organization to which the relationship's project belongs</param>
    /// <param name="relationshipId">The ID of the relationship to delete</param>
    /// <returns>A message stating the relationship was successfully deleted.</returns>
    [HttpDelete("{relationshipId:long}", Name = "api_delete_a_relationship_organization")]
    [Auth("write", "relationship")]
    public async Task<IActionResult> DeleteRelationship(
        long organizationId,
        long relationshipId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _relationshipBusiness.DeleteRelationship(currentUserId, organizationId, null, relationshipId);
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
    /// <param name="organizationId">The ID of the organization to which the relationship's project belongs</param>
    /// <param name="relationshipId">The ID of the relationship to archive or unarchive</param>
    /// <param name="archive">True to archive the relationship, false to unarchive it.</param>
    /// <returns>A message stating the relationship was successfully archived or unarchived.</returns>
    [HttpPatch("{relationshipId:long}", Name = "api_archive_relationship_organization")]
    [Auth("write", "relationship")]
    public async Task<IActionResult> ArchiveRelationship(
        long organizationId,
        long relationshipId,
        [FromQuery] bool archive)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                await _relationshipBusiness.ArchiveRelationship(currentUserId, organizationId, null,
                    relationshipId);
                return Ok(new { message = $"Archived relationship {relationshipId}" });
            }

            await _relationshipBusiness.UnarchiveRelationship(currentUserId, organizationId, null, relationshipId);
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