using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

/// <summary>
///     Handles all notification operations including SignalR real-time notifications
/// </summary>
public class NotificationBusiness : INotificationBusiness
{
    private static readonly ConcurrentDictionary<string, string> _emailTemplateCache = new();

    private readonly DeeplynxContext _context;
    private readonly IHubContext<EventNotificationHub> _hubContext;
    private readonly ILogger<NotificationBusiness> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotificationBusiness" /> class.
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
    ///     Sends event notification to all users subscribed to this specific event
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
            var jsonResponse = JsonSerializer.Serialize(eventDto);

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
    ///     Sends bulk event notifications to subscribed users
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
                e => JsonSerializer.Serialize(e)
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
                    if (!userEventMap.ContainsKey(userId)) userEventMap[userId] = new List<EventResponseDto>();
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

    /// <summary>
    ///     Sends an email notification
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="name">Recipient name (optional, defaults to "User")</param>
    /// <param name="isNewUser">True if this is a new user invitation, false if adding existing user</param>
    /// <param name="organizationId">Organization ID for context (optional)</param>
    /// <param name="projectId">Project ID for context (optional)</param>
    /// <returns>True if email was sent successfully, false otherwise</returns>
    public async Task<bool> SendEmail(string toEmail, string? name, bool isNewUser = true, long? organizationId = null,
        long? projectId = null)
    {
        try
        {
            var smtpServer = GetRequiredEnvironmentVariable("SMTP_SERVER");
            var fromEmail = GetRequiredEnvironmentVariable("FROM_EMAIL");
            var support = GetRequiredEnvironmentVariable("SUPPORT_EMAIL");
            var fromName = GetRequiredEnvironmentVariable("FROM_NAME");
            var url = GetRequiredEnvironmentVariable("INVITE_URL");
            var enableSslStr = GetRequiredEnvironmentVariable("SMTP_ENABLE_SSL");
            bool.TryParse(enableSslStr, out var enableSsl);

            var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT");
            if (!int.TryParse(smtpPortStr, out var smtpPort)) smtpPort = 587; //default

            var emailCred = "";

            // Build the message based on context
            var message = await BuildInvitationMessage(isNewUser, organizationId, projectId);

            var templateContent = LoadEmailTemplate("invitation_email.html", new Dictionary<string, string>
            {
                ["name"] = name ?? "User",
                ["email"] = toEmail,
                ["url"] = url,
                ["support"] = support,
                ["message"] = message,
            });

            // Create message — inline logo as a LinkedResource referenced via cid:logo in the HTML
            using var mailMessage = new MailMessage();
            mailMessage.From = new MailAddress(fromEmail, fromName);
            mailMessage.To.Add(toEmail);
            mailMessage.Subject = "DeepLynx Nexus Notification";

            var htmlView = AlternateView.CreateAlternateViewFromString(
                templateContent, null, "text/html");
            var logoStream = typeof(NotificationBusiness).Assembly
                .GetManifestResourceStream("deeplynx.business.Templates.nexusWhite.png")
                ?? throw new InvalidOperationException("Embedded logo 'nexusWhite.png' not found.");
            var logo = new LinkedResource(logoStream, "image/png") { ContentId = "logo" };
            htmlView.LinkedResources.Add(logo);
            mailMessage.AlternateViews.Add(htmlView);

            // Configure SMTP client
            using var smtpClient = new SmtpClient(smtpServer, smtpPort);
            smtpClient.EnableSsl = enableSsl;
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new NetworkCredential(fromEmail, emailCred);

            // Send the email
            await smtpClient.SendMailAsync(mailMessage);

            return true;
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, "SMTP error occurred while sending email to {ToEmail}: {ErrorMessage}", toEmail,
                smtpEx.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while sending email to {ToEmail}: {ErrorMessage}", toEmail,
                ex.Message);
            return false;
        }
    }

    private async Task<string> BuildInvitationMessage(bool isNewUser, long? organizationId, long? projectId)
    {
        string? orgName = null;
        string? projectName = null;

        // Fetch organization name if provided
        if (organizationId.HasValue)
        {
            var org = await _context.Organizations
                .Where(o => o.Id == organizationId.Value)
                .Select(o => o.Name)
                .FirstOrDefaultAsync();
            orgName = org;
        }

        // Fetch project name if provided
        if (projectId.HasValue)
        {
            var project = await _context.Projects
                .Where(p => p.Id == projectId.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync();
            projectName = project;
        }

        // Build message based on context
        var action = isNewUser ? "invited to join" : "added to";

        if (!string.IsNullOrEmpty(projectName) && !string.IsNullOrEmpty(orgName))
            return
                $"You have been {action} the DeepLynx Nexus project <strong>{projectName}</strong> in the <strong>{orgName}</strong> organization. Click the button below to get started.";

        if (!string.IsNullOrEmpty(orgName))
            return
                $"You have been {action} the DeepLynx Nexus <strong>{orgName}</strong> organization. Click the button below to get started.";

        // Default message when no context is provided
        return "You've been invited to join DeepLynx Nexus. Click the button below to get started.";
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
    ///     Sends a notification message to a specific user's SignalR group
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
    ///     Gets all user IDs that are subscribed to a specific event based on subscription rules
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
    ///     Gets all user IDs subscribed to any of the provided events (optimized for bulk)
    /// </summary>
    /// <param name="eventDtos">Events to check subscriptions for</param>
    /// <returns>Dictionary mapping each event ID to its list of subscribed user IDs</returns>
    private async Task<Dictionary<long, List<long>>> GetSubscribedUserIdsForManyEvents(
        List<EventResponseDto> eventDtos)
    {
        if (!eventDtos.Any()) return new Dictionary<long, List<long>>();

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

    private static string GetRequiredEnvironmentVariable(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{variableName} environment variable is not set or is empty");
        return value;
    }

    /// <summary>
    ///     Loads an embedded email template by file name (e.g., "invitation_email.html") and substitutes
    ///     <c>{key}</c> placeholders with the provided values. Templates are cached after first load.
    /// </summary>
    private static string LoadEmailTemplate(string templateName, Dictionary<string, string> values)
    {
        var template = _emailTemplateCache.GetOrAdd(templateName, name =>
        {
            var resourceName = $"deeplynx.business.Templates.{name}";
            using var stream = typeof(NotificationBusiness).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Email template '{name}' not found as embedded resource '{resourceName}'.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });

        foreach (var (key, value) in values)
            template = template.Replace($"{{{key}}}", value);

        return template;
    }
}
