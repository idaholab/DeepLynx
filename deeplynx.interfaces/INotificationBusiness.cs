namespace deeplynx.interfaces;

public interface INotificationBusiness
{
    Task SendEventNotification(EventResponseDto eventDto);

    Task SendBulkEventNotifications(List<EventResponseDto> eventDtos);

    Task<bool> SendEmail(string toEmail, string? name, bool isNewUser = true, long? organizationId = null,
        long? projectId = null);
}