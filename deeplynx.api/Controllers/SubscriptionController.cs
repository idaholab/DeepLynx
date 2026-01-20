// using Microsoft.AspNetCore.Mvc;
// using deeplynx.interfaces;
// using deeplynx.models;
// using Microsoft.AspNetCore.Authorization;
// using deeplynx.helpers.Context;
//
// namespace deeplynx.api.Controllers
// {
//     /// <summary>
//     /// Controller for managing subscriptions.
//     /// </summary>
//     /// <remarks>
//     /// This controller provides endpoints to create, update, delete, and retrieve subscription information
//     /// at both organization and project levels.
//     /// </remarks>
//     [ApiController]
//     [Route("subscriptions")]
//     [Authorize]
//TODO: Add tag for openapi 
//     public class SubscriptionController : ControllerBase
//     {
//         private readonly ISubscriptionBusiness _subscriptionBusiness;
//         private readonly ILogger<SubscriptionController> _logger;
//
//         /// <summary>
//         /// Initializes a new instance of the <see cref="SubscriptionController"/> class.
//         /// </summary>
//         /// <param name="subscriptionBusiness">The business logic interface for handling subscription operations.</param>
//         /// <param name="logger">Error/Info logging interface for database log table.</param>
//         public SubscriptionController(ISubscriptionBusiness subscriptionBusiness, ILogger<SubscriptionController> logger)
//         {
//             _subscriptionBusiness = subscriptionBusiness;
//             _logger = logger;
//         }
//
//         /// <summary>
//         /// Get all subscriptions for the current user at organization or project level
//         /// </summary>
//         /// <param name="organizationId">The ID of the organization</param>
//         /// <param name="projectId">Optional project ID to scope subscriptions to a specific project</param>
//         /// <param name="hideArchived">Flag indicating whether to hide archived subscriptions from the result</param>
//         /// <returns>List of subscription response DTOs</returns>
//         [HttpGet("GetAllSubscriptions", Name = "api_get_all_subscriptions")]
//         public async Task<ActionResult<IEnumerable<SubscriptionResponseDto>>> GetAllSubscriptions(
//             [FromRoute] long organizationId,
//             [FromQuery] long? projectId = null,
//             [FromQuery] bool hideArchived = true)
//         {
//             try
//             {
//                 var currentUserId = UserContextStorage.UserId;
//                 var subscriptions = await _subscriptionBusiness.GetAllSubscriptions(currentUserId, organizationId, hideArchived, projectId);
//                 return Ok(subscriptions);
//             }
//             catch (Exception exc)
//             {
//                 var scope = projectId.HasValue ? "project" : "organization";
//                 var message = $"An unexpected error occurred while fetching all {scope} subscriptions: {exc}";
//                 _logger.LogError(message);
//                 return StatusCode(StatusCodes.Status500InternalServerError, message);
//             }
//         }
//
//         /// <summary>
//         /// Get a specific subscription by ID
//         /// </summary>
//         /// <param name="organizationId">The ID of the organization</param>
//         /// <param name="subscriptionId">The ID of the subscription to retrieve</param>
//         /// <param name="projectId">Optional project ID to scope the subscription to a specific project</param>
//         /// <param name="hideArchived">Flag indicating whether to hide archived subscriptions from the result</param>
//         /// <returns>Subscription response DTO</returns>
//         [HttpGet("GetSubscription", Name = "api_get_subscription")]
//         public async Task<ActionResult<SubscriptionResponseDto>> GetSubscription(
//             [FromRoute] long organizationId,
//             [FromQuery] long subscriptionId,
//             [FromQuery] long? projectId = null,
//             [FromQuery] bool hideArchived = true)
//         {
//             try
//             {
//                 var currentUserId = UserContextStorage.UserId;
//                 var subscription = await _subscriptionBusiness.GetSubscription(currentUserId, subscriptionId, organizationId, hideArchived, projectId);
//                 return Ok(subscription);
//             }
//             catch (KeyNotFoundException)
//             {
//                 return NotFound($"Subscription with id {subscriptionId} not found");
//             }
//             catch (UnauthorizedAccessException ex)
//             {
//                 return Unauthorized(ex.Message);
//             }
//             catch (Exception exc)
//             {
//                 var message = $"An unexpected error occurred while fetching subscription {subscriptionId}: {exc}";
//                 _logger.LogError(message);
//                 return StatusCode(StatusCodes.Status500InternalServerError, message);
//             }
//         }
//
//         /// <summary>
//         /// Create many organization-level subscriptions
//         /// </summary>
//         /// <param name="organizationId">The ID of the organization</param>
//         /// <param name="subscriptions">List of request DTOs for subscriptions</param>
//         /// <returns>List of created subscription response DTOs</returns>
//         [HttpPost("BulkCreateSubscriptionsByOrg", Name = "api_bulk_create_subscriptions_org")]
//         public async Task<ActionResult<List<SubscriptionResponseDto>>> BulkCreateSubscriptionsForOrganization(
//             [FromRoute] long organizationId,
//             [FromBody] List<CreateSubscriptionRequestDto> subscriptions)
//         {
//             try
//             {
//                 var currentUserId = UserContextStorage.UserId;
//                 var newSubscriptions = await _subscriptionBusiness.BulkCreateSubscriptions(currentUserId, organizationId, subscriptions, null);
//                 return Ok(newSubscriptions);
//             }
//             catch (Exception exc)
//             {
//                 var message = $"An unexpected error occurred while creating organization subscriptions: {exc}";
//                 _logger.LogError(message);
//                 return StatusCode(StatusCodes.Status500InternalServerError, message);
//             }
//         }
//
//         /// <summary>
//         /// Create many project-level subscriptions
//         /// </summary>
//         /// <param name="organizationId">The ID of the organization</param>
//         /// <param name="projectId">The ID of the project</param>
//         /// <param name="subscriptions">List of request DTOs for subscriptions</param>
//         /// <returns>List of created subscription response DTOs</returns>
//         [HttpPost("{projectId}/BulkCreateSubscriptionsByProject", Name = "api_bulk_create_subscriptions_project")]
//         public async Task<ActionResult<List<SubscriptionResponseDto>>> BulkCreateSubscriptionsForProject(
//             [FromRoute] long organizationId,
//             [FromRoute] long projectId,
//             [FromBody] List<CreateSubscriptionRequestDto> subscriptions)
//         {
//             try
//             {
//                 var currentUserId = UserContextStorage.UserId;
//                 var newSubscriptions = await _subscriptionBusiness.BulkCreateSubscriptions(currentUserId, organizationId, subscriptions, projectId);
//                 return Ok(newSubscriptions);
//             }
//             catch (Exception exc)
//             {
//                 var message = $"An unexpected error occurred while creating project subscriptions: {exc}";
//                 _logger.LogError(message);
//                 return StatusCode(StatusCodes.Status500InternalServerError, message);
//             }
//         }
//
//         /// <summary>
//         /// Update many subscriptions
//         /// </summary>
//         /// <param name="organizationId">The ID of the organization</param>
//         /// <param name="subscriptions">List of request DTOs for subscriptions</param>
//         /// <param name="projectId">Optional project ID to scope subscriptions to a specific project</param>
//         /// <returns>List of updated subscription response DTOs</returns>
//         [HttpPut("BulkUpdateSubscriptions", Name = "api_bulk_update_subscriptions")]
//         public async Task<ActionResult<List<SubscriptionResponseDto>>> BulkUpdateSubscriptions(
//             [FromRoute] long organizationId,
//             [FromBody] List<UpdateSubscriptionRequestDto> subscriptions,
//             [FromQuery] long? projectId = null)
//         {
//             try
//             {
//                 var currentUserId = UserContextStorage.UserId;
//                 var updatedSubscriptions = await _subscriptionBusiness.BulkUpdateSubscriptions(currentUserId, organizationId, subscriptions, projectId);
//                 return Ok(updatedSubscriptions);
//             }
//             catch (InvalidOperationException ex)
//             {
//                 return BadRequest(ex.Message);
//             }
//             catch (Exception exc)
//             {
//                 var scope = projectId.HasValue ? "project" : "organization";
//                 var message = $"An unexpected error occurred while updating {scope} subscriptions: {exc}";
//                 _logger.LogError(message);
//                 return StatusCode(StatusCodes.Status500InternalServerError, message);
//             }
//         }
//
//         /// <summary>
//         /// Delete many subscriptions
//         /// </summary>
//         /// <param name="organizationId">The ID of the organization</param>
//         /// <param name="subscriptionIds">List of subscription IDs to delete</param>
//         /// <param name="projectId">Optional project ID to scope subscriptions to a specific project</param>
//         /// <returns>A message stating the subscriptions were successfully deleted</returns>
//         [HttpDelete("BulkDeleteSubscriptions", Name = "api_bulk_delete_subscriptions")]
//         public async Task<IActionResult> BulkDeleteSubscriptions(
//             [FromRoute] long organizationId,
//             [FromBody] List<long> subscriptionIds,
//             [FromQuery] long? projectId = null)
//         {
//             try
//             {
//                 var currentUserId = UserContextStorage.UserId;
//                 await _subscriptionBusiness.BulkDeleteSubscriptions(currentUserId, organizationId, subscriptionIds, projectId);
//                 var scope = projectId.HasValue ? "project" : "organization";
//                 return Ok(new { message = $"Deleted {scope} subscriptions" });
//             }
//             catch (InvalidOperationException ex)
//             {
//                 return BadRequest(ex.Message);
//             }
//             catch (Exception exc)
//             {
//                 var scope = projectId.HasValue ? "project" : "organization";
//                 var message = $"An error occurred while deleting {scope} subscriptions: {exc}";
//                 _logger.LogError(message);
//                 return StatusCode(StatusCodes.Status500InternalServerError, message);
//             }
//         }
//
//         /// <summary>
//         /// Archive many subscriptions
//         /// </summary>
//         /// <param name="organizationId">The ID of the organization</param>
//         /// <param name="subscriptionIds">List of subscription IDs to archive</param>
//         /// <param name="projectId">Optional project ID to scope subscriptions to a specific project</param>
//         /// <returns>A message stating the subscriptions were successfully archived</returns>
//         [HttpPut("BulkArchiveSubscriptions", Name = "api_bulk_archive_subscriptions")]
//         public async Task<IActionResult> BulkArchiveSubscriptions(
//             [FromRoute] long organizationId,
//             [FromBody] List<long> subscriptionIds,
//             [FromQuery] long? projectId = null)
//         {
//             try
//             {
//                 var currentUserId = UserContextStorage.UserId;
//                 await _subscriptionBusiness.BulkArchiveSubscriptions(currentUserId, organizationId, subscriptionIds, projectId);
//                 var scope = projectId.HasValue ? "project" : "organization";
//                 return Ok(new { message = $"Archived {scope} subscriptions" });
//             }
//             catch (InvalidOperationException ex)
//             {
//                 return BadRequest(ex.Message);
//             }
//             catch (Exception exc)
//             {
//                 var scope = projectId.HasValue ? "project" : "organization";
//                 var message = $"An error occurred while archiving {scope} subscriptions: {exc}";
//                 _logger.LogError(message);
//                 return StatusCode(StatusCodes.Status500InternalServerError, message);
//             }
//         }
//
//         /// <summary>
//         /// Unarchive many subscriptions
//         /// </summary>
//         /// <param name="organizationId">The ID of the organization</param>
//         /// <param name="subscriptionIds">List of subscription IDs to unarchive</param>
//         /// <param name="projectId">Optional project ID to scope subscriptions to a specific project</param>
//         /// <returns>A message stating the subscriptions were successfully unarchived</returns>
//         [HttpPut("BulkUnarchiveSubscriptions", Name = "api_bulk_unarchive_subscriptions")]
//         public async Task<IActionResult> BulkUnarchiveSubscriptions(
//             [FromRoute] long organizationId,
//             [FromBody] List<long> subscriptionIds,
//             [FromQuery] long? projectId = null)
//         {
//             try
//             {
//                 var currentUserId = UserContextStorage.UserId;
//                 await _subscriptionBusiness.BulkUnarchiveSubscriptions(currentUserId, organizationId, subscriptionIds, projectId);
//                 var scope = projectId.HasValue ? "project" : "organization";
//                 return Ok(new { message = $"Unarchived {scope} subscriptions" });
//             }
//             catch (InvalidOperationException ex)
//             {
//                 return BadRequest(ex.Message);
//             }
//             catch (Exception exc)
//             {
//                 var scope = projectId.HasValue ? "project" : "organization";
//                 var message = $"An error occurred while unarchiving {scope} subscriptions: {exc}";
//                 _logger.LogError(message);
//                 return StatusCode(StatusCodes.Status500InternalServerError, message);
//             }
//         }
//     }
// }