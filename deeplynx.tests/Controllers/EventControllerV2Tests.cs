using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]


public class EventControllerV2Tests : IDisposable
{
    private readonly Mock<IEventBusiness> _mockEventBusiness;
    private readonly Mock<ILogger<EventController>> _mockLogger;
    private readonly EventController _eventController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private static readonly long[] ProjectIds = { 2L, 3L };
    private readonly EventsQueryRequestDto QueryDto = new();

    public EventControllerV2Tests()
    {
        _mockEventBusiness = new Mock<IEventBusiness>();
        _mockLogger = new Mock<ILogger<EventController>>();

        _eventController = new EventController(
            _mockEventBusiness.Object,
            _mockLogger.Object);

        UserContextStorage.UserId = UserId;
    }

    public void Dispose()
    {
        UserContextStorage.UserId = default;
        UserContextStorage.OrganizationId = default;
        UserContextStorage.IsSysAdmin = default;
        UserContextStorage.IsOrgAdmin = default;
        UserContextStorage.IsProjectAdmin = default;
    }

    // =========================================================================
    // GetAllEvents Tests
    // =========================================================================

    #region GetAllEvents Tests

    [Fact]
    public async Task GetAllEvents_Returns200_WithEventList()
    {
        // Arrange
        var expected = new List<EventResponseDto> { new() };
        _mockEventBusiness
            .Setup(b => b.GetAllEvents(ProjectId, OrgId))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _eventController.GetAllEventsV2(ProjectId, OrgId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllEvents_Returns200_WithNullFilters()
    {
        // Arrange
        var expected = new List<EventResponseDto>();
        _mockEventBusiness
            .Setup(b => b.GetAllEvents(null, null))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _eventController.GetAllEventsV2(null, null);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllEvents_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockEventBusiness
            .Setup(b => b.GetAllEvents(It.IsAny<long?>(), It.IsAny<long?>()))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _eventController.GetAllEventsV2(ProjectId, OrgId));
    }

    [Fact]
    public async Task GetAllEvents_PassesFiltersToBusinessLayer()
    {
        // Arrange
        var expected = new List<EventResponseDto>();
        _mockEventBusiness
            .Setup(b => b.GetAllEvents(ProjectId, OrgId))
            .ReturnsAsync(expected);

        // Act
        await _eventController.GetAllEventsV2(ProjectId, OrgId);

        // Assert
        _mockEventBusiness.Verify(b => b.GetAllEvents(ProjectId, OrgId), Times.Once);
    }

    [Fact]
    public void GetAllEvents_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(EventController.GetAllEventsV2),
            "projectId",
            "organizationId");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "GetAllEvents");
    }

    #endregion

    // =========================================================================
    // QueryEvents Tests
    // =========================================================================

    #region QueryEvents Tests

    [Fact]
    public async Task QueryEvents_Returns200_WithPaginatedResponse()
    {
        // Arrange
        var expected = new PaginatedResponse<EventResponseDto>();
        _mockEventBusiness
            .Setup(b => b.QueryAllEvents(OrgId, ProjectId, QueryDto))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _eventController.QueryEventsV2(OrgId, ProjectId, QueryDto);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task QueryEvents_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockEventBusiness
            .Setup(b => b.QueryAllEvents(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<EventsQueryRequestDto?>()))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _eventController.QueryEventsV2(OrgId, ProjectId, QueryDto));
    }

    [Fact]
    public async Task QueryEvents_PassesParametersToBusinessLayer()
    {
        // Arrange
        var expected = new PaginatedResponse<EventResponseDto>();
        _mockEventBusiness
            .Setup(b => b.QueryAllEvents(OrgId, ProjectId, QueryDto))
            .ReturnsAsync(expected);

        // Act
        await _eventController.QueryEventsV2(OrgId, ProjectId, QueryDto);

        // Assert
        _mockEventBusiness.Verify(b => b.QueryAllEvents(OrgId, ProjectId, QueryDto), Times.Once);
    }

    [Fact]
    public void QueryEvents_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(EventController.QueryEventsV2),
            "organizationId",
            "projectId",
            "queryDto");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "QueryEvents");
    }

    #endregion

    // =========================================================================
    // QueryAuthorizedEvents Tests
    // =========================================================================

    #region QueryAuthorizedEvents Tests

    [Fact]
    public async Task QueryAuthorizedEvents_Returns200_WithEventList()
    {
        // Arrange
        var expected = new PaginatedResponse<EventResponseDto>();
        _mockEventBusiness
            .Setup(b => b.QueryAuthorizedEvents(UserId, OrgId, ProjectIds, QueryDto))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _eventController.QueryAuthorizedEventsV2(OrgId, ProjectIds, QueryDto);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task QueryAuthorizedEvents_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockEventBusiness
            .Setup(b => b.QueryAuthorizedEvents(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<EventsQueryRequestDto?>()))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _eventController.QueryAuthorizedEventsV2(
            OrgId, ProjectIds, QueryDto));
    }

    [Fact]
    public async Task QueryAuthorizedEvents_PassesCurrentUserIdToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = 77L;
        var expected = new PaginatedResponse<EventResponseDto>();
        _mockEventBusiness
            .Setup(b => b.QueryAuthorizedEvents(77L, OrgId, ProjectIds, QueryDto))
            .ReturnsAsync(expected);

        // Act
        await _eventController.QueryAuthorizedEventsV2(OrgId, ProjectIds, QueryDto);

        // Assert
        _mockEventBusiness.Verify(
            b => b.QueryAuthorizedEvents(77L, OrgId, ProjectIds, QueryDto), Times.Once);
    }

    [Fact]
    public async Task QueryAuthorizedEvents_PassesOrganizationIdAndProjectIdsToBusinessLayer()
    {
        // Arrange
        var expected = new PaginatedResponse<EventResponseDto>();
        _mockEventBusiness
            .Setup(b => b.QueryAuthorizedEvents(UserId, OrgId, ProjectIds, QueryDto))
            .ReturnsAsync(expected);

        // Act
        await _eventController.QueryAuthorizedEventsV2(OrgId, ProjectIds, QueryDto);

        // Assert
        _mockEventBusiness.Verify(
            b => b.QueryAuthorizedEvents(UserId, OrgId, ProjectIds, QueryDto), Times.Once);
    }

    [Fact]
    public void QueryAuthorizedEvents_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(EventController.QueryAuthorizedEventsV2),
            "organizationId",
            "projectIds",
            "queryDto");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "QueryAuthorizedEvents/{organizationId:long}");
    }

    #endregion

    // =========================================================================
    // QueryEventsBySubscriptions Tests
    // =========================================================================

    #region QueryEventsBySubscriptions Tests

    [Fact]
    public async Task QueryEventsBySubscriptions_Returns200_WithEventList()
    {
        // Arrange
        var expected = new PaginatedResponse<EventResponseDto>();
        _mockEventBusiness
            .Setup(b => b.QueryEventsBySubscriptions(UserId, OrgId, ProjectId, QueryDto))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _eventController.QueryEventsBySubscriptionsV2(OrgId, ProjectId, QueryDto);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task QueryEventsBySubscriptions_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockEventBusiness
            .Setup(b => b.QueryEventsBySubscriptions(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<EventsQueryRequestDto?>()))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _eventController.QueryEventsBySubscriptionsV2(
            OrgId, ProjectId, QueryDto));
    }

    [Fact]
    public async Task QueryEventsBySubscriptions_PassesCurrentUserIdToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = 77L;
        var expected = new PaginatedResponse<EventResponseDto>();
        _mockEventBusiness
            .Setup(b => b.QueryEventsBySubscriptions(77L, OrgId, ProjectId, QueryDto))
            .ReturnsAsync(expected);

        // Act
        await _eventController.QueryEventsBySubscriptionsV2(OrgId, ProjectId, QueryDto);

        // Assert
        _mockEventBusiness.Verify(
            b => b.QueryEventsBySubscriptions(77L, OrgId, ProjectId, QueryDto), Times.Once);
    }

    [Fact]
    public async Task QueryEventsBySubscriptions_PassesOrganizationIdAndProjectIdToBusinessLayer()
    {
        // Arrange
        var expected = new PaginatedResponse<EventResponseDto>();
        _mockEventBusiness
            .Setup(b => b.QueryEventsBySubscriptions(UserId, OrgId, ProjectId, QueryDto))
            .ReturnsAsync(expected);

        // Act
        await _eventController.QueryEventsBySubscriptionsV2(OrgId, ProjectId, QueryDto);

        // Assert
        _mockEventBusiness.Verify(
            b => b.QueryEventsBySubscriptions(UserId, OrgId, ProjectId, QueryDto), Times.Once);
    }

    [Fact]
    public void QueryEventsBySubscriptions_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(EventController.QueryEventsBySubscriptionsV2),
            "organizationId",
            "projectId",
            "queryDto");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "QueryEventsBySubscriptions");
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
        return Assert.Single(typeof(EventController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}