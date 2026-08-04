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
[Route("organizations/{organizationId:long}/projects/{projectId:long}/storages")]
[Authorize]
[Tags("Project - Object Storage")]
public class ObjectStorageProjectController : ControllerBase
{
    private readonly ILogger<ObjectStorageProjectController> _logger;
    private readonly IObjectStorageBusiness _objectStorageBusiness;
    private readonly IProjectBusiness _projectBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ObjectStorageProjectController" /> class
    /// </summary>
    /// <param name="objectStorageBusiness">The business logic interface for handling object storage operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    /// <param name="projectBusiness">The business logic interface for handling project operations.</param>
    public ObjectStorageProjectController(
        IObjectStorageBusiness objectStorageBusiness,
        ILogger<ObjectStorageProjectController> logger,
        IProjectBusiness projectBusiness)
    {
        _objectStorageBusiness = objectStorageBusiness;
        _logger = logger;
        _projectBusiness = projectBusiness;
    }



    /// <summary>
    ///     Get All Object Storages 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose object storages are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived object storages from the result (Default true)</param>
    /// <returns>A list of object storages for the given project.</returns>
    [HttpGet(Name = "api_get_all_object_storages_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "object_storage")]
    public async Task<ActionResult<IEnumerable<ObjectStorageResponseDto>>> GetAllObjectStorages(
        long organizationId,
        long projectId,
        [FromQuery] bool hideArchived = true)
    {
        var objectStorages = await _objectStorageBusiness.GetAllObjectStorages(
            organizationId, projectId, hideArchived);

        return Ok(objectStorages);
    }



    /// <summary>
    ///     Get an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived object storages from the result (Default true)</param>
    /// <returns>The object storage associated with the given ID</returns>
    [HttpGet("{objectStorageId:long}", Name = "api_get_object_storage_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> GetObjectStorage(
        long organizationId,
        long projectId,
        long objectStorageId,
        [FromQuery] bool hideArchived = true)
    {
        var objectStorage =
            await _objectStorageBusiness.GetObjectStorage(
                organizationId, projectId, objectStorageId, hideArchived);
        return Ok(objectStorage);
    }



    /// <summary>
    ///     Create an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="dto">The data transfer object containing object storage details</param>
    /// <returns>The created object storage</returns>
    [HttpPost(Name = "api_create_object_storage_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> CreateObjectStorage(
        long organizationId,
        long projectId,
        [FromBody] CreateObjectStorageRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var objectStorage = await _objectStorageBusiness.CreateObjectStorage(
            currentUserId, organizationId, projectId, dto);
        return Ok(objectStorage);
    }



    /// <summary>
    ///     Update an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to update</param>
    /// <param name="dto">The data transfer object containing updated object storage details</param>
    /// <returns>The updated object storage</returns>
    [HttpPut("{objectStorageId:long}", Name = "api_update_object_storage_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> UpdateObjectStorage(
        long organizationId,
        long projectId,
        long objectStorageId,
        [FromBody] UpdateObjectStorageRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var objectStorage = await _objectStorageBusiness.UpdateObjectStorage(
            currentUserId, organizationId, projectId, objectStorageId, dto);
        return Ok(objectStorage);
    }



    /// <summary>
    ///     Delete an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to delete</param>
    /// <returns>True if the object storage was successfully deleted.</returns>
    [HttpDelete("{objectStorageId:long}", Name = "api_delete_object_storage_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "object_storage")]
    public async Task<ActionResult> DeleteObjectStorage(
        long organizationId,
        long projectId,
        long objectStorageId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _objectStorageBusiness.DeleteObjectStorage(
            currentUserId, organizationId, projectId, objectStorageId);
        return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive an Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to archive or unarchive</param>
    /// <param name="archive">True to archive the object storage, false to unarchive it.</param>
    /// <returns>True if the object storage was successfully archived or unarchived.</returns>
    [HttpPatch("{objectStorageId:long}", Name = "api_archive_object_storage_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "object_storage")]
    public async Task<ActionResult> ArchiveObjectStorage(
        long organizationId,
        long projectId,
        long objectStorageId,
        [FromQuery] bool archive)
    {
        var currentUserId = UserContextStorage.UserId;
        if (archive)
        {
            var responseA = await _objectStorageBusiness.ArchiveObjectStorage(
                currentUserId, organizationId, projectId, objectStorageId);
            return Ok(responseA);
        }

        var responseB = await _objectStorageBusiness.UnarchiveObjectStorage(
            currentUserId, organizationId, projectId, objectStorageId);
        return Ok(responseB);
    }



    /// <summary>
    ///     Get Default Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <returns>The default object storage for the project</returns>
    [HttpGet("default", Name = "api_get_default_object_storage_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> GetDefaultObjectStorage(
        long organizationId,
        long projectId)
    {
        var defaultObjectStorage = await _objectStorageBusiness.GetDefaultObjectStorage(
            organizationId, projectId);
        return Ok(defaultObjectStorage);
    }



    /// <summary>
    ///     Set Default Object Storage 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage belongs</param>
    /// <param name="objectStorageId">The ID of the object storage to set as default</param>
    /// <returns>The object storage that was set as default.</returns>
    [HttpPatch("{objectStorageId:long}/default", Name = "api_set_default_object_storage_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> SetDefaultObjectStorage(
        long organizationId,
        long projectId,
        long objectStorageId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _objectStorageBusiness.SetDefaultObjectStorage(
            currentUserId, organizationId, projectId, objectStorageId);
        return Ok(response);
    }

    /// <summary>
    ///     Create a cloud object storage container for a Project
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the object storage will belong</param>
    /// <param name="containerName">The name of the container</param>
    /// <param name="existingContainer">A bool indicating whether the container already exists</param>
    /// <param name="storageType">The storage provider type. Currently only "azure_object" is supported.</param>
    /// <returns>The newly created object storage.</returns>
    [HttpPost("container", Name = "api_create_project_azure_container")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "object_storage")]
    public async Task<ActionResult<ObjectStorageResponseDto>> CreateProjectContainer(
        long organizationId,
        long projectId,
        string? containerName,
        bool existingContainer = false,
        [FromQuery] string storageType = "azure_object")
    {
        if (!string.Equals(storageType, "azure_object", StringComparison.OrdinalIgnoreCase))
        {
            // TODO: eventually support 'aws' as an option. https://nstinl.atlassian-us-gov-mod.net/browse/DL-2732
            throw new ArgumentException(
                $"Unsupported storage type '{storageType}'. Only 'azure_object' is currently supported.");
        }

        var currentUserId = UserContextStorage.UserId;
        var objectStorage = await _projectBusiness.CreateProjectAzureContainer(
            currentUserId, organizationId, projectId, containerName, existingContainer);
        return Ok(objectStorage);
    }
}
