using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for managing records.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve record information.
/// </remarks>
[ApiController]
[ApiVersion(2)]
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
    /// <param name="isInsightEligible">Restricts to records that are eligible for use in Insight if `true`</param>
    /// <returns>A list of records based on the applied filters.</returns>
    [HttpGet(Name = "api_get_all_records")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<IEnumerable<RecordResponseDto>>> GetAllRecords(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId = null,
        [FromQuery] string? fileType = null,
        [FromQuery] bool hideArchived = true,
        [FromQuery] bool isInsightEligible = false)
    {
        var currentUserId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var isOrgAdmin = UserContextStorage.IsOrgAdmin;
        var isProjectAdmin = UserContextStorage.IsProjectAdmin;
        var records =
            await _recordBusiness.GetAllRecords(currentUserId, organizationId, projectId, dataSourceId, hideArchived, fileType,
                isSysAdmin, isOrgAdmin, isProjectAdmin, isInsightEligible);
        return Ok(records);
    }




    /// <summary>
    ///     Get All Records Paginated
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose records are to be retrieved</param>
    /// <param name="dataSourceId">(Optional) The ID of the datasource by which to filter records</param>
    /// <param name="fileType">
    ///     (Optional) File extension to filter by (e.g., pdf, png, jpg) - leading dot is optional and will
    ///     be removed
    /// </param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result (Default true)</param>
    /// <param name="isInsightEligible">Restricts to records that are eligible for use in Insight if `true`</param>
    /// <param name="paginatedDto">Pagination details</param>
    /// <returns>A paginated list of records based on the applied filters.</returns>
    [HttpGet("paginated", Name = "api_get_all_records_paginated")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<PaginatedResponse<RecordResponseDto>>> GetAllRecordsPaginated(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId = null,
        [FromQuery] string? fileType = null,
        [FromQuery] bool hideArchived = true,
        [FromQuery] bool isInsightEligible = false,
        [FromQuery] PaginatedRequestDto? paginatedDto = null)
    {
        paginatedDto ??= new PaginatedRequestDto();
        var currentUserId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var isOrgAdmin = UserContextStorage.IsOrgAdmin;
        var isProjectAdmin = UserContextStorage.IsProjectAdmin;
        var records = await _recordBusiness.GetAllRecordsPaginated(
            currentUserId,
            organizationId,
            projectId,
            dataSourceId,
            hideArchived,
            fileType,
            paginatedDto,
            isSysAdmin,
            isOrgAdmin,
            isProjectAdmin,
            isInsightEligible);
        return Ok(records);
    }



    /// <summary>
    ///     Paginated full text records search
    /// </summary>
    /// <remarks>
    ///     Embedding must be one of: any, embedded, pending
    /// </remarks>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the records belongs</param>
    /// <param name="search">Search parameters</param>
    /// <param name="paginated">Pagination parameters</param>
    /// <returns>Paginated list of record response dtos from the query view that match provided query parameters</returns>
    [HttpGet("search/paginated", Name = "api_record_search_paginated")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    public async Task<ActionResult<PaginatedResponse<RecordResponseDto>>> SearchPaginated(
        long organizationId,
        long projectId,
        [FromQuery] RecordSearchRequestDto search,
        [FromQuery] PaginatedRequestDto paginated)
    {
        var currentUserId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var isOrgAdmin = UserContextStorage.IsOrgAdmin;
        var isProjectAdmin = UserContextStorage.IsProjectAdmin;
        var records =
            await _recordBusiness.SearchPaginated(currentUserId, organizationId, projectId, search, paginated,
                isSysAdmin, isOrgAdmin, isProjectAdmin);
        return Ok(records);
    }

    /// <summary>
    ///     Full text records search
    /// </summary>
    /// <remarks>
    ///     Embedding must be one of: any, embedded, pending
    /// </remarks>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the records belongs</param>
    /// <param name="search">Search parameters</param>
    /// <returns>List of record response dtos from the query view that match provided query parameters</returns>
    [HttpGet("search", Name = "api_record_search")]
    [Auth("read", "record")]
    public async Task<ActionResult<List<RecordResponseDto>>> Search(
        long organizationId,
        long projectId,
        [FromQuery] RecordSearchRequestDto search)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records =
                await _recordBusiness.Search(currentUserId, organizationId, projectId, search,
                    isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(records);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while searching records: {exc}";
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
    /// <param name="paginatedRequestDto">Pagination parameters</param>
    /// <returns>A paginated list of records that have all the specified tags.</returns>
    [HttpGet("by-tags", Name = "api_get_records_by_tags")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Auth("read", "tag")]
    [Sensitivity("read record")]
    public async Task<ActionResult<IEnumerable<RecordResponseDto>>> GetRecordsByTags(
        long organizationId,
        long projectId,
        [FromQuery] long[] tagIds,
        [FromQuery] bool hideArchived = true,
        [FromQuery] PaginatedRequestDto? paginatedRequestDto = null)
    {
            paginatedRequestDto ??= new PaginatedRequestDto();
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records = await _recordBusiness.GetRecordsByTagsPaginated(currentUserId, organizationId, projectId, tagIds, hideArchived, paginatedRequestDto, isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(records);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<IEnumerable<RecordResponseDto>>> GetRecordsByOriginalId(
        long organizationId,
        long projectId,
        [FromQuery] long dataSourceId,
        [FromBody] List<string> originalIds,
        [FromQuery] bool hideArchived = true)
    {
        var currentUserId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var isOrgAdmin = UserContextStorage.IsOrgAdmin;
        var isProjectAdmin = UserContextStorage.IsProjectAdmin;
        var records = await _recordBusiness.GetRecordsByOriginalId(
            currentUserId, organizationId, projectId, dataSourceId, originalIds, hideArchived, isSysAdmin, isOrgAdmin, isProjectAdmin);
        return Ok(records);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<RecordResponseDto>> GetRecord(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] bool hideArchived = true)
    {
        var currentUserId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var isOrgAdmin = UserContextStorage.IsOrgAdmin;
        var isProjectAdmin = UserContextStorage.IsProjectAdmin;

        var record = await _recordBusiness.GetRecord(
            currentUserId,
            organizationId,
            projectId,
            recordId,
            hideArchived,
            isSysAdmin,
            isOrgAdmin,
            isProjectAdmin);

        return Ok(record);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<int>> GetRecordsCountByDataSource(
        long organizationId,
        long projectId,
        [FromQuery] long dataSourceId,
        [FromQuery] bool hideArchived = true)
    {
        var count =
                 await _recordBusiness.GetRecordsCountByDataSource(organizationId, projectId, dataSourceId,
                     hideArchived);
        return Ok(count);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "record")]
    [Sensitivity("write record")]
    public async Task<ActionResult<RecordResponseDto>> CreateRecord(
        long organizationId,
        long projectId,
        [FromQuery] long dataSourceId,
        [FromQuery] List<long>? sensitivityLabelIds,
        [FromBody] CreateRecordRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var isOrgAdmin = UserContextStorage.IsOrgAdmin;
        var isProjectAdmin = UserContextStorage.IsProjectAdmin;
        var record = await _recordBusiness.CreateRecord(
            currentUserId,
            organizationId,
            projectId,
            dataSourceId,
            dto,
            sensitivityLabelIds,
            embedded: false,
            isSysAdmin,
            isOrgAdmin,
            isProjectAdmin);
        return Ok(record);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "record")]
    [Sensitivity("write record")]
    public async Task<ActionResult<List<RecordResponseDto>>> BulkCreateRecords(
        long organizationId,
        long projectId,
        [FromQuery] long dataSourceId,
        [FromBody] List<CreateRecordRequestDto> records,
        [FromQuery] List<long>? sensitivityLabelIds = null)
    {
        var currentUserId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var isOrgAdmin = UserContextStorage.IsOrgAdmin;
        var isProjectAdmin = UserContextStorage.IsProjectAdmin;

        var newRecords = await _recordBusiness.BulkCreateRecords(
            currentUserId,
            organizationId,
            projectId,
            dataSourceId,
            records,
            sensitivityLabelIds,
            isSysAdmin,
            isOrgAdmin,
            isProjectAdmin);
        return Ok(newRecords);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record")]
    [Sensitivity("update record")]
    public async Task<ActionResult<RecordResponseDto>> UpdateRecord(
        long organizationId,
        long projectId,
        long recordId,
        [FromBody] UpdateRecordRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var isOrgAdmin = UserContextStorage.IsOrgAdmin;
        var isProjectAdmin = UserContextStorage.IsProjectAdmin;
        var updated = await _recordBusiness.UpdateRecord(currentUserId, organizationId, projectId, recordId, dto, isSysAdmin,
            isOrgAdmin,
            isProjectAdmin);
        return Ok(updated);
    }



    /// <summary>
    ///     Delete a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record to delete</param>
    /// <returns>True if the record was successfully deleted</returns>
    [HttpDelete("{recordId:long}", Name = "api_delete_a_record")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "record")]
    [Sensitivity("delete record")]
    public async Task<IActionResult> DeleteRecord(
        long organizationId,
        long projectId,
        long recordId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _recordBusiness.DeleteRecord(currentUserId, organizationId, projectId, recordId);
        return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record to archive or unarchive</param>
    /// <param name="archive">True to archive the record, false to unarchive it.</param>
    /// <returns>True if the record was successfully archived or unarchived.</returns>
    [HttpPatch("{recordId:long}", Name = "api_archive_record")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record")]
    [Sensitivity("update record")]
    public async Task<IActionResult> ArchiveRecord(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] bool archive)
    {
        var currentUserId = UserContextStorage.UserId;
        if (archive)
        {
            var responseA = await _recordBusiness.ArchiveRecord(currentUserId, organizationId, projectId, recordId);
            return Ok(responseA);
        }

        var responseB = await _recordBusiness.UnarchiveRecord(currentUserId, organizationId, projectId, recordId);
        return Ok(responseB);
    }



    /// <summary>
    ///     Attach a Tag to a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="tagId">The ID of the tag to attach</param>
    /// <returns>True if the tag was successfully attached to the record.</returns>
    [HttpPost("{recordId:long}/tags", Name = "api_attach_a_tag")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record")]
    [Auth("read", "tag")]
    [Sensitivity("update record")]
    public async Task<IActionResult> AttachTag(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] long tagId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _recordBusiness.AttachTag(currentUserId, organizationId, projectId, recordId, tagId);
        return Ok(response);
    }



    /// <summary>
    ///     Unattach a Tag from a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="tagId">The ID of the tag to unattach</param>
    /// <returns>True if the tag was successfully unattached from the record.</returns>
    [HttpDelete("{recordId:long}/tags", Name = "api_unattach_a_tag")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record")]
    [Auth("read", "tag")]
    [Sensitivity("update record")]
    public async Task<IActionResult> UnattachTag(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] long tagId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _recordBusiness.UnattachTag(currentUserId, organizationId, projectId, recordId, tagId);
        return Ok(response);
    }



    /// <summary>
    ///     Bulk Attach Tags to Records
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="dtos">List of record/tag pairs to attach</param>
    /// <returns>True if the tags were successfully attached to the records.</returns>
    [HttpPost("bulk-attach-tags-to-records", Name = "api_bulk_attach_tags_to_records")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record")]
    [Auth("read", "tag")]
    public async Task<IActionResult> BulkAttachTagsToRecords(
        long organizationId,
        long projectId,
        [FromBody] List<RecordTagLinkDto> dtos)
    {
        var currentUserId = UserContextStorage.UserId;

        var response = await _recordBusiness.BulkAttachTags(currentUserId, organizationId, projectId, dtos);

        return Ok(response);
    }



    /// <summary>
    ///     Bulk Unattach Tags From Records
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="dtos">List of record/tag pairs to unattach</param>
    /// <returns>True if the tags were successfully unattached from the records.</returns>
    [HttpPost("bulk-unattach-tags-from-records", Name = "api_bulk_unattach_tags_from_records")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record")]
    [Auth("read", "tag")]
    public async Task<IActionResult> BulkUnattachTagsFromRecords(
        long organizationId,
        long projectId,
        [FromBody] List<RecordTagLinkDto> dtos)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _recordBusiness.BulkUnattachTags(currentUserId, organizationId, projectId, dtos);
        return Ok(response);
    }



    /// <summary>
    ///     Attach a Sensitivity Label to a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="sensitivityLabelId">The ID of the label to attach</param>
    /// <returns>True if the label was successfully attached to the record.</returns>
    [HttpPost("{recordId:long}/sensitivity-labels", Name = "api_attach_sensitivity_label")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record")]
    [Auth("read", "sensitivity_label")]
    [Sensitivity("update record")]
    public async Task<IActionResult> AttachSensitivityLabel(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] long sensitivityLabelId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _recordBusiness.AttachLabel(currentUserId, organizationId, projectId, recordId, sensitivityLabelId);
        return Ok(response);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record")]
    [Auth("read", "sensitivity_label")]
    [Sensitivity("update record")]
    public async Task<IActionResult> BulkAttachSensitivityLabels(
        long organizationId,
        long projectId,
        [FromQuery] List<long> recordIds,
        [FromQuery] List<long> sensitivityLabelIds)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _recordBusiness.BulkAttachLabels(currentUserId, organizationId, projectId, recordIds,
            sensitivityLabelIds);
        return Ok(response);
    }



    /// <summary>
    ///     Unattach a sensitivity label from a Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record</param>
    /// <param name="sensitivityLabelId">The ID of the label to unattach</param>
    /// <returns>True if the label was successfully unattached from the record.</returns>
    [HttpDelete("{recordId:long}/sensitivity-labels", Name = "api_unattach_sensitivity-label")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "record")]
    [Auth("read", "sensitivity_label")]
    [Sensitivity("update record")]
    public async Task<IActionResult> UnattachSensitivityLabel(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] long sensitivityLabelId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _recordBusiness.UnattachLabel(currentUserId, organizationId, projectId, recordId, sensitivityLabelId);
        return Ok(response);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
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
        var currentUserId = UserContextStorage.UserId;
        var edges = await _graphBusiness.GetEdgesByRecord(
            currentUserId, organizationId, projectId, recordId, isOrigin, page, pageSize);
        return Ok(edges);
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Auth("read", "edge")]
    public async Task<ActionResult<GraphResponse>> GetGraphDataForRecord(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] int depth)
    {
        bool isAdmin = UserContextStorage.IsSysAdmin || UserContextStorage.IsOrgAdmin || UserContextStorage.IsProjectAdmin;
        var edges = await _graphBusiness.GetGraphDataForRecord(
            organizationId, projectId, recordId, UserContextStorage.UserId, depth, isAdmin);
        return Ok(edges);
    }
}