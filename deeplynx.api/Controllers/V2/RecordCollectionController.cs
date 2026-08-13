using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for managing record collections.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve record collection information.
/// </remarks>
[ApiController]
[ApiVersion(2)]
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
    /// <param name="dto">The collection data transfer object used to search and return collections</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived collections from the result (Default true)</param>
    /// <returns>A paginated response of record collections based on the applied filters.</returns>
    [HttpGet(Name = "api_get_all_record_collections")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record_collection")]
    [Sensitivity("read record")]
    public async Task<ActionResult<PaginatedResponse<RecordCollectionResponseDto>>> GetAllRecordCollections(
        long organizationId,
        long projectId,
        [FromQuery] RecordCollectionQueryRequestDto dto,
        [FromQuery] bool hideArchived = true)
    {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var recordCollections =
                await _recordCollectionBusiness.GetAllRecordCollections(currentUserId, organizationId, projectId, dto, hideArchived, isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(recordCollections);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record_collection")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<IEnumerable<RecordResponseDto>>> GetRecordsInRecordCollection(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromQuery] bool hideArchived = true)
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



    /// <summary>
    ///     Get Record Collections for a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the collection belongs</param>
    /// <param name="recordId">The ID of the record whose collections are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived collections from the result (Default true)</param>
    /// <returns>A list of record collections for the specified record.</returns>
    [HttpGet("~/organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/record-collections", Name = "api_get_record_collections_for_a_record")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record_collection")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<PaginatedResponse<RecordCollectionResponseDto>>> GetRecordCollectionsForARecord(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] RecordCollectionQueryRequestDto dto,
        [FromQuery] bool hideArchived = true)
    {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var collections = await _recordCollectionBusiness.GetRecordCollectionsForRecord(
                currentUserId,
                organizationId,
                projectId,
                recordId,
                hideArchived,
                dto,
                isSysAdmin,
                isOrgAdmin,
                isProjectAdmin);
            return Ok(collections);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record_collection")]
    [Auth("read", "tag")]
    [Sensitivity("read record")]
    public async Task<ActionResult<IEnumerable<RecordCollectionResponseDto>>> GetRecordCollectionsByTags(
        long organizationId,
        long projectId,
        [FromQuery] long[] tagIds,
        [FromQuery] bool hideArchived = true)
    {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records = await _recordCollectionBusiness.GetRecordCollectionsByTags(currentUserId, organizationId, projectId, tagIds, hideArchived, isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(records);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record_collection")]
    [Sensitivity("read record")]
    public async Task<ActionResult<RecordCollectionResponseDto>> UpdateRecordCollection(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromBody] UpdateRecordCollectionRequestDto dto)
    {
            var currentUserId = UserContextStorage.UserId;
            var updatedRecordCollection = await _recordCollectionBusiness.UpdateRecordCollection(currentUserId, organizationId, projectId, recordCollectionId, dto);
            return Ok(updatedRecordCollection);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record_collection")]
    [Sensitivity("read record")]
    public async Task<IActionResult> AddRecordsToRecordCollection(long organizationId, long projectId,
        long recordCollectionId, [FromBody] long[] recordIds)
    {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var response = await _recordCollectionBusiness.AddRecordsToRecordCollection(currentUserId, organizationId, projectId,
                recordCollectionId, recordIds, isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(response);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record_collection")]
    [Sensitivity("read record")]
    public async Task<IActionResult> RemoveRecordsFromRecordCollection(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromBody] long[] recordIds)
    {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var response = await _recordCollectionBusiness.RemoveRecordsFromRecordCollection(
                currentUserId,
                organizationId,
                projectId,
                recordCollectionId,
                recordIds,
                isSysAdmin,
                isOrgAdmin,
                isProjectAdmin);
            return Ok(response);
    }



    /// <summary>
    ///     Create a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="dto">The collection request data transfer object containing collection details</param>
    /// <param name="sensitivityLabelIds">sensitivity labels to apply to the collection on creation</param>
    /// <returns>The created Record Collection</returns>
    [HttpPost(Name = "api_create_a_record_collection")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "record_collection")]
    [Sensitivity("read record")]
    public async Task<ActionResult<RecordCollectionResponseDto>> CreateRecordCollection(
        long organizationId,
        long projectId,
        [FromQuery] List<long>? sensitivityLabelIds,
        [FromBody] CreateRecordCollectionRequestDto dto)
    {
            var currentUserId = UserContextStorage.UserId;
            var recordCollection =
                await _recordCollectionBusiness.CreateRecordCollection(currentUserId, organizationId, projectId,
                    sensitivityLabelIds, dto);
            return Ok(recordCollection);
    }



    /// <summary>
    ///     Delete a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record collection belongs</param>
    /// <param name="recordCollectionId">The ID of the record collection to delete</param>
    /// <returns>The result of deleting the record collection.</returns>
    [HttpDelete("{recordCollectionId:long}", Name = "api_delete_a_record_collection")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "record_collection")]
    [Sensitivity("read record")]
    public async Task<IActionResult> DeleteRecordCollection(
        long organizationId,
        long projectId,
        long recordCollectionId)
    {
            var currentUserId = UserContextStorage.UserId;
            var response = await _recordCollectionBusiness.DeleteRecordCollection(currentUserId, organizationId, projectId, recordCollectionId);
            return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordCollectionId">The ID of the record to archive or unarchive</param>
    /// <param name="archive">True to archive the record, false to unarchive it.</param>
    /// <returns>The result of archiving or unarchiving the record collection.</returns>
    [HttpPatch("{recordCollectionId:long}", Name = "api_archive_record_collection")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record_collection")]
    [Sensitivity("read record")]
    public async Task<IActionResult> ArchiveRecordCollection(
        long organizationId,
        long projectId,
        long recordCollectionId,
        [FromQuery] bool archive)
    {
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                var responseA = await _recordCollectionBusiness.ArchiveRecordCollection(currentUserId, organizationId, projectId, recordCollectionId);
                return Ok(responseA);
            }

            var responseB = await _recordCollectionBusiness.UnarchiveRecordCollection(currentUserId, organizationId, projectId, recordCollectionId);
            return Ok(responseB);
    }



    /// <summary>
    ///     Attach a Tag to a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <param name="tagId">The ID of the tag to attach</param>
    /// <returns>The result of attaching the tag to the record collection.</returns>
    [HttpPost("{recordCollectionId:long}/tags/{tagId:long}", Name = "api_attach_tag_to_record_collection")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record_collection")]
    [Auth("read", "tag")]
    [Sensitivity("read record")]
    public async Task<IActionResult> AttachTag(
        long organizationId,
        long projectId,
        long recordCollectionId,
        long tagId)
    {
            var response = await _recordCollectionBusiness.AttachTag(organizationId, projectId, recordCollectionId, tagId);
            return Ok(response);
    }



    /// <summary>
    ///     Unattach a Tag from a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <param name="tagId">The ID of the tag to unattach</param>
    /// <returns>The result of unattaching the tag from the record collection.</returns>
    [HttpDelete("{recordCollectionId:long}/tags/{tagId:long}", Name = "api_unattach_tag_from_record_collection")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record_collection")]
    [Auth("read", "tag")]
    [Sensitivity("read record")]
    public async Task<IActionResult> UnattachTag(
        long organizationId,
        long projectId,
        long recordCollectionId,
        long tagId)
    {
            var response = await _recordCollectionBusiness.UnattachTag(organizationId, projectId, recordCollectionId, tagId);
            return Ok(response);
    }



    /// <summary>
    ///     Attach a Sensitivity Label to a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record collectionbelongs</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <param name="sensitivityLabelId">The ID of the label to attach</param>
    /// <returns>The result of attaching the sensitivity label to the record collection.</returns>
    [HttpPost("{recordCollectionId:long}/sensitivity-labels/{sensitivityLabelId:long}", Name = "api_attach_sensitivity_label_to_record_collection")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record_collection")]
    [Auth("read", "sensitivity_label")]
    [Sensitivity("read record")]
    public async Task<IActionResult> AttachSensitivityLabel(
        long organizationId,
        long projectId,
        long recordCollectionId,
        long sensitivityLabelId)
    {
            var response = await _recordCollectionBusiness.AttachLabel(organizationId, projectId, recordCollectionId, sensitivityLabelId);
            return Ok(response);
    }



    /// <summary>
    ///     Unattach a sensitivity label from a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record collection belongs</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <param name="sensitivityLabelId">The ID of the label to unattach</param>
    /// <returns>The result of unattaching the sensitivity label from the record collection.</returns>
    [HttpDelete("{recordCollectionId:long}/sensitivity-labels/{sensitivityLabelId:long}", Name = "api_unattach_sensitivity_label_from_record_collection")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record_collection")]
    [Auth("read", "sensitivity_label")]
    [Sensitivity("read record")]
    public async Task<IActionResult> UnattachSensitivityLabel(
        long organizationId,
        long projectId,
        long recordCollectionId,
        long sensitivityLabelId)
    {
            var response = await _recordCollectionBusiness.UnattachLabel(organizationId, projectId, recordCollectionId, sensitivityLabelId);
            return Ok(response);
    }




    /// <summary>
    ///     Get Sensitivity Labels for a Record Collection
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record collectionbelongs</param>
    /// <param name="recordCollectionId">The ID of the record collection</param>
    /// <returns>A message stating the label was successfully attached to the record.</returns>
    [HttpGet("{recordCollectionId:long}/sensitivity-labels", Name = "api_get_sensitivity_labels_for_record_collection")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record_collection")]
    [Auth("read", "sensitivity_label")]
    [Sensitivity("read record")]
    public async Task<IActionResult> GetSensitivityLabelsForRecordCollection(
        long organizationId,
        long projectId,
        long recordCollectionId)
    {
            var sensitivityLabels = await _recordCollectionBusiness.GetSensitivityLabelsForRecordCollection(organizationId, projectId, recordCollectionId);
            return Ok(sensitivityLabels);
    }

}