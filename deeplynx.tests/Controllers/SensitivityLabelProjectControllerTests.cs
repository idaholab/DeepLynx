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
///     Unit tests for <see cref="SensitivityLabelProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class SensitivityLabelProjectControllerTests : IDisposable
{
    private readonly Mock<ISensitivityLabelBusiness> _mockSensitivityLabelBusiness;
    private readonly Mock<ILogger<SensitivityLabelProjectController>> _mockLogger;
    private readonly SensitivityLabelProjectController _sensitivityLabelProjectController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private static readonly long[] ProjectIds = { 2L, 13L };
    private const long UserId = 10L;
    private const long LabelId = 8L;
    private const long PermissionId = 9L;
    public SensitivityLabelProjectControllerTests()
    {
        _mockSensitivityLabelBusiness = new Mock<ISensitivityLabelBusiness>();
        _mockLogger = new Mock<ILogger<SensitivityLabelProjectController>>();

        _sensitivityLabelProjectController = new SensitivityLabelProjectController(
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
        UserContextStorage.OrganizationId = OrgId;
        IEnumerable<SensitivityLabelResponseDto> expected =
            new List<SensitivityLabelResponseDto>();

        _mockSensitivityLabelBusiness
       .Setup(b => b.GetAllSensitivityLabels(
           It.Is<long[]?>(ids =>
               ids != null &&
               ids.Length == 1 &&
               ids[0] == ProjectId),
           OrgId,
           true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _sensitivityLabelProjectController.GetAllSensitivityLabels(ProjectId, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_Returns200_WithEmptyList()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        _mockSensitivityLabelBusiness
       .Setup(b => b.GetAllSensitivityLabels(
           It.Is<long[]?>(ids =>
               ids != null &&
               ids.Length == 1 &&
               ids[0] == ProjectId),
           OrgId,
           true))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _sensitivityLabelProjectController.GetAllSensitivityLabels(ProjectId, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetAllSensitivityLabels_Returns500_OnUnexpectedException()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        _mockSensitivityLabelBusiness
       .Setup(b => b.GetAllSensitivityLabels(
           It.Is<long[]?>(ids =>
               ids != null &&
               ids.Length == 1 &&
               ids[0] == ProjectId),
           OrgId,
           true))
       .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _sensitivityLabelProjectController.GetAllSensitivityLabels(ProjectId, true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        IEnumerable<SensitivityLabelResponseDto> expected =
            new List<SensitivityLabelResponseDto>();

        _mockSensitivityLabelBusiness
       .Setup(b => b.GetAllSensitivityLabels(
           It.Is<long[]?>(ids =>
               ids != null &&
               ids.Length == 1 &&
               ids[0] == ProjectId),
           OrgId,
           true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = _sensitivityLabelProjectController.GetAllSensitivityLabels(ProjectId, true);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.GetAllSensitivityLabels(It.Is<long[]?>(ids =>
               ids != null &&
               ids.Length == 1 &&
               ids[0] == ProjectId),
                OrgId,
                true),
            Times.Once);
    }

    [Fact]
    public void GetAllSensitivityLabels_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.GetAllSensitivityLabels),
            "projectId",
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
        UserContextStorage.OrganizationId = OrgId;
        SensitivityLabelResponseDto expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(
                LabelId,
                ProjectId,
                OrgId,
                true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _sensitivityLabelProjectController.GetSensitivityLabel(
            ProjectId,
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
        UserContextStorage.OrganizationId = OrgId;
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(
                LabelId,
                ProjectId,
                OrgId,
                true))
            .ReturnsAsync((SensitivityLabelResponseDto)null!);

        // Act
        var actionResult = await _sensitivityLabelProjectController.GetSensitivityLabel(
            ProjectId,
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
        UserContextStorage.OrganizationId = OrgId;
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(
                LabelId,
                ProjectId,
                OrgId,
                true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _sensitivityLabelProjectController.GetSensitivityLabel(
            ProjectId,
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
        UserContextStorage.OrganizationId = OrgId;
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(
                LabelId,
                ProjectId,
                OrgId,
                true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _sensitivityLabelProjectController.GetSensitivityLabel(
            ProjectId,
            LabelId,
            true);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.GetSensitivityLabel(
                LabelId,
                ProjectId,
                OrgId,
                true),
            Times.Once);
    }

    [Fact]
    public void GetSensitivityLabel_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.GetSensitivityLabel),
            "projectId",
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
        UserContextStorage.OrganizationId = OrgId;
        SensitivityLabelResponseDto expected =
            new SensitivityLabelResponseDto();
        CreateSensitivityLabelRequestDto input = new CreateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(
                UserId,
                input,
                ProjectId,
                OrgId))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _sensitivityLabelProjectController.CreateSensitivityLabel(ProjectId, input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task CreateSensitivityLabel_Returns500_OnUnexpectedException()
    {
        UserContextStorage.OrganizationId = OrgId;
        CreateSensitivityLabelRequestDto input = new CreateSensitivityLabelRequestDto();
        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(
                UserId,
                input,
                ProjectId,
                OrgId))
            .ThrowsAsync(new Exception("db error"));

        var result = (await _sensitivityLabelProjectController.CreateSensitivityLabel(ProjectId, input)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateSensitivityLabel_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        CreateSensitivityLabelRequestDto input = new CreateSensitivityLabelRequestDto();
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(
                UserId,
                input,
                ProjectId,
                OrgId))
            .ReturnsAsync(expected);

        // Act
        await _sensitivityLabelProjectController.CreateSensitivityLabel(ProjectId, input);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.CreateSensitivityLabel(
                UserId,
                input,
                ProjectId,
                OrgId),
            Times.Once);
    }

    [Fact]
    public void CreateSensitivityLabel_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.CreateSensitivityLabel),
            "projectId",
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
        UserContextStorage.OrganizationId = OrgId;
        SensitivityLabelResponseDto expected =
            new SensitivityLabelResponseDto();
        UpdateSensitivityLabelRequestDto input = new UpdateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.UpdateSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId,
                input)).ReturnsAsync(expected);

        // Act
        var actionResult = await _sensitivityLabelProjectController.UpdateSensitivityLabel(ProjectId, LabelId, input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task UpdateSensitivityLabel_Returns500_OnUnexpectedException()
    {
        UserContextStorage.OrganizationId = OrgId;
        UpdateSensitivityLabelRequestDto input = new UpdateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
           .Setup(b => b.UpdateSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId,
                input)).ThrowsAsync(new Exception("db error"));

        var result = (await _sensitivityLabelProjectController.UpdateSensitivityLabel(ProjectId, LabelId, input)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdateSensitivityLabel_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        SensitivityLabelResponseDto expected =
            new SensitivityLabelResponseDto();
        UpdateSensitivityLabelRequestDto input = new UpdateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.UpdateSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId,
                input)).ReturnsAsync(expected);

        // Act
        await _sensitivityLabelProjectController.UpdateSensitivityLabel(ProjectId, LabelId, input);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.UpdateSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId,
                input),
            Times.Once);
    }

    [Fact]
    public void UpdateSensitivityLabel_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.UpdateSensitivityLabel),
            "projectId",
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
        UserContextStorage.OrganizationId = OrgId;
        var expectedMessage = $"Deleted sensitivity label {LabelId}";

        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId)).ReturnsAsync(true);

        // Act
        var actionResult = await _sensitivityLabelProjectController.DeleteSensitivityLabel(ProjectId, LabelId);

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
        UserContextStorage.OrganizationId = OrgId;
        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId)).ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _sensitivityLabelProjectController.DeleteSensitivityLabel(ProjectId, LabelId);

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
        UserContextStorage.OrganizationId = OrgId;
        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId)).ReturnsAsync(true);

        // Act
        await _sensitivityLabelProjectController.DeleteSensitivityLabel(ProjectId, LabelId);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.DeleteSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId),
            Times.Once);
    }

    [Fact]
    public void DeleteSensitivityLabel_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.DeleteSensitivityLabel),
            "projectId",
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
        UserContextStorage.OrganizationId = OrgId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _sensitivityLabelProjectController.ArchiveSensitivityLabel(
            ProjectId,
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
        UserContextStorage.OrganizationId = OrgId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _sensitivityLabelProjectController.ArchiveSensitivityLabel(
            ProjectId,
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
        UserContextStorage.OrganizationId = OrgId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _sensitivityLabelProjectController.ArchiveSensitivityLabel(
            ProjectId,
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
        UserContextStorage.OrganizationId = OrgId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _sensitivityLabelProjectController.ArchiveSensitivityLabel(
            ProjectId,
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
        UserContextStorage.OrganizationId = OrgId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId))
            .ReturnsAsync(true);

        // Act
        await _sensitivityLabelProjectController.ArchiveSensitivityLabel(
            ProjectId,
            LabelId,
            true);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.ArchiveSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId),
            Times.Once);

        _mockSensitivityLabelBusiness.Verify(
            b => b.UnarchiveSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_PassesUserIdOrganizationIdAndPermissionIdToBusinessLayer_WhenUnarchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.OrganizationId = OrgId;

        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId))
            .ReturnsAsync(true);

        // Act
        await _sensitivityLabelProjectController.ArchiveSensitivityLabel(
            ProjectId,
            LabelId,
            false);

        // Assert
        _mockSensitivityLabelBusiness.Verify(
            b => b.UnarchiveSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId),
            Times.Once);

        _mockSensitivityLabelBusiness.Verify(
            b => b.ArchiveSensitivityLabel(
                UserId,
                LabelId,
                ProjectId,
                OrgId),
            Times.Never);
    }

    [Fact]
    public void ArchiveSensitivityLabel_HasHttpPatch()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.ArchiveSensitivityLabel),
            "projectId",
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
        return Assert.Single(typeof(SensitivityLabelProjectController).GetMethods()
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
