using deeplynx.api.Controllers;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Org.BouncyCastle.Crypto.Engines;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class RelationshipOrganizationControllerTests : IDisposable
{
    private readonly Mock<IRelationshipBusiness> _mockRelationshipBusiness;
    private readonly Mock<ILogger<RelationshipOrganizationController>> _mockLogger;
    private readonly RelationshipOrganizationController _relationshipOrganizationController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private static readonly long[] ProjectList = { 13L, 14L };
    private const long DataSourceId = 20L;
    private const long RecordIdConst = 7L;
    private const long ClassId = 30L;
    private const long TagId = 40L;
    private const long LabelId = 50L;
    private const long NotFoundId = 99L;
    private const long TargetUserId = 20L;
    private const string UserEmail = "test@example.com";

    public RelationshipOrganizationControllerTests()
    {
        _mockRelationshipBusiness = new Mock<IRelationshipBusiness>();
        _mockLogger = new Mock<ILogger<RelationshipOrganizationController>>();

        _relationshipOrganizationController = new RelationshipOrganizationController(
            _mockRelationshipBusiness.Object,
            _mockLogger.Object
        );

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = false;
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
    // GetAllRelationships Tests
    // =========================================================================

    #region GetAllRelationships Tests

    [Fact]
    public async Task GetAllRelationships_Returns200_WithRelationships()
    {
        // Arrange
        List<RelationshipResponseDto> expected =
            new List<RelationshipResponseDto>();

        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationships(OrgId, ProjectList, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _relationshipOrganizationController.GetAllRelationships(OrgId, ProjectList, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockRelationshipBusiness.Verify(
            b => b.GetAllRelationships(OrgId, ProjectList, true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllRelationships_Returns200_WithEmptyList()
    {
        // Arrange

        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationships(It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _relationshipOrganizationController.GetAllRelationships(It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>());

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetAllRelationships_Returns500_OnUnexpectedException()
    {
        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationships(It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        var result = (await _relationshipOrganizationController.GetAllRelationships(It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>())).Result as ObjectResult;
        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetAllRelationships_PassesToBusinessLayer()
    {
        // Arrange

        var expected = new List<RelationshipResponseDto>();

        _mockRelationshipBusiness
            .Setup(b => b.GetAllRelationships(OrgId, ProjectList, true))
            .ReturnsAsync(expected);

        // Act
        await _relationshipOrganizationController.GetAllRelationships(OrgId, ProjectList, true);

        // Assert
        _mockRelationshipBusiness.Verify(
            b => b.GetAllRelationships(OrgId, ProjectList, true),
            Times.Once);
    }

    [Fact]
    public void GetAllRelationships_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.GetAllRelationships),
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // Test Helpers
    // =========================================================================


    private static void AssertHasHttpAttribute(
        System.Reflection.MethodInfo method,
        string expectedAttributeName)
    {
        Assert.Contains(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == expectedAttributeName);
    }

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(RelationshipOrganizationController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }

    private static string GetMessageFromResultValue(object? value)
    {
        Assert.NotNull(value);

        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var message = messageProperty.GetValue(value) as string;
        Assert.NotNull(message);

        return message;
    }
}