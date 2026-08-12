using Asp.Versioning;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for managing notifications.
/// </summary>
[ApiController]
[ApiVersion(2)]
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
    /// <returns>Boolean true if email was sent successfully</returns>
    [HttpPost("email", Name = "api_send_email")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<bool>> SendEmail([FromQuery] string email, string? name)
    {
            if (string.IsNullOrEmpty(name)) name = email;
            var success = await _notificationBusiness.SendEmail(email, name);
            return Ok(success);
    }
}