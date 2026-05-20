using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing records.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve record information.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/records")]
[Authorize]
public class RecordController : ControllerBase
{
    private readonly IGraphBusiness _graphBusiness;
    private readonly ILogger<RecordController> _logger;
    private readonly IRecordBusiness _recordBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecordController" /> class
    /// </summary>
    /// <param name="recordBusiness">The business logic interface for handling record operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public RecordController(IRecordBusiness recordBusiness, IGraphBusiness graphBusiness,
        ILogger<RecordController> logger)
    {
        _recordBusiness = recordBusiness;
        _graphBusiness = graphBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Records
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose records are to be retrieved</param>
    /// <param name="dataSourceId">(Optional) The ID of the datasource by which to filter records</param>
    /// <param name="fileType">
    ///     (Optional) File extension to filter by (e.g., pdf, png, jpg) - leading dot is optional and will
    ///     be removed
    /// </param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result (Default true)</param>
    /// <returns>A list of records based on the applied filters.</returns>
    [HttpGet(Name = "api_get_all_records")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<IEnumerable<RecordResponseDto>>> GetAllRecords(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId = null,
        [FromQuery] string? fileType = null,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records =
                await _recordBusiness.GetAllRecords(currentUserId, organizationId, projectId, dataSourceId, hideArchived, fileType, isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(records);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing all records: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }


    /// <summary>
    /// GetAllRecords (Paginated)
    /// </summary>
    /// <param name="organizationId">The id of the organization</param>
    /// <param name="projectId">The id of the project</param>
    /// <param name="hideArchived">Whether to hide archived records</param>
    /// <param name="queryDto">Pagination parameters</param>
    /// <returns>A paginated list of records based on applied filters</returns>
    [HttpGet("GetAllRecordsPaginated", Name = "api_get_all_records_paginated")]
    public async Task<ActionResult<PaginatedResponse<RecordResponseDto>>> GetAllRecordsPaginated(
        long organizationId,
        long projectId,
        bool hideArchived,
        [FromQuery] RecordQueryRequestDto? queryDto
    )
    {
        try
        {
            var currentUserID  = UserContextStorage.UserId;
            var isSysAdmin     = UserContextStorage.IsSysAdmin;
            var isOrgAdmin     = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;

            var records = await _recordBusiness.GetAllRecordsPaginated(
                currentUserID,
                organizationId,
                projectId,
                hideArchived,
                queryDto,
                isSysAdmin,
                isOrgAdmin,
                isProjectAdmin
            );

            return Ok(records);
        }
        catch (Exception e)
        {
            var message = $"An unexpected error occurred while fetching records: {e}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }



    /// <summary>
    ///     Get Records by Tags
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the records belong</param>
    /// <param name="tagIds">The list of tag IDs to filter records by - records must contain all IDs in the list</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result (Default true)</param>
    /// <returns>A list of records that have all the specified tags.</returns>
    [HttpGet("by-tags", Name = "api_get_records_by_tags")]
    [Auth("read", "record")]
    [Auth("read", "tag")]
    [Sensitivity("read record")]
    public async Task<ActionResult<IEnumerable<RecordResponseDto>>> GetRecordsByTags(
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
            var records = await _recordBusiness.GetRecordsByTags(currentUserId, organizationId, projectId, tagIds, hideArchived, isSysAdmin, isOrgAdmin, isProjectAdmin);
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
    ///     Get Records by Original IDs
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to search within</param>
    /// <param name="dataSourceId">The ID of the data source to search within</param>
    /// <param name="originalIds">List of original IDs to retrieve records for</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result (Default true)</param>
    /// <returns>A list of records matching the provided original IDs.</returns>
    [HttpPost("by-original-ids", Name = "api_get_records_by_original_ids")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<IEnumerable<RecordResponseDto>>> GetRecordsByOriginalId(
        long organizationId,
        long projectId,
        [FromQuery] long dataSourceId,
        [FromBody] List<string> originalIds,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records = await _recordBusiness.GetRecordsByOriginalId(
                currentUserId, organizationId, projectId, dataSourceId, originalIds, hideArchived, isSysAdmin, isOrgAdmin, isProjectAdmin);
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
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving records by original ID: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result (Default true)</param>
    /// <returns>The record associated with the given ID</returns>
    [HttpGet("{recordId:long}", Name = "api_get_a_record")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<RecordResponseDto>> GetRecord(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, hideArchived);
            return Ok(record);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Record Count for a Data Source
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the records belong</param>
    /// <param name="dataSourceId">The ID of the datasource by which to count records for</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result (Default true)</param>
    /// <returns>The record count for the given data source</returns>
    [HttpGet("count", Name = "api_get_records_count_by_data_source")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<int>> GetRecordsCountByDataSource(
        long organizationId,
        long projectId,
        [FromQuery] long dataSourceId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var count =
                await _recordBusiness.GetRecordsCountByDataSource(organizationId, projectId, dataSourceId,
                    hideArchived);
            return Ok(count);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while counting records for data source {dataSourceId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the record belongs</param>
    /// <param name="dto">The record request data transfer object containing record details</param>
    /// <param name="sensitivityLabelIds">The IDs of the labels to attach</param>
    /// <returns>The created record</returns>
    [HttpPost(Name = "api_create_a_record")]
    [Auth("write", "record")]
    [Sensitivity("write record")]
    public async Task<ActionResult<RecordResponseDto>> CreateRecord(
        long organizationId,
        long projectId,
        [FromQuery] long dataSourceId,
        [FromQuery] List<long>? sensitivityLabelIds,
        [FromBody] CreateRecordRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var record =
                await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, dataSourceId, dto, sensitivityLabelIds);
            return Ok(record);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating record: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Bulk Create Records
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the records belong</param>
    /// <param name="dataSourceId">The ID of the data source to which the records belong</param>
    /// <param name="records">List of record request data transfer objects containing record details</param>
    /// <param name="sensitivityLabelIds">List of sensitivity labels that will be attached to created records</param>
    /// <returns>The created records</returns>
    [HttpPost("bulk", Name = "api_create_many_records")]
    [Auth("write", "record")]
    [Sensitivity("write record")]
    public async Task<ActionResult<List<RecordResponseDto>>> BulkCreateRecords(
        long organizationId,
        long projectId,
        [FromQuery] long dataSourceId,
        [FromBody] List<CreateRecordRequestDto> records,
        [FromQuery] List<long>? sensitivityLabelIds = null)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var newRecords =
                await _recordBusiness.BulkCreateRecords(currentUserId, organizationId, projectId, dataSourceId,
                    records, sensitivityLabelIds);
            return Ok(newRecords);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating records: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Update a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record to update</param>
    /// <param name="dto">The record request data transfer object containing updated record details</param>
    /// <returns>The updated record</returns>
    [HttpPut("{recordId:long}", Name = "api_update_a_record")]
    [Auth("update", "record")]
    [Sensitivity("update record")]
    public async Task<ActionResult<RecordResponseDto>> UpdateRecord(
        long organizationId,
        long projectId,
        long recordId,
        [FromBody] UpdateRecordRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var updated = await _recordBusiness.UpdateRecord(currentUserId, organizationId, projectId, recordId, dto);
            return Ok(updated);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record to delete</param>
    /// <returns>A message stating the record was successfully deleted.</returns>
    [HttpDelete("{recordId:long}", Name = "api_delete_a_record")]
    [Auth("write", "record")]
    [Sensitivity("delete record")]
    public async Task<IActionResult> DeleteRecord(
        long organizationId,
        long projectId,
        long recordId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _recordBusiness.DeleteRecord(currentUserId, organizationId, projectId, recordId);
            return Ok(new { message = $"Deleted record {recordId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record to archive or unarchive</param>
    /// <param name="archive">True to archive the record, false to unarchive it.</param>
    /// <returns>A message stating the record was successfully archived or unarchived.</returns>
    [HttpPatch("{recordId:long}", Name = "api_archive_record")]
    [Auth("update", "record")]
    [Sensitivity("update record")]
    public async Task<IActionResult> ArchiveRecord(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] bool archive)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                await _recordBusiness.ArchiveRecord(currentUserId, organizationId, projectId, recordId);
                return Ok(new { message = $"Archived record {recordId}" });
            }

            await _recordBusiness.UnarchiveRecord(currentUserId, organizationId, projectId, recordId);
            return Ok(new { message = $"Unarchived record {recordId}" });
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Attach a Tag to a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="tagId">The ID of the tag to attach</param>
    /// <returns>A message stating the tag was successfully attached to the record.</returns>
    [HttpPost("{recordId:long}/tags", Name = "api_attach_a_tag")]
    [Auth("update", "record")]
    [Auth("read", "tag")]
    [Sensitivity("update record")]
    public async Task<IActionResult> AttachTag(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] long tagId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _recordBusiness.AttachTag(currentUserId, organizationId, projectId, recordId, tagId);
            return Ok(new { message = $"Tag {tagId} attached to record {recordId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while attaching tag {tagId} to record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Unattach a Tag from a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="tagId">The ID of the tag to unattach</param>
    /// <returns>A message stating the tag was successfully unattached from the record.</returns>
    [HttpDelete("{recordId:long}/tags", Name = "api_unattach_a_tag")]
    [Auth("update", "record")]
    [Auth("read", "tag")]
    [Sensitivity("update record")]
    public async Task<IActionResult> UnattachTag(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] long tagId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _recordBusiness.UnattachTag(currentUserId, organizationId, projectId, recordId, tagId);
            return Ok(new { message = $"Tag {tagId} unattached from record {recordId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while unattaching tag {tagId} from record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Bulk Attach Tags to Records
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="dtos">List of record/tag pairs to attach</param>
    /// <returns></returns>
    [HttpPost("bulk-attach-tags-to-records", Name = "api_bulk_attach_tags_to_records")]
    [Auth("update", "record")]
    [Auth("read", "tag")]
    [Sensitivity("update record")]
    public async Task<IActionResult> BulkAttachTagsToRecords(
        long organizationId,
        long projectId,
        [FromBody] List<RecordTagLinkDto> dtos)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;

            await _recordBusiness.BulkAttachTagsToRecords(currentUserId, organizationId, projectId, dtos);

            return Ok(new { message = "Successfully bulk attached tags to records" });
        }
        catch (ArgumentException exc)
        {
            return BadRequest(exc.Message);
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while bulk attaching tags to records: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    ///     Attach a Sensitivity Label to a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="sensitivityLabelId">The ID of the label to attach</param>
    /// <returns>A message stating the label was successfully attached to the record.</returns>
    [HttpPost("{recordId:long}/sensitivity-labels", Name = "api_attach_sensitivity_label")]
    [Auth("update", "record")]
    [Auth("read", "sensitivity_label")]
    [Sensitivity("update record")]
    public async Task<IActionResult> AttachSensitivityLabel(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] long sensitivityLabelId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _recordBusiness.AttachLabel(currentUserId, organizationId, projectId, recordId, sensitivityLabelId);
            return Ok(new { message = $"label {sensitivityLabelId} attached to record {recordId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while attaching label {sensitivityLabelId} to record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Bulk attach sensitivity label(s) to records
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the records belong</param>
    /// <param name="recordIds">The IDs of the records that the sensitivity labels will be attached to</param>
    /// <param name="sensitivityLabelIds">The ID of the labels that will be attached to all provided records by ID</param>
    /// <returns>Boolean value defining if the operation was successful.</returns>
    [HttpPost("bulk-attach-sensitivity-labels", Name = "api_bulk_attach_sensitivity_labels")]
    [Auth("update", "record")]
    [Auth("read", "sensitivity_label")]
    [Sensitivity("update record")]
    public async Task<IActionResult> BulkAttachSensitivityLabels(
        long organizationId,
        long projectId,
        [FromQuery] List<long> recordIds,
        [FromQuery] List<long> sensitivityLabelIds)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _recordBusiness.BulkAttachLabels(currentUserId, organizationId, projectId, recordIds,
                sensitivityLabelIds);
            return Ok(new
            {
                message =
                    $"Successfully bulk attached all labels to all records"
            });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while bulk attaching sensitivity labels to records: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Unattach a sensitivity label from a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="sensitivityLabelId">The ID of the label to unattach</param>
    /// <returns>A message stating the label was successfully unattached from the record.</returns>
    [HttpDelete("{recordId:long}/sensitivity-labels", Name = "api_unattach_sensitivity-label")]
    [Auth("update", "record")]
    [Auth("read", "sensitivity_label")]
    [Sensitivity("update record")]
    public async Task<IActionResult> UnattachSensitivityLabel(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] long sensitivityLabelId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _recordBusiness.UnattachLabel(currentUserId, organizationId, projectId, recordId, sensitivityLabelId);
            return Ok(new { message = $"Sensitivity label {sensitivityLabelId} unattached from record {recordId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while unattaching sensitivity label {sensitivityLabelId} from record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Edges by Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record by which to filter edges</param>
    /// <param name="isOrigin">Indicates whether to find where recordId is origin or not</param>
    /// <param name="page">Indicates the page number for pagination</param>
    /// <param name="pageSize">Indicates the page size for pagination</param>
    /// <returns>A list of related records based on edges.</returns>
    [HttpGet("{recordId:long}/edges", Name = "api_get_edges_by_record")]
    [Auth("read", "record")]
    [Auth("read", "edge")]
    public async Task<ActionResult<IEnumerable<RelatedRecordsResponseDto>>> GetEdgesByRecord(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] bool isOrigin,
        [FromQuery] int page,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var edges = await _graphBusiness.GetEdgesByRecord(
                currentUserId, organizationId, projectId, recordId, isOrigin, page, pageSize);
            return Ok(edges);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing edges by record: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Graph Data for Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record for which to retrieve graph data</param>
    /// <param name="depth">The number of levels you want to search through</param>
    /// <returns>Graph data including nodes and edges.</returns>
    [HttpGet("{recordId:long}/graph", Name = "api_get_graph_data_for_record")]
    [Auth("read", "record")]
    public async Task<ActionResult<GraphResponse>> GetGraphDataForRecord(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] int depth)
    {
        try
        {
            var edges = await _graphBusiness.GetGraphDataForRecord(
                organizationId, projectId, recordId, UserContextStorage.UserId, depth);
            return Ok(edges);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving graph data: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}