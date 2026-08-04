using Asp.Versioning;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V1;

/// <summary>
///     Controller for managing notifications.
/// </summary>
[ApiController]
[ApiVersion(1, Deprecated = true)]
[Route("notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly ILogger<NotificationController> _logger;
    private readonly INotificationBusiness _notificationBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotificationController" /> class
    /// </summary>
    /// <param name="notificationBusiness">The business logic interface for handling class operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public NotificationController(INotificationBusiness notificationBusiness, ILogger<NotificationController> logger)
    {
        _notificationBusiness = notificationBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Send Email
    /// </summary>
    /// <returns>Message if email was sent successfully</returns>
    [HttpPost("email", Name = "api_send_email")]
    public async Task<IActionResult> SendEmail([FromQuery] string email, string? name)
    {
        try
        {
            if (string.IsNullOrEmpty(name)) name = email;
            var success = await _notificationBusiness.SendEmail(email, name);

            if (success) return Ok(new { success = true, message = $"Email sent successfully to {email}" });

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { success = false, message = "Failed to send email. Check server logs for details." });
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while sending email: {exc.Message}";
            _logger.LogError(exc, message);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { success = false, message });
        }
    }
    

}
