using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing object storages.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve object storage information.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/storages")]
[Authorize]
[Tags("Organization - Object Storage")]
public class ObjectStorageOrganizationController : ControllerBase
{
    private readonly ILogger<ObjectStorageProjectController> _logger;
    private readonly IObjectStorageBusiness _objectStorageBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ObjectStorageOrganizationController" /> class
    /// </summary>
    /// <param name="objectStorageBusiness">The business logic interface for handling object storage operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public ObjectStorageOrganizationController(
        IObjectStorageBusiness objectStorageBusiness,
        ILogger<ObjectStorageProjectController> logger)
    {
        _objectStorageBusiness = objectStorageBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Object Storages 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived object storages from the result (Default true)</param>
    /// <returns>A list of object storages for the given organization.</returns>
    [HttpGet(Name = "api_get_all_object_storages_organization")]
    [Auth("read", "object_storage")]
    public async Task<ActionResult<IEnumerable<ObjectStorageResponseDto>>> GetAllObjectStorages(
        long organizationId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var objectStorages = await _objectStorageBusiness.GetAllObjectStorages(
                organizationId, null, hideArchived);

            return Ok(objectStorages);
        }
        catch (Exception ex)
        {
            var message = $"An error occurred while listing all object storages: {ex}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived object storages from the result (Default true)</param>
    /// <returns>The object storage associated with the given ID</returns>
    [HttpGet("{objectStorageId:long}", Name = "api_get_object_storage_organization")]
    [Auth("read", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> GetObjectStorage(
        long organizationId,
        long objectStorageId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var objectStorage =
                await _objectStorageBusiness.GetObjectStorage(
                    organizationId, null, objectStorageId, hideArchived);
            return Ok(objectStorage);
        }
        catch (Exception ex)
        {
            var message = $"An error occurred while retrieving object storage {objectStorageId}: {ex}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="dto">The data transfer object containing object storage details</param>
    /// <returns>The created object storage</returns>
    [HttpPost(Name = "api_create_object_storage_organization")]
    [Auth("write", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> CreateObjectStorage(
        long organizationId,
        [FromBody] CreateObjectStorageRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var objectStorage = await _objectStorageBusiness.CreateObjectStorage(
                currentUserId, organizationId, null, dto);
            return Ok(objectStorage);
        }
        catch (Exception ex)
        {
            var message = $"An error occurred while creating object storage: {ex}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Update an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to update</param>
    /// <param name="dto">The data transfer object containing updated object storage details</param>
    /// <returns>The updated object storage</returns>
    [HttpPut("{objectStorageId:long}", Name = "api_update_object_storage_organization")]
    [Auth("write", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> UpdateObjectStorage(
        long organizationId,
        long objectStorageId,
        [FromBody] UpdateObjectStorageRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var objectStorage = await _objectStorageBusiness.UpdateObjectStorage(
                currentUserId, organizationId, null, objectStorageId, dto);
            return Ok(objectStorage);
        }
        catch (Exception ex)
        {
            var message = $"An error occurred while updating object storage {objectStorageId}: {ex}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to delete</param>
    /// <returns>A message stating the object storage was successfully deleted.</returns>
    [HttpDelete("{objectStorageId:long}", Name = "api_delete_object_storage_organization")]
    [Auth("write", "object_storage")]
    public async Task<ActionResult> DeleteObjectStorage(
        long organizationId,
        long objectStorageId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _objectStorageBusiness.DeleteObjectStorage(
                currentUserId, organizationId, null, objectStorageId);
            return Ok(new { message = $"Deleted object storage {objectStorageId}" });
        }
        catch (Exception ex)
        {
            var message = $"An error occurred while deleting object storage {objectStorageId}: {ex}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to archive or unarchive</param>
    /// <param name="archive">True to archive the object storage, false to unarchive it.</param>
    /// <returns>A message stating the object storage was successfully archived or unarchived.</returns>
    [HttpPatch("{objectStorageId:long}", Name = "api_archive_object_storage_organization")]
    [Auth("write", "object_storage")]
    public async Task<ActionResult> ArchiveObjectStorage(
        long organizationId,
        long objectStorageId,
        [FromQuery] bool archive)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                await _objectStorageBusiness.ArchiveObjectStorage(
                    currentUserId, organizationId, null, objectStorageId);
                return Ok(new { message = $"Archived object storage {objectStorageId}" });
            }

            await _objectStorageBusiness.UnarchiveObjectStorage(
                currentUserId, organizationId, null, objectStorageId);
            return Ok(new { message = $"Unarchived object storage {objectStorageId}" });
        }
        catch (Exception ex)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} object storage {objectStorageId}: {ex}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Default Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <returns>The default object storage for the organization</returns>
    [HttpGet("default", Name = "api_get_default_object_storage_organization")]
    [Auth("read", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> GetDefaultObjectStorage(
        long organizationId)
    {
        try
        {
            var defaultObjectStorage = await _objectStorageBusiness.GetDefaultObjectStorage(
                organizationId, null);
            return Ok(defaultObjectStorage);
        }
        catch (Exception ex)
        {
            var message =
                $"An error occurred while retrieving default object storage for organization {organizationId}: {ex}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Set Default Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to set as default</param>
    /// <returns>The updated object storage</returns>
    [HttpPatch("{objectStorageId:long}/default", Name = "api_set_default_object_storage_organization")]
    [Auth("write", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> SetDefaultObjectStorage(
        long organizationId,
        long objectStorageId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _objectStorageBusiness.SetDefaultObjectStorage(
                currentUserId, organizationId, null, objectStorageId);
            return Ok(new { message = $"Set object storage {objectStorageId} as default" });
        }
        catch (Exception ex)
        {
            var message = $"An error occurred while setting default object storage {objectStorageId}: {ex}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}