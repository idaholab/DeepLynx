using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using Asp.Versioning;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for managing object storages.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve object storage information.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("organizations/{organizationId:long}/storages")]
[Authorize]
[ForbidServiceAccounts]
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "object_storage")]
    public async Task<ActionResult<IEnumerable<ObjectStorageResponseDto>>> GetAllObjectStorages(
        long organizationId,
        [FromQuery] bool hideArchived = true)
    {
            var objectStorages = await _objectStorageBusiness.GetAllObjectStorages(
                organizationId, null, hideArchived);

            return Ok(objectStorages);
    }



    /// <summary>
    ///     Get an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived object storages from the result (Default true)</param>
    /// <returns>The object storage associated with the given ID</returns>
    [HttpGet("{objectStorageId:long}", Name = "api_get_object_storage_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> GetObjectStorage(
        long organizationId,
        long objectStorageId,
        [FromQuery] bool hideArchived = true)
    {
            var objectStorage =
                await _objectStorageBusiness.GetObjectStorage(
                    organizationId, null, objectStorageId, hideArchived);
            return Ok(objectStorage);
    }



    /// <summary>
    ///     Create an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="dto">The data transfer object containing object storage details</param>
    /// <returns>The created object storage</returns>
    [HttpPost(Name = "api_create_object_storage_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> CreateObjectStorage(
        long organizationId,
        [FromBody] CreateObjectStorageRequestDto dto)
    {
            var currentUserId = UserContextStorage.UserId;
            var objectStorage = await _objectStorageBusiness.CreateObjectStorage(
                currentUserId, organizationId, null, dto);
            return Ok(objectStorage);
    }



    /// <summary>
    ///     Update an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to update</param>
    /// <param name="dto">The data transfer object containing updated object storage details</param>
    /// <returns>The updated object storage</returns>
    [HttpPut("{objectStorageId:long}", Name = "api_update_object_storage_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> UpdateObjectStorage(
        long organizationId,
        long objectStorageId,
        [FromBody] UpdateObjectStorageRequestDto dto)
    {
            var currentUserId = UserContextStorage.UserId;
            var objectStorage = await _objectStorageBusiness.UpdateObjectStorage(
                currentUserId, organizationId, null, objectStorageId, dto);
            return Ok(objectStorage);
    }



    /// <summary>
    ///     Delete an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to delete</param>
    /// <returns>True if the object storage was successfully deleted.</returns>
    [HttpDelete("{objectStorageId:long}", Name = "api_delete_object_storage_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "object_storage")]
    public async Task<ActionResult> DeleteObjectStorage(
        long organizationId,
        long objectStorageId)
    {
            var currentUserId = UserContextStorage.UserId;
            var response = await _objectStorageBusiness.DeleteObjectStorage(
                currentUserId, organizationId, null, objectStorageId);
            return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to archive or unarchive</param>
    /// <param name="archive">True to archive the object storage, false to unarchive it.</param>
    /// <returns>True if the object storage was successfully archived or unarchived.</returns>
    [HttpPatch("{objectStorageId:long}", Name = "api_archive_object_storage_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "object_storage")]
    public async Task<ActionResult> ArchiveObjectStorage(
        long organizationId,
        long objectStorageId,
        [FromQuery] bool archive)
    {
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                var responseA = await _objectStorageBusiness.ArchiveObjectStorage(
                    currentUserId, organizationId, null, objectStorageId);
                return Ok(responseA);
            }

            var responseB = await _objectStorageBusiness.UnarchiveObjectStorage(
                currentUserId, organizationId, null, objectStorageId);
            return Ok(responseB);
    }



    /// <summary>
    ///     Get Default Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <returns>The default object storage for the organization</returns>
    [HttpGet("default", Name = "api_get_default_object_storage_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> GetDefaultObjectStorage(
        long organizationId)
    {
            var defaultObjectStorage = await _objectStorageBusiness.GetDefaultObjectStorage(
                organizationId, null);
            return Ok(defaultObjectStorage);
    }



    /// <summary>
    ///     Set Default Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to set as default</param>
    /// <returns>The object storage that was set as default.</returns>
    [HttpPatch("{objectStorageId:long}/default", Name = "api_set_default_object_storage_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> SetDefaultObjectStorage(
        long organizationId,
        long objectStorageId)
    {
            var currentUserId = UserContextStorage.UserId;
            var response = await _objectStorageBusiness.SetDefaultObjectStorage(
                currentUserId, organizationId, null, objectStorageId);
            return Ok(response);
    }
}