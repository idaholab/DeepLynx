using deeplynx.api.Controllers.V2;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]


public class NotificationControllerTests
{
    private readonly Mock<INotificationBusiness> _mockNotificationBusiness;
    private readonly Mock<ILogger<NotificationController>> _mockLogger;
    private readonly NotificationController _notificationController;

    private const string Email = "user@example.com";
    private const string Name = "Test User";

    public NotificationControllerTests()
    {
        _mockNotificationBusiness = new Mock<INotificationBusiness>();
        _mockLogger = new Mock<ILogger<NotificationController>>();

        _notificationController = new NotificationController(
            _mockNotificationBusiness.Object,
            _mockLogger.Object);
    }

    // =========================================================================
    // SendEmail Tests
    // =========================================================================

    #region SendEmail Tests

    [Fact]
    public async Task SendEmail_Returns200_WithTrueOnSuccess()
    {
        // Arrange
        _mockNotificationBusiness
            .Setup(b => b.SendEmail(Email, Name))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _notificationController.SendEmail(Email, Name);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task SendEmail_Returns200_WithFalseOnFailure()
    {
        // Arrange
        // Note: unlike V1 (which returns a 500 when the business layer reports failure), V2
        // returns a 200 with a `false` body. This pins down that intentional-or-not behavior
        // change so a future fix is deliberate rather than silent.
        _mockNotificationBusiness
            .Setup(b => b.SendEmail(Email, Name))
            .ReturnsAsync(false);

        // Act
        var actionResult = await _notificationController.SendEmail(Email, Name);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(false, result.Value);
    }

    [Fact]
    public async Task SendEmail_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockNotificationBusiness
            .Setup(b => b.SendEmail(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("smtp error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _notificationController.SendEmail(Email, Name));
    }

    [Fact]
    public async Task SendEmail_DefaultsNameToEmailWhenNameIsNull()
    {
        // Arrange
        _mockNotificationBusiness
            .Setup(b => b.SendEmail(Email, Email))
            .ReturnsAsync(true);

        // Act
        await _notificationController.SendEmail(Email, null);

        // Assert
        _mockNotificationBusiness.Verify(b => b.SendEmail(Email, Email), Times.Once);
    }

    [Fact]
    public async Task SendEmail_DefaultsNameToEmailWhenNameIsEmpty()
    {
        // Arrange
        _mockNotificationBusiness
            .Setup(b => b.SendEmail(Email, Email))
            .ReturnsAsync(true);

        // Act
        await _notificationController.SendEmail(Email, string.Empty);

        // Assert
        _mockNotificationBusiness.Verify(b => b.SendEmail(Email, Email), Times.Once);
    }

    [Fact]
    public async Task SendEmail_PassesProvidedNameToBusinessLayer()
    {
        // Arrange
        _mockNotificationBusiness
            .Setup(b => b.SendEmail(Email, Name))
            .ReturnsAsync(true);

        // Act
        await _notificationController.SendEmail(Email, Name);

        // Assert
        _mockNotificationBusiness.Verify(b => b.SendEmail(Email, Name), Times.Once);
    }

    [Fact]
    public void SendEmail_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(NotificationController.SendEmail),
            "email",
            "name");

        AssertHasHttpAttribute(method, "HttpPostAttribute", "email");
    }

    #endregion

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private static void AssertHasHttpAttribute(
        System.Reflection.MethodInfo method,
        string expectedAttributeName,
        string expectedRoute)
    {
        var httpAttribute = Assert.Single(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == expectedAttributeName);

        Assert.Equal(expectedRoute, httpAttribute.ConstructorArguments[0].Value);
    }

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(NotificationController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}