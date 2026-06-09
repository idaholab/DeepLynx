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
    private const long RelationshipId = 22L;


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
    // GetRelationship Tests
    // =========================================================================

    #region GetRelationship Tests

    [Fact]
    public async Task GetRelationship_Returns200_WithRelationship()
    {
        // Arrange
        RelationshipResponseDto expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _relationshipOrganizationController.GetRelationship(
            OrgId,
            RelationshipId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRelationship_Returns200_WithEmptyPermission()
    {
        // Arrange

        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, ProjectId, RelationshipId, true))
            .ReturnsAsync((RelationshipResponseDto)null!);

        // Act
        var actionResult = await _relationshipOrganizationController.GetRelationship(
            OrgId,
            RelationshipId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetRelationship_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _relationshipOrganizationController.GetRelationship(
            OrgId,
            RelationshipId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetRelationship_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new RelationshipResponseDto();

        _mockRelationshipBusiness
            .Setup(b => b.GetRelationship(OrgId, null, RelationshipId, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _relationshipOrganizationController.GetRelationship(
            OrgId,
            RelationshipId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockRelationshipBusiness.Verify(
            b => b.GetRelationship(
                OrgId,
                null,
                RelationshipId,
                true),
            Times.Once);
    }

    [Fact]
    public void GetRelationship_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.GetRelationship),
            "organizationId",
            "relationshipId",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // CreateRelationship Tests
    // =========================================================================

    #region CreateRelationship Tests

    [Fact]
    public async Task CreateRelationship_Returns200_WithRelationship()
    {
        // Arrange
        RelationshipResponseDto expected =
            new RelationshipResponseDto();
        CreateRelationshipRequestDto input = new CreateRelationshipRequestDto();

        _mockRelationshipBusiness
            .Setup(b => b.CreateRelationship(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _relationshipOrganizationController.CreateRelationship(
            OrgId,
            input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task CreateRelationship_Returns500_OnUnexpectedException()
    {
        CreateRelationshipRequestDto input = new CreateRelationshipRequestDto();
        _mockRelationshipBusiness
            .Setup(b => b.CreateRelationship(UserId, OrgId, null, input))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _relationshipOrganizationController.CreateRelationship(
            OrgId,
            input);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateRelationship_PassesToBusinessLayer()
    {
        // Arrange
        CreateRelationshipRequestDto input = new CreateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRelationshipBusiness
            .Setup(b => b.CreateRelationship(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _relationshipOrganizationController.CreateRelationship(
            OrgId,
            input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockRelationshipBusiness.Verify(
            b => b.CreateRelationship(UserId, OrgId, null, input),
            Times.Once);
    }

    [Fact]
    public void CreateRelationship_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.CreateRelationship),
            "organizationId",
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // BulkCreateRelationships Tests
    // =========================================================================

    #region BulkCreateRelationships Tests

    [Fact]
    public async Task BulkCreateRelationships_Returns200_WithRelationshipList()
    {
        // Arrange
        List<RelationshipResponseDto> expected =
            new List<RelationshipResponseDto>();
        List<CreateRelationshipRequestDto> input = new List<CreateRelationshipRequestDto>();

        _mockRelationshipBusiness
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _relationshipOrganizationController.BulkCreateRelationships(
            OrgId,
            input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task BulkCreateRelationships_Returns500_OnUnexpectedException()
    {
        List<CreateRelationshipRequestDto> input = new List<CreateRelationshipRequestDto>();
        _mockRelationshipBusiness
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, null, input))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _relationshipOrganizationController.BulkCreateRelationships(
            OrgId,
            input);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task BulkCreateRelationships_PassesToBusinessLayer()
    {
        // Arrange
        List<RelationshipResponseDto> expected =
            new List<RelationshipResponseDto>();
        List<CreateRelationshipRequestDto> input = new List<CreateRelationshipRequestDto>();

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRelationshipBusiness
            .Setup(b => b.BulkCreateRelationships(UserId, OrgId, null, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _relationshipOrganizationController.BulkCreateRelationships(
            OrgId,
            input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockRelationshipBusiness.Verify(
            b => b.BulkCreateRelationships(UserId, OrgId, null, input),
            Times.Once);
    }

    [Fact]
    public void BulkCreateRelationships_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.BulkCreateRelationships),
            "organizationId",
            "relationships");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion


    // =========================================================================
    // UpdateRelationship Tests
    // =========================================================================

    #region UpdateRelationship Tests

    [Fact]
    public async Task UpdateRelationship_Returns200_WithRelationship()
    {
        // Arrange
        RelationshipResponseDto expected =
            new RelationshipResponseDto();
        UpdateRelationshipRequestDto input = new UpdateRelationshipRequestDto();

        _mockRelationshipBusiness
            .Setup(b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _relationshipOrganizationController.UpdateRelationship(
            OrgId,
            RelationshipId,
            input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task UpdateRelationship_Returns500_OnUnexpectedException()
    {
        UpdateRelationshipRequestDto input = new UpdateRelationshipRequestDto();
        _mockRelationshipBusiness
            .Setup(b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _relationshipOrganizationController.UpdateRelationship(
            OrgId,
            RelationshipId,
            input);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdateRelationship_PassesToBusinessLayer()
    {
        // Arrange
        UpdateRelationshipRequestDto input = new UpdateRelationshipRequestDto();
        var expected = new RelationshipResponseDto();

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRelationshipBusiness
            .Setup(b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _relationshipOrganizationController.UpdateRelationship(
            OrgId,
            RelationshipId,
            input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockRelationshipBusiness.Verify(
            b => b.UpdateRelationship(UserId, OrgId, null, RelationshipId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateRelationship_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.UpdateRelationship),
            "organizationId",
            "relationshipId",
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
    }

    #endregion

    // =========================================================================
    // DeleteRelationship Tests
    // =========================================================================

    #region DeleteRelationship Tests

    [Fact]
    public async Task DeleteRelationship_Returns200()
    {
        // Arrange
        var expectedMessage = $"Deleted relationship {RelationshipId}";

        _mockRelationshipBusiness
            .Setup(b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _relationshipOrganizationController.DeleteRelationship(
            OrgId,
            RelationshipId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);

        var messageProperty = result.Value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var actualMessage = messageProperty.GetValue(result.Value) as string;
        Assert.Equal(expectedMessage, actualMessage);
    }


    [Fact]
    public async Task DeleteRelationship_Returns500_OnUnexpectedException()
    {
        _mockRelationshipBusiness
            .Setup(b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _relationshipOrganizationController.DeleteRelationship(
            OrgId,
            RelationshipId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task DeleteRelationship_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRelationshipBusiness
            .Setup(b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _relationshipOrganizationController.DeleteRelationship(
            OrgId,
            RelationshipId);

        var result = Assert.IsType<OkObjectResult>(actionResult);

        // Assert
        _mockRelationshipBusiness.Verify(
            b => b.DeleteRelationship(UserId, OrgId, null, RelationshipId),
            Times.Once);
    }

    [Fact]
    public void DeleteRelationship_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.DeleteRelationship),
            "organizationId",
            "relationshipId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // ArchiveRelationship Tests
    // =========================================================================

    #region ArchiveRelationship Tests

    [Fact]
    public async Task ArchiveRelationship_Returns200_WhenArchiving()
    {
        // Arrange
        var expectedMessage = $"Archived relationship {RelationshipId}";

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRelationshipBusiness
            .Setup(b => b.ArchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _relationshipOrganizationController.ArchiveRelationship(
            OrgId,
            RelationshipId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);

        var messageProperty = result.Value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var actualMessage = messageProperty.GetValue(result.Value) as string;
        Assert.Equal(expectedMessage, actualMessage);
    }

    [Fact]
    public async Task ArchiveRelationship_Returns200_WhenUnarchiving()
    {
        // Arrange
        var expectedMessage = $"Unarchived relationship {RelationshipId}";

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRelationshipBusiness
            .Setup(b => b.UnarchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _relationshipOrganizationController.ArchiveRelationship(
            OrgId,
            RelationshipId,
            false);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);

        var messageProperty = result.Value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var actualMessage = messageProperty.GetValue(result.Value) as string;
        Assert.Equal(expectedMessage, actualMessage);
    }

    [Fact]
    public async Task ArchiveRelationship_Returns500_OnUnexpectedException_WhenArchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRelationshipBusiness
            .Setup(b => b.ArchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _relationshipOrganizationController.ArchiveRelationship(
            OrgId,
            RelationshipId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);

        Assert.Contains(
            $"An error occurred while archiving relationship {RelationshipId}",
            message);
    }

    [Fact]
    public async Task ArchiveRelationship_Returns500_OnUnexpectedException_WhenUnarchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRelationshipBusiness
            .Setup(b => b.UnarchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _relationshipOrganizationController.ArchiveRelationship(
            OrgId,
            RelationshipId,
            false);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);

        Assert.Contains(
            $"An error occurred while unarchiving relationship {RelationshipId}",
            message);
    }

    [Fact]
    public async Task ArchiveRelationship_PassesToBusinessLayer_WhenArchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRelationshipBusiness
            .Setup(b => b.ArchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _relationshipOrganizationController.ArchiveRelationship(
            OrgId,
            RelationshipId,
            true);

        var result = Assert.IsType<OkObjectResult>(actionResult);

        // Assert
        _mockRelationshipBusiness.Verify(
            b => b.ArchiveRelationship(UserId, OrgId, null, RelationshipId),
            Times.Once);

        _mockRelationshipBusiness.Verify(
            b => b.UnarchiveRelationship(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long?>(),
                It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveRelationship_PassesToBusinessLayer_WhenUnarchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRelationshipBusiness
            .Setup(b => b.UnarchiveRelationship(UserId, OrgId, null, RelationshipId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _relationshipOrganizationController.ArchiveRelationship(
            OrgId,
            RelationshipId,
            false);

        var result = Assert.IsType<OkObjectResult>(actionResult);

        // Assert
        _mockRelationshipBusiness.Verify(
            b => b.UnarchiveRelationship(UserId, OrgId, null, RelationshipId),
            Times.Once);

        _mockRelationshipBusiness.Verify(
            b => b.ArchiveRelationship(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long?>(),
                It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public void ArchiveRelationship_HasHttpPatch()
    {
        var method = GetControllerMethod(
            nameof(RelationshipOrganizationController.ArchiveRelationship),
            "organizationId",
            "relationshipId",
            "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
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