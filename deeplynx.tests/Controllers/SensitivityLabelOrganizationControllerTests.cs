using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for <see cref="SensitivityLabelOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class SensitivityLabelOrganizationControllerTests : IDisposable
{
    private readonly Mock<ISensitivityLabelBusiness> _mockSensitivityLabelBusiness;
    private readonly Mock<ILogger<SensitivityLabelOrganizationController>> _mockLogger;
    private readonly SensitivityLabelOrganizationController _sensitivityLabelOrganizationController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private static readonly long[] ProjectIds = { 11L, 13L };
    private const long UserId = 10L;
    private const long LabelId = 8L;
    private const long PermissionId = 9L;
    public SensitivityLabelOrganizationControllerTests()
    {
        _mockSensitivityLabelBusiness = new Mock<ISensitivityLabelBusiness>();
        _mockLogger = new Mock<ILogger<SensitivityLabelOrganizationController>>();

        _sensitivityLabelOrganizationController = new SensitivityLabelOrganizationController(
            _mockSensitivityLabelBusiness.Object,
            _mockLogger.Object);

        UserContextStorage.UserId = UserId;
    }

    public void Dispose()
    {
        // Reset to safe sentinels so a mutated value never bleeds into another class's tests
        UserContextStorage.UserId = default;
        UserContextStorage.OrganizationId = default;
        UserContextStorage.IsSysAdmin = default;
        UserContextStorage.IsOrgAdmin = default;
        UserContextStorage.IsProjectAdmin = default;
    }

    // =========================================================================
    // GetAllSensitivityLabels Tests
    // =========================================================================

    #region GetAllSensitivityLabels Tests

    [Fact]
    public async Task GetAllSensitivityLabels_Returns200_WithSensLabels()
    {
        // Arrange
        IEnumerable<SensitivityLabelResponseDto> expected =
            new List<SensitivityLabelResponseDto>();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.GetAllSensitivityLabels(OrgId, ProjectIds, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_Returns200_WithEmptyList()
    {
        // Arrange

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.GetAllSensitivityLabels(OrgId, ProjectIds, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetAllSensitivityLabels_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.GetAllSensitivityLabels(OrgId, ProjectIds, true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_PassesToBusinessLayer()
    {
        // Arrange
        IEnumerable<SensitivityLabelResponseDto> expected =
            new List<SensitivityLabelResponseDto>();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = _sensitivityLabelOrganizationController.GetAllSensitivityLabels(OrgId, ProjectIds, true);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true),
            Times.Once);
    }

    [Fact]
    public void GetAllSensitivityLabels_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.GetAllSensitivityLabels),
            "organizationId",
            "projectIds",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetSensitivityLabel Tests
    // =========================================================================

    #region GetSensitivityLabel Tests

    [Fact]
    public async Task GetSensitivityLabel_Returns200_WithSensitivityLabel()
    {
        // Arrange
        SensitivityLabelResponseDto expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(
                LabelId,
                null,
                OrgId,
                true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.GetSensitivityLabel(
            OrgId,
            LabelId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetSensitivityLabel_Returns200_WithNullSensitivityLabel()
    {
        // Arrange

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(
                LabelId,
                null,
                OrgId,
                true))
            .ReturnsAsync((SensitivityLabelResponseDto)null!);

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.GetSensitivityLabel(
            OrgId,
            LabelId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetSensitivityLabel_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(
                LabelId,
                null,
                OrgId,
                true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.GetSensitivityLabel(
            OrgId,
            LabelId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetSensitivityLabel_PassesToBusinessLayer()
    {
        // Arrange
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(
                LabelId,
                null,
                OrgId,
                true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.GetSensitivityLabel(
            OrgId,
            LabelId,
            true);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.GetSensitivityLabel(
                LabelId,
                null,
                OrgId,
                true),
            Times.Once);
    }

    [Fact]
    public void GetSensitivityLabel_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.GetSensitivityLabel),
            "organizationId",
            "labelId",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // CreateSensitivityLabel Tests
    // =========================================================================

    #region CreateSensitivityLabel Tests

    [Fact]
    public async Task CreateSensitivityLabel_Returns200_WithSensitivityLabel()
    {
        // Arrange
        SensitivityLabelResponseDto expected =
            new SensitivityLabelResponseDto();
        CreateSensitivityLabelRequestDto input = new CreateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(
                UserId,
                input,
                null,
                OrgId))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.CreateSensitivityLabel(OrgId, input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task CreateSensitivityLabel_Returns500_OnUnexpectedException()
    {
        CreateSensitivityLabelRequestDto input = new CreateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(
                UserId,
                input,
                null,
                OrgId))
            .ThrowsAsync(new Exception("db error"));

        var result = (await _sensitivityLabelOrganizationController.CreateSensitivityLabel(OrgId, input)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateSensitivityLabel_PassesToBusinessLayer()
    {
        // Arrange
        CreateSensitivityLabelRequestDto input = new CreateSensitivityLabelRequestDto();
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(
                UserId,
                input,
                null,
                OrgId))
            .ReturnsAsync(expected);

        // Act
        await _sensitivityLabelOrganizationController.CreateSensitivityLabel(OrgId, input);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.CreateSensitivityLabel(
                UserId,
                input,
                null,
                OrgId),
            Times.Once);
    }

    [Fact]
    public void CreateSensitivityLabel_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.CreateSensitivityLabel),
            "organizationId",
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // UpdateSensitivityLabel Tests
    // =========================================================================

    #region UpdateSensitivityLabel Tests

    [Fact]
    public async Task UpdateSensitivityLabel_Returns200_WithPermission()
    {
        // Arrange
        SensitivityLabelResponseDto expected =
            new SensitivityLabelResponseDto();
        UpdateSensitivityLabelRequestDto input = new UpdateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.UpdateSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId,
                input)).ReturnsAsync(expected);

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.UpdateSensitivityLabel(OrgId, LabelId, input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task UpdateSensitivityLabel_Returns500_OnUnexpectedException()
    {
        UpdateSensitivityLabelRequestDto input = new UpdateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
           .Setup(b => b.UpdateSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId,
                input)).ThrowsAsync(new Exception("db error"));

        var result = (await _sensitivityLabelOrganizationController.UpdateSensitivityLabel(OrgId, LabelId, input)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdateSensitivityLabel_PassesToBusinessLayer()
    {
        // Arrange
        SensitivityLabelResponseDto expected =
            new SensitivityLabelResponseDto();
        UpdateSensitivityLabelRequestDto input = new UpdateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.UpdateSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId,
                input)).ReturnsAsync(expected);

        // Act
        await _sensitivityLabelOrganizationController.UpdateSensitivityLabel(OrgId, LabelId, input);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.UpdateSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId,
                input),
            Times.Once);
    }

    [Fact]
    public void UpdateSensitivityLabel_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.UpdateSensitivityLabel),
            "organizationId",
            "labelId",
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
    }

    #endregion

    // =========================================================================
    // DeleteSensitivityLabel Tests
    // =========================================================================

    #region DeleteSensitivityLabel Tests

    [Fact]
    public async Task DeleteSensitivityLabel_Returns200_WithMessage()
    {
        // Arrange
        var expectedMessage = $"Deleted sensitivity label {LabelId}";

        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId)).ReturnsAsync(true);

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.DeleteSensitivityLabel(OrgId, LabelId);

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
    public async Task DeleteSensitivityLabel_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId)).ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.DeleteSensitivityLabel(OrgId, LabelId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while deleting sensitivity label {LabelId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task DeleteSensitivityLabel_PassesToBusinessLayer()
    {
        // Arrange
        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId)).ReturnsAsync(true);

        // Act
        await _sensitivityLabelOrganizationController.DeleteSensitivityLabel(OrgId, LabelId);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.DeleteSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId),
            Times.Once);
    }

    [Fact]
    public void DeleteSensitivityLabel_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.DeleteSensitivityLabel),
            "organizationId",
            "labelId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // ArchiveSensitivityLabel Tests
    // =========================================================================

    #region ArchiveSensitivityLabel Tests

    [Fact]
    public async Task ArchiveSensitivityLabel_Returns200_WithArchivedMessage()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(
            OrgId,
            LabelId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Archived sensitivity label {LabelId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_Returns200_WithUnarchivedMessage()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(
            OrgId,
            LabelId,
            false);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Unarchived sensitivity label {LabelId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_Returns500_OnArchiveException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(
            OrgId,
            LabelId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while archiving sensitivity label {LabelId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_Returns500_OnUnarchiveException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(
            OrgId,
            LabelId,
            false);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while unarchiving sensitivity label {LabelId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_PassesUserIdOrganizationIdAndPermissionIdToBusinessLayer_WhenArchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId))
            .ReturnsAsync(true);

        // Act
        await _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(
            OrgId,
            LabelId,
            true);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.ArchiveSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId),
            Times.Once);

        _mockSensitivityLabelBusiness.Verify(
            b => b.UnarchiveSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_PassesUserIdOrganizationIdAndPermissionIdToBusinessLayer_WhenUnarchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId))
            .ReturnsAsync(true);

        // Act
        await _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(
            OrgId,
            LabelId,
            false);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.UnarchiveSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId),
            Times.Once);

        _mockSensitivityLabelBusiness.Verify(
            b => b.ArchiveSensitivityLabel(
                UserId,
                LabelId,
                null,
                OrgId),
            Times.Never);
    }

    [Fact]
    public void ArchiveSensitivityLabel_HasHttpPatch()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.ArchiveSensitivityLabel),
            "organizationId",
            "labelId",
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
        return Assert.Single(typeof(SensitivityLabelOrganizationController).GetMethods()
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
