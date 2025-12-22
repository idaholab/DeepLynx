using System.Net;
using System.Net.Mail;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using Microsoft.Extensions.Logging;
using deeplynx.helpers.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;
/// <summary>
/// Handles all notification operations including SignalR real-time notifications
/// </summary>
public class NotificationBusiness : INotificationBusiness
{
    private readonly DeeplynxContext _context;
    private readonly ILogger<NotificationBusiness> _logger;
    private readonly IHubContext<EventNotificationHub> _hubContext;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationBusiness"/> class.
    /// </summary>
    /// <param name="context">The database context to be used for class operations</param>
    /// <param name="logger">Logging</param>
    /// <param name="hubContext">SignalR hub context for sending notifications</param>
    public NotificationBusiness(
        DeeplynxContext context, 
        ILogger<NotificationBusiness> logger,
        IHubContext<EventNotificationHub> hubContext
    )
    {
        _logger = logger;
        _context = context;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Sends event notification to all users subscribed to this specific event
    /// </summary>
    /// <param name="eventDto">The event to send notifications for</param>
    public async Task SendEventNotification(EventResponseDto eventDto)
    {
        if (eventDto == null)
        {
            _logger.LogWarning("Attempted to send notification for null event");
            return;
        }

        try
        {
            // Get all users subscribed to this event
            var subscribedUserIds = await GetSubscribedUserIdsForEvent(eventDto);
            
            if (!subscribedUserIds.Any())
            {
                _logger.LogDebug("No users subscribed to event {EventId} for project {ProjectId}", 
                    eventDto.Id, eventDto.ProjectId);
                return;
            }

            // Serialize the event to JSON
            var jsonResponse = System.Text.Json.JsonSerializer.Serialize(eventDto);

            // Send notification to each subscribed user's group
            var notificationTasks = subscribedUserIds.Select(userId => 
                SendToUserGroup(userId, jsonResponse, eventDto.Id)
            );

            await Task.WhenAll(notificationTasks);
            
            _logger.LogInformation(
                "Successfully sent notifications for event {EventId} to {UserCount} users", 
                eventDto.Id, 
                subscribedUserIds.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending event notification for event {EventId}", eventDto.Id);
            throw;
        }
    }

    /// <summary>
    /// Sends bulk event notifications to subscribed users
    /// </summary>
    /// <param name="eventDtos">List of events to send notifications for</param>
    public async Task SendBulkEventNotifications(List<EventResponseDto> eventDtos)
    {
        if (eventDtos == null || !eventDtos.Any())
        {
            _logger.LogWarning("Attempted to send bulk notifications for empty or null event list");
            return;
        }

        try
        {
            // Serialize each event ONCE
            var eventJsonCache = eventDtos.ToDictionary(
                e => e,
                e => System.Text.Json.JsonSerializer.Serialize(e)
            );

            // Single database query for all subscriptions
            var eventSubscriptionsMap = await GetSubscribedUserIdsForManyEvents(eventDtos);

            // Group events by subscribed users
            var userEventMap = new Dictionary<long, List<EventResponseDto>>();

            foreach (var eventDto in eventDtos)
            {
                var subscribedUserIds = eventSubscriptionsMap[eventDto.Id];
            
                foreach (var userId in subscribedUserIds)
                {
                    if (!userEventMap.ContainsKey(userId))
                    {
                        userEventMap[userId] = new List<EventResponseDto>();
                    }
                    userEventMap[userId].Add(eventDto);
                }
            }

            if (!userEventMap.Any())
            {
                _logger.LogDebug("No users subscribed to any of the {EventCount} events", eventDtos.Count);
                return;
            }

            // Send all notifications
            var notificationTasks = userEventMap.Select(kvp => 
                SendEventsToUser(kvp.Key, kvp.Value, eventJsonCache)
            );

            await Task.WhenAll(notificationTasks);
        
            _logger.LogInformation(
                "Successfully sent bulk notifications for {EventCount} events to {UserCount} users", 
                eventDtos.Count, 
                userEventMap.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk event notifications");
            throw;
        }
    }

    private async Task SendEventsToUser(
        long userId, 
        List<EventResponseDto> events, 
        Dictionary<EventResponseDto, string> eventJsonCache)
    {
        try
        {
            // Send all events to this user in parallel using cached JSON
            var notificationTasks = events.Select(eventDto =>
                SendToUserGroup(userId, eventJsonCache[eventDto], eventDto.Id)
            );

            await Task.WhenAll(notificationTasks);
            
            _logger.LogDebug("Sent {EventCount} notifications to user {UserId}", events.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending events to user {UserId}", userId);
        }
    }

    /// <summary>
    /// Sends a notification message to a specific user's SignalR group
    /// </summary>
    /// <param name="userId">The user ID to send to</param>
    /// <param name="message">The serialized JSON message</param>
    /// <param name="eventId">The event ID for logging purposes</param>
    private async Task SendToUserGroup(long userId, string message, long eventId)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("ReceiveNotification", message);
            
            _logger.LogTrace("Sent notification for event {EventId} to user {UserId}", eventId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to send notification for event {EventId} to user {UserId}", 
                eventId, userId);
            // Don't throw - allow other notifications to continue
        }
    }

    /// <summary>
    /// Gets all user IDs that are subscribed to a specific event based on subscription rules
    /// </summary>
    /// <param name="eventDto">The event to check subscriptions for</param>
    /// <returns>List of user IDs subscribed to this event</returns>
    private async Task<List<long>> GetSubscribedUserIdsForEvent(EventResponseDto eventDto)
    {
        try
        {
            var subscribedUserIds = await _context.Set<Subscription>()
                .Where(s => s.ProjectId == eventDto.ProjectId)
                .Where(s => 
                    (s.EntityId == eventDto.EntityId || s.EntityId == null) &&
                    (s.EntityType == eventDto.EntityType || s.EntityType == null) &&
                    (s.DataSourceId == eventDto.DataSourceId || s.DataSourceId == null) &&
                    (s.Operation == eventDto.Operation || s.Operation == null)
                )
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync();

            return subscribedUserIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error retrieving subscribed users for event {EventId} in project {ProjectId}", 
                eventDto.Id, eventDto.ProjectId);
            return new List<long>();
        }
    }
    
    /// <summary>
    /// Gets all user IDs subscribed to any of the provided events (optimized for bulk)
    /// </summary>
    /// <param name="eventDtos">Events to check subscriptions for</param>
    /// <returns>Dictionary mapping each event ID to its list of subscribed user IDs</returns>
    private async Task<Dictionary<long, List<long>>> GetSubscribedUserIdsForManyEvents(
        List<EventResponseDto> eventDtos)
    {
        if (!eventDtos.Any())
        {
            return new Dictionary<long, List<long>>();
        }

        var projectIds = eventDtos.Select(e => e.ProjectId).Distinct().ToList();

        // Fetch ALL relevant subscriptions in ONE query
        var allSubscriptions = await _context.Set<Subscription>()
            .Where(s => projectIds.Contains(s.ProjectId))
            .ToListAsync();

        // Match subscriptions to events in memory
        var result = new Dictionary<long, List<long>>();

        foreach (var eventDto in eventDtos)
        {
            var matchingUserIds = allSubscriptions
                .Where(s => s.ProjectId == eventDto.ProjectId)
                .Where(s => 
                    (s.EntityId == eventDto.EntityId || s.EntityId == null) &&
                    (s.EntityType == eventDto.EntityType || s.EntityType == null) &&
                    (s.DataSourceId == eventDto.DataSourceId || s.DataSourceId == null) &&
                    (s.Operation == eventDto.Operation || s.Operation == null)
                )
                .Select(s => s.UserId)
                .Distinct()
                .ToList();

            result[eventDto.Id] = matchingUserIds;
        }

        return result;
    }

    /// <summary>
    /// Sends an email notification
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="name">Recipient name (optional, defaults to "User")</param>
    /// <returns>True if email was sent successfully, false otherwise</returns>
    public async Task<bool> SendEmail(string toEmail, string? name)
    {
       try
       {
        var smtpServer = Environment.GetEnvironmentVariable("SMTP_SERVER") ?? throw new InvalidOperationException("SMTP_SERVER environment variable is not set");

        var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT");
        if (!int.TryParse(smtpPortStr, out int smtpPort))
        {
            smtpPort = 587; //default
        }

        var fromEmail = Environment.GetEnvironmentVariable("FROM_EMAIL") ?? throw new InvalidOperationException("FROM_EMAIL environment variable is not set");

        var support = Environment.GetEnvironmentVariable("SUPPORT_EMAIL") ?? throw new InvalidOperationException("SUPPORT_EMAIL environment variable is not set");

        var emailPassword = "";

        var fromName = Environment.GetEnvironmentVariable("FROM_NAME");

        var url = Environment.GetEnvironmentVariable("INVITE_URL") ?? throw new InvalidOperationException("Invite URL environment variable is not set");;

        var enableSslStr = Environment.GetEnvironmentVariable("SMTP_ENABLE_SSL");
        bool.TryParse(enableSslStr, out bool enableSsl);

        string templateContent = @"<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
        <html xmlns=""http://www.w3.org/1999/xhtml"">
        <head>
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
            <meta name=""color-scheme"" content=""light dark"" />
            <meta name=""supported-color-schemes"" content=""light dark"" />
            <title>{{subject}}</title>
            <style type=""text/css"">
                @media (prefers-color-scheme: dark) {
                    .dark-bg { background-color: #1a1a1a !important; }
                    .dark-card { background-color: #2a2a2a !important; }
                    .dark-text { color: #ffffff !important; }
                    .dark-text-secondary { color: #cccccc !important; }
                    .dark-border { border-color: #444444 !important; }
                }
            </style>
        </head>
        <body style=""font-family: 'Roboto', Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px;"" class=""dark-bg"">
        <table style=""width: 100%; max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);"" cellpadding=""0"" cellspacing=""0"" class=""dark-card"">
            <tr>
                <td style=""background-color: #07519e; padding: 40px 30px; text-align: center; border-radius: 8px 8px 0 0;"">
                    <h1 style=""color: #ffffff; font-size: 24px; font-weight: bold; margin: 0;"">
                        <img src=""""
                             alt=""DeepLynx Nexus""
                             style=""width: 50%; height: auto;""/>
                    </h1>
                </td>
            </tr>
            <tr>
                <td style=""padding: 40px 30px;"">
                    <h2 style=""color: #333333; font-size: 20px; margin: 0 0 20px 0;"" class=""dark-text"">Hello {{name}}!</h2>
                    <p style=""color: #666666; font-size: 16px; line-height: 1.6; margin: 0 0 30px 0;"" class=""dark-text-secondary"">
                        You've been invited to join DeepLynx Nexus. Click the button below to get started.
                    </p>
                    <div style=""text-align: center; margin: 30px 0;"">
                        <a href=""{{url}}"" style=""display: inline-block; background-color: #07519e; color: #ffffff; padding: 12px 30px; text-decoration: none; border-radius: 6px; font-weight: bold;"">Accept Invitation</a>
                    </div>
                    <div style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #eeeeee;"" class=""dark-border"">
                        <p style=""color: #666666; font-size: 14px; margin: 0;"" class=""dark-text-secondary"">
                            Best regards,<br>
                            The DeepLynx Nexus Team
                        </p>
                    </div>
                    <div style=""margin-top: 30px; padding-top: 15px; border-top: 1px solid #eeeeee;"" class=""dark-border"">
                        <p style=""color: #999999; font-size: 12px; margin: 0; line-height: 1.4;"" class=""dark-text-secondary"">
                            This invitation was sent to {{email}}.<br>
                            If you believe you received this email in error, please contact our support team @
                            {{support}}.
                        </p>
                    </div>
                </td>
            </tr>
        </table>
        </body>
        </html>";

        templateContent = templateContent.Replace("{{name}}", name);
        templateContent = templateContent.Replace("{{email}}", toEmail);
        templateContent = templateContent.Replace("{{url}}", url);
        templateContent = templateContent.Replace("{{support}}", support);




        // Create message
        using var mailMessage = new MailMessage();
        mailMessage.From = new MailAddress(fromEmail, fromName);
        mailMessage.To.Add(toEmail);
        mailMessage.Subject = "DeepLynx Nexus Notification";
        mailMessage.Body = templateContent;
        mailMessage.IsBodyHtml = true;

        // Configure SMTP client
        using var smtpClient = new SmtpClient(smtpServer, smtpPort);
        smtpClient.EnableSsl = enableSsl;
        smtpClient.UseDefaultCredentials = false;
        smtpClient.Credentials = new NetworkCredential(fromEmail, emailPassword);

        // Send the email
        await smtpClient.SendMailAsync(mailMessage);

        return true;
       }
       catch (SmtpException smtpEx)
       {
           _logger.LogError(smtpEx, "SMTP error occurred while sending email to {ToEmail}: {ErrorMessage}", toEmail, smtpEx.Message);
           return false;
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Unexpected error occurred while sending email to {ToEmail}: {ErrorMessage}", toEmail, ex.Message);
           return false;
       }
    }

}