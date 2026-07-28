using Asp.Versioning;
using deeplynx.helpers.Context;
using Microsoft.AspNetCore.Mvc;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers
{
    /// <summary>
    /// Controller for fetching events that match user subscriptions.
    /// </summary>
    /// <remarks>
    /// This controller provides an endpoint retrieve project events that match the user's subscriptions.
    /// </remarks>
    [ApiController]
    [ApiVersion(1, Deprecated = true)]
    [ApiVersion(2)]
    [Route("events")]
    [Authorize]
    public class EventController : ControllerBase // Inherit from ControllerBase
    {
        private readonly IEventBusiness _eventBusiness;
        private readonly ILogger<EventController> _logger;

        public EventController(IEventBusiness eventBusiness, ILogger<EventController> logger)
        {
            _eventBusiness = eventBusiness;
            _logger = logger;
        }

        /// <summary>
        /// Get All Events
        /// </summary>
        /// <param name="projectId">Optional filter to only include events matching the projectId</param>
        /// <param name="organizationId">Optional filter </param>
        /// <returns> Paginated event response DTOs</returns>
        [HttpGet("GetAllEvents", Name = "api_get_all_events")]
        [MapToApiVersion(1)]
        public async Task<ActionResult<PaginatedResponse<EventResponseDto>>> GetAllEvents(
            [FromQuery] long? projectId,
            [FromQuery] long? organizationId
        )
        {
            try
            {
                var events = await _eventBusiness.GetAllEvents(projectId, organizationId);
                return Ok(events);
            }
            catch (Exception e)
            {
                var message = $"An unexpected error occurred while fetching events: {e}";
                _logger.LogError(message);
                return StatusCode(StatusCodes.Status500InternalServerError, message);
            }
        }
        
        /// <summary>
        /// Get All Events
        /// </summary>
        /// <param name="projectId">Optional filter to only include events matching the projectId</param>
        /// <param name="organizationId">Optional filter </param>
        /// <returns> Paginated event response DTOs</returns>
        [HttpGet("GetAllEvents", Name = "api_get_all_events")]
        [MapToApiVersion(2)]
        [Badge("V2", BadgePosition.Before, "#72e6a1")]
        public async Task<ActionResult<PaginatedResponse<EventResponseDto>>> GetAllEventsV2(
            [FromQuery] long? projectId,
            [FromQuery] long? organizationId
        )
        {
            var events = await _eventBusiness.GetAllEvents(projectId, organizationId);
            return Ok(events);
        }

        /// <summary>
        /// QueryEvents (Paginated)
        /// </summary>
        /// <param name="organizationId">The id of the organization</param>.
        /// <param name="projectId">The id of the project</param>.
        /// <param name="queryDto">Filter criteria and pagination parameters</param>.
        /// <returns>Paginated event response DTOs</returns>
        [HttpGet("QueryEvents", Name = "api_query_events_paginated")]
        [MapToApiVersion(1)]
        public async Task<ActionResult<PaginatedResponse<EventResponseDto>>> QueryEvents(
            long organizationId,
            long projectId,
            [FromQuery] EventsQueryRequestDto? queryDto
        )
        {
            try
            {
                var events = await _eventBusiness.QueryAllEvents(organizationId, projectId, queryDto);
                return Ok(events);
            }
            catch (Exception e)
            {
                var message = $"An unexpected error occurred while fetching events: {e}";
                _logger.LogError(message);
                return StatusCode(StatusCodes.Status500InternalServerError, message);
            }
        }
        
        /// <summary>
        /// QueryEvents (Paginated)
        /// </summary>
        /// <param name="organizationId">The id of the organization</param>.
        /// <param name="projectId">The id of the project</param>.
        /// <param name="queryDto">Filter criteria and pagination parameters</param>.
        /// <returns>Paginated event response DTOs</returns>
        [HttpGet("QueryEvents", Name = "api_query_events_paginated")]
        [MapToApiVersion(2)]
        [Badge("V2", BadgePosition.Before, "#72e6a1")]
        public async Task<ActionResult<PaginatedResponse<EventResponseDto>>> QueryEventsV2(
            long organizationId,
            long projectId,
            [FromQuery] EventsQueryRequestDto? queryDto
        )
        {
            var events = await _eventBusiness.QueryAllEvents(organizationId, projectId, queryDto);
            return Ok(events);
        }

        /// <summary>
        /// Query All Events The requesting user is Authorized to See (Paginated).
        /// </summary>
        /// <returns> Paginated event response DTOs</returns>
        [HttpGet("QueryAuthorizedEvents/{organizationId:long}", Name = "api_query_authorized_events")]
        [MapToApiVersion(1)]
        public async Task<ActionResult<IEnumerable<EventResponseDto>>> QueryAuthorizedEvents(
            long organizationId,
            [FromQuery] long[] projectIds,
            [FromQuery] EventsQueryRequestDto? queryDto)
        {
            try
            {
                var currentUserId = UserContextStorage.UserId;
                var events =
                    await _eventBusiness.QueryAuthorizedEvents(currentUserId, organizationId, projectIds, queryDto);
                return Ok(events);
            }
            catch (Exception e)
            {
                var message = $"An unexpected error occurred while fetching events: {e}";
                _logger.LogError(message);
                return StatusCode(StatusCodes.Status500InternalServerError, message);
            }
        }
        
        /// <summary>
        /// Query All Events The requesting user is Authorized to See (Paginated).
        /// </summary>
        /// <returns> Paginated event response DTOs</returns>
        [HttpGet("QueryAuthorizedEvents/{organizationId:long}", Name = "api_query_authorized_events")]
        [MapToApiVersion(2)]
        [Badge("V2", BadgePosition.Before, "#72e6a1")]
        public async Task<ActionResult<IEnumerable<EventResponseDto>>> QueryAuthorizedEventsV2(
            long organizationId,
            [FromQuery] long[] projectIds,
            [FromQuery] EventsQueryRequestDto? queryDto)
        {
            var currentUserId = UserContextStorage.UserId;
            var events =
                await _eventBusiness.QueryAuthorizedEvents(currentUserId, organizationId, projectIds, queryDto);
            return Ok(events);
        }

        /// <summary>
        /// Query Events by Subscriptions 
        /// </summary>
        /// <param name="organizationId">The ID of the organization</param>
        /// <param name="projectId">The ID of the project to which the events belong</param>
        /// <param name="queryDto">The DTO containing the optional filters</param>
        /// <returns> Paginated event response DTOs</returns>
        [HttpGet("QueryEventsBySubscriptions", Name = "api_query_events_by_subscriptions")]
        [MapToApiVersion(1)]
        public async Task<ActionResult<IEnumerable<EventResponseDto>>> QueryEventsBySubscriptions(
            long organizationId,
            long projectId,
            [FromQuery] EventsQueryRequestDto? queryDto)
        {
            try
            {
                var currentUserId = UserContextStorage.UserId;
                var events =
                    await _eventBusiness.QueryEventsBySubscriptions(currentUserId, organizationId, projectId, queryDto);
                return Ok(events);
            }
            catch (Exception e)
            {
                var message = $"An unexpected error occurred while fetching events: {e}";
                _logger.LogError(message);
                return StatusCode(StatusCodes.Status500InternalServerError, message);
            }
        }
        
        /// <summary>
        /// Query Events by Subscriptions 
        /// </summary>
        /// <param name="organizationId">The ID of the organization</param>
        /// <param name="projectId">The ID of the project to which the events belong</param>
        /// <param name="queryDto">The DTO containing the optional filters</param>
        /// <returns> Paginated event response DTOs</returns>
        [HttpGet("QueryEventsBySubscriptions", Name = "api_query_events_by_subscriptions")]
        [MapToApiVersion(2)]
        [Badge("V2", BadgePosition.Before, "#72e6a1")]
        public async Task<ActionResult<IEnumerable<EventResponseDto>>> QueryEventsBySubscriptionsV2(
            long organizationId,
            long projectId,
            [FromQuery] EventsQueryRequestDto? queryDto)
        {
            var currentUserId = UserContextStorage.UserId;
            var events =
                await _eventBusiness.QueryEventsBySubscriptions(currentUserId, organizationId, projectId, queryDto);
            return Ok(events);
        }
    }
}
