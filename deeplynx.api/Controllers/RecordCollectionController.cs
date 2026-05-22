using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing record collections.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve record collection information.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/record-collections")]
[Authorize]
[Tags("Record Collection")]
public class RecordCollectionController : ControllerBase
{
    private readonly ILogger<RecordCollectionController> _logger;
    private readonly IRecordCollectionBusiness _recordCollectionBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecordCollectionController" /> class
    /// </summary>
    /// <param name="recordCollectionBusiness">The business logic interface for handling record operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public RecordCollectionController(IRecordCollectionBusiness recordCollectionBusiness,
        ILogger<RecordCollectionController> logger)
    {
        _recordCollectionBusiness = recordCollectionBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Record Collections
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose collections are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived collections from the result (Default true)</param>
    /// <returns>A list of record collections based on the applied filters.</returns>
    [HttpGet(Name = "api_get_all_record_collections")]
    [Auth("read", "record_collection")]
    [Sensitivity("read record")]
    public async Task<ActionResult<IEnumerable<RecordCollectionResponseDto>>> GetAllRecordCollections(
        long organizationId,
        long projectId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var recordCollections =
                await _recordCollectionBusiness.GetAllRecordCollections(currentUserId, organizationId, projectId, hideArchived, isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(recordCollections);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing all record collections: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Records In a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the collection belongs</param>
    /// <param name="recordCollectionId">The ID of the collection whose records are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result (Default true)</param>
    /// <returns>A list of records in the specified record collection.</returns>
    [HttpGet("{recordCollectionId:long}/records", Name = "api_get_records_in_record_collection")]
    [Auth("read", "record_collection")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<IEnumerable<RecordResponseDto>>> GetRecordsInRecordCollection(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records = await _recordCollectionBusiness.GetRecordsInRecordCollection(
                currentUserId,
                organizationId,
                projectId,
                recordCollectionId,
                hideArchived,
                isSysAdmin,
                isOrgAdmin,
                isProjectAdmin);
            return Ok(records);
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing records in collection {recordCollectionId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Record Collections by Tags
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the records belong</param>
    /// <param name="tagIds">The list of tag IDs to filter records by - records must contain all IDs in the list</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result (Default true)</param>
    /// <returns>A list of record collections that have all the specified tags.</returns>
    [HttpGet("by-tags", Name = "api_get_record_collections_by_tags")]
    [Auth("read", "record_collection")]
    [Auth("read", "tag")]
    [Sensitivity("read record")]
    public async Task<ActionResult<IEnumerable<RecordCollectionResponseDto>>> GetRecordCollectionsByTags(
        long organizationId,
        long projectId,
        [FromQuery] long[] tagIds,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records = await _recordCollectionBusiness.GetRecordCollectionsByTags(currentUserId, organizationId, projectId, tagIds, hideArchived, isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(records);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing records by tags: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    ///     Update Record Collection Metadata
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordCollectionId">The ID of the record to update</param>
    /// <param name="dto">The record request data transfer object containing updated record details</param>
    /// <returns>The updated record</returns>
    [HttpPut("{recordCollectionId:long}", Name = "api_update_a_record_collection")]
    [Auth("update", "record_collection")]
    [Sensitivity("read record")]
    public async Task<ActionResult<RecordCollectionResponseDto>> UpdateRecordCollection(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromBody] UpdateRecordCollectionRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var updatedRecordCollection = await _recordCollectionBusiness.UpdateRecordCollection(currentUserId, organizationId, projectId, recordCollectionId, dto);
            return Ok(updatedRecordCollection);
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating record collection {recordCollectionId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }


    /// <summary>
    ///     Add Records to a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to search within</param>
    /// <param name="recordCollectionId">The ID of the collection to add records to</param>
    /// <param name="recordIds">The ids of records to add to collection</param>
    /// <returns>Records added to collection.</returns>
    [HttpPost("{recordCollectionId:long}/records", Name = "api_add_records_to_record_collection")]
    [Auth("update", "record_collection")]
    [Sensitivity("read record")]
    public async Task<IActionResult> AddRecordsToRecordCollection(long organizationId, long projectId,
        long recordCollectionId, [FromBody] long[] recordIds)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records = await _recordCollectionBusiness.AddRecordsToRecordCollection(currentUserId, organizationId, projectId,
                recordCollectionId, recordIds, isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(records);
        }
        catch (ArgumentException exc)
        {
            return BadRequest(exc.Message);
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (UnauthorizedAccessException exc)
        {
            return StatusCode(StatusCodes.Status403Forbidden, exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating record collection records: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Remove Records from a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to search within</param>
    /// <param name="recordCollectionId">The ID of the collection to add records to</param>
    /// <param name="recordIds">Records to remove from collection </param>
    /// <returns>Records removed from collection.</returns>
    [HttpPut("{recordCollectionId:long}/records", Name = "api_remove_records_from_record_collection")]
    [Auth("update", "record_collection")]
    [Sensitivity("read record")]
    public async Task<IActionResult> RemoveRecordsFromRecordCollection(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromBody] long[] recordIds)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records = await _recordCollectionBusiness.RemoveRecordsFromRecordCollection(
                currentUserId,
                organizationId,
                projectId,
                recordCollectionId,
                recordIds,
                isSysAdmin,
                isOrgAdmin,
                isProjectAdmin);
            return Ok(records);
        }
        catch (ArgumentException exc)
        {
            return BadRequest(exc.Message);
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (UnauthorizedAccessException exc)
        {
            return StatusCode(StatusCodes.Status403Forbidden, exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating record collection records: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="dto">The record request data transfer object containing record details</param>
    /// <param name="sensitivityLabelIds">sensitivity labels to apply to the collection on creation</param>
    /// <returns>The created Record Collection</returns>
    [HttpPost(Name = "api_create_a_record_collection")]
    [Auth("write", "record_collection")]
    public async Task<ActionResult<RecordCollectionResponseDto>> CreateRecordCollection(
        long organizationId,
        long projectId,
        [FromQuery] List<long>? sensitivityLabelIds,
        [FromBody] CreateRecordCollectionRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var record =
                await _recordCollectionBusiness.CreateRecordCollection(currentUserId, organizationId, projectId,
                    sensitivityLabelIds, dto);
            return Ok(record);
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating record collection: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record collection belongs</param>
    /// <param name="recordCollectionId">The ID of the record collection to delete</param>
    /// <returns>A message stating the record collection was successfully deleted.</returns>
    [HttpDelete("{recordCollectionId:long}", Name = "api_delete_a_record_collection")]
    [Auth("write", "record_collection")]
    [Sensitivity("read record")]
    public async Task<IActionResult> DeleteRecordCollection(
        long organizationId,
        long projectId,
        long recordCollectionId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _recordCollectionBusiness.DeleteRecordCollection(currentUserId, organizationId, projectId, recordCollectionId);
            return Ok(new { message = $"Deleted record collection {recordCollectionId}" });
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting record collection {recordCollectionId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    ///     Archive or Unarchive a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordCollectionId">The ID of the record to archive or unarchive</param>
    /// <param name="archive">True to archive the record, false to unarchive it.</param>
    /// <returns>A message stating the record was successfully archived or unarchived.</returns>
    [HttpPatch("{recordCollectionId:long}", Name = "api_archive_record_collection")]
    [Auth("update", "record_collection")]
    [Sensitivity("read record")]
    public async Task<IActionResult> ArchiveRecordCollection(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromQuery] bool archive)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                await _recordCollectionBusiness.ArchiveRecordCollection(currentUserId, organizationId, projectId, recordCollectionId);
                return Ok(new { message = $"Archived record collection {recordCollectionId}" });
            }

            await _recordCollectionBusiness.UnarchiveRecordCollection(currentUserId, organizationId, projectId, recordCollectionId);
            return Ok(new { message = $"Unarchived record collection {recordCollectionId}" });
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} record collection{recordCollectionId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    ///     Attach a Tag to a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <param name="tagId">The ID of the tag to attach</param>
    /// <returns>A message stating the tag was successfully attached to the record.</returns>
    [HttpPost("{recordCollectionId:long}/tags", Name = "api_attach_tag_to_record_collection")]
    [Auth("update", "record_collection")]
    [Auth("read", "tag")]
    [Sensitivity("read record")]
    public async Task<IActionResult> AttachTag(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromQuery] long tagId)
    {
        try
        {
            await _recordCollectionBusiness.AttachTag(organizationId, projectId, recordCollectionId, tagId);
            return Ok(new { message = $"Tag {tagId} attached to record collection {recordCollectionId}" });
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (InvalidOperationException exc)
        {
            return Conflict(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while attaching tag {tagId} to record collection{recordCollectionId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    ///     Unattach a Tag from a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <param name="tagId">The ID of the tag to unattach</param>
    /// <returns>A message stating the tag was successfully unattached from the record.</returns>
    [HttpDelete("{recordCollectionId:long}/tags", Name = "api_unattach_tag_from_record_collection")]
    [Auth("update", "record_collection")]
    [Auth("read", "tag")]
    [Sensitivity("read record")]
    public async Task<IActionResult> UnattachTag(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromQuery] long tagId)
    {
        try
        {
            await _recordCollectionBusiness.UnattachTag(organizationId, projectId, recordCollectionId, tagId);
            return Ok(new { message = $"Tag {tagId} unattached from record collection {recordCollectionId}" });
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while unattaching tag {tagId} from record collection {recordCollectionId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    ///     Attach a Sensitivity Label to a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record collectionbelongs</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <param name="sensitivityLabelId">The ID of the label to attach</param>
    /// <returns>A message stating the label was successfully attached to the record.</returns>
    [HttpPost("{recordCollectionId:long}/sensitivity-labels", Name = "api_attach_sensitivity_label_to_record_collection")]
    [Auth("update", "record_collection")]
    [Auth("read", "sensitivity_label")]
    [Sensitivity("read record")]
    public async Task<IActionResult> AttachSensitivityLabel(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromQuery] long sensitivityLabelId)
    {
        try
        {
            await _recordCollectionBusiness.AttachLabel(organizationId, projectId, recordCollectionId, sensitivityLabelId);
            return Ok(new { message = $"label {sensitivityLabelId} attached to record {recordCollectionId}" });
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (InvalidOperationException exc)
        {
            return Conflict(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while attaching label {sensitivityLabelId} to record {recordCollectionId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    ///     Unattach a sensitivity label from a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record collection belongs</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <param name="sensitivityLabelId">The ID of the label to unattach</param>
    /// <returns>A message stating the label was successfully unattached from the record.</returns>
    [HttpDelete("{recordCollectionId:long}/sensitivity-labels", Name = "api_unattach_sensitivity_label_from_record_collection")]
    [Auth("update", "record_collection")]
    [Auth("read", "sensitivity_label")]
    [Sensitivity("read record")]
    public async Task<IActionResult> UnattachSensitivityLabel(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromQuery] long sensitivityLabelId)
    {
        try
        {
            await _recordCollectionBusiness.UnattachLabel(organizationId, projectId, recordCollectionId, sensitivityLabelId);
            return Ok(new { message = $"Sensitivity label {sensitivityLabelId} unattached from record collection {recordCollectionId}" });
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (InvalidOperationException exc)
        {
            return Conflict(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while unattaching sensitivity label {sensitivityLabelId} from record collection {recordCollectionId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
}
