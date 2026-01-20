using deeplynx.models;
namespace deeplynx.interfaces;

public interface IEventBusiness
{
    Task<List<EventResponseDto>> GetAllEvents(long? projectId, long? organizationId);
    Task<PaginatedResponse<EventResponseDto>> QueryAllEvents(long organizationId, long? projectId, EventsQueryRequestDto? filterDto);
    Task<PaginatedResponse<EventResponseDto>> QueryAuthorizedEvents (long currentUserId, long organizationId, long[] projectIds, EventsQueryRequestDto? filterDto);
    Task<PaginatedResponse<EventResponseDto>> QueryEventsBySubscriptions(long currentUserId, long organizationId, long? projectId,
    EventsQueryRequestDto? queryDto);
    Task<EventResponseDto> CreateEvent(long currentUserId, long organizationId, long? projectId, CreateEventRequestDto dto, long? count = 1);
}
