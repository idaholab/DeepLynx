using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using Asp.Versioning;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V1;

[Route("organizations/{organizationId:long}/projects/{projectId:long}/datasources/{dataSourceId:long}/metadata")]
[ApiController]
[ApiVersion(1, Deprecated = true)]
[Authorize]
public class MetadataController : ControllerBase
{
    private readonly ILogger<MetadataController> _logger;
    private readonly IMetadataBusiness _metadataBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MetadataController" /> class.
    /// </summary>
    /// <param name="metadataBusiness">The business logic interface for handling metadata operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public MetadataController(IMetadataBusiness metadataBusiness, ILogger<MetadataController> logger)
    {
        _metadataBusiness = metadataBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Parse Metadata from Raw JSON
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the metadata belongs.</param>
    /// <param name="projectId">The ID of the project to which the metadata belongs.</param>
    /// <param name="dataSourceId">The ID of the datasource from which the metadata was collected.</param>
    /// <param name="metadataRequestDto">The metadata data transfer object containing metadata details.</param>
    [HttpPost(Name = "api_create_metadata")]
    [Auth("write", "class")]
    [Auth("write", "relationship")]
    [Auth("write", "tag")]
    [Auth("write", "record")]
    [Auth("write", "edge")]
    public async Task<ActionResult<MetadataResponseDto>> CreateMetadata(
        long organizationId,
        long projectId,
        long dataSourceId,
        [FromBody] CreateMetadataRequestDto metadataRequestDto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var createdMetadata =
                await _metadataBusiness.CreateMetadata(currentUserId, projectId, organizationId, dataSourceId,
                    metadataRequestDto);
            return Ok(createdMetadata);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.Message);
            var message = $"An error occurred while parsing metadata: {exception}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }



    /// <summary>
    ///     Parse Metadata from a JSON File
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the metadata belongs.</param>
    /// <param name="projectId">The ID of the project to which the metadata belongs.</param>
    /// <param name="dataSourceId">The ID of the datasource from which the metadata was collected.</param>
    /// <param name="file">The .json file that contains the metadata.</param>
    [HttpPost("file", Name = "api_create_metadata_from_file")]
    [Auth("write", "class")]
    [Auth("write", "relationship")]
    [Auth("write", "tag")]
    [Auth("write", "record")]
    [Auth("write", "edge")]
    public async Task<ActionResult<MetadataResponseDto>> CreateMetadataFromFile(
        long organizationId,
        long projectId,
        long dataSourceId,
        IFormFile file)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var createdMetadata =
                await _metadataBusiness.CreateMetadataFromFile(currentUserId, projectId, organizationId, dataSourceId,
                    file);
            return Ok(createdMetadata);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.Message);
            var message = $"An error occurred while parsing metadata: {exception}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }


}
