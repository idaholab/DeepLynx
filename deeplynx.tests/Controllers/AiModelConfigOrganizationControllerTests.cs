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
///     Unit tests for <see cref="AiModelConfigController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class AiModelConfigControllerTests : IDisposable
{
    private readonly Mock<IAiModelConfigBusiness> _mockAiModelConfigBusiness;
    private readonly Mock<ILogger<AiModelConfigController>> _mockLogger;
    private readonly AiModelConfigController _aiModelConfigController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long LabelId = 8L;
    private const long AiModelConfigId = 19L;
    private readonly string ModelType = "devModel";
    public AiModelConfigControllerTests()
    {
        _mockAiModelConfigBusiness = new Mock<IAiModelConfigBusiness>();
        _mockLogger = new Mock<ILogger<AiModelConfigController>>();

        _aiModelConfigController = new AiModelConfigController(
            _mockAiModelConfigBusiness.Object,
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
    // GetAllAiModelConfigs Tests
    // =========================================================================

    #region GetAllAiModelConfigs Tests

    [Fact]
    public async Task GetAllAiModelConfigs_Returns200_WithAiModelConfigs()
    {
        // Arrange
        var expected =
            new List<AiModelConfigResponseDto>();

        _mockAiModelConfigBusiness
            .Setup(b => b.GetAllAiModelConfigs(OrgId, null, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _aiModelConfigController.GetAllAiModelConfigs(OrgId, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockAiModelConfigBusiness.Verify(
            b => b.GetAllAiModelConfigs(OrgId, null, true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllAiModelConfigs_Returns200_WithEmptyList()
    {
        // Arrange

        _mockAiModelConfigBusiness
            .Setup(b => b.GetAllAiModelConfigs(OrgId, null, true))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _aiModelConfigController.GetAllAiModelConfigs(OrgId, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetAllAiModelConfigs_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockAiModelConfigBusiness
            .Setup(b => b.GetAllAiModelConfigs(OrgId, null, true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _aiModelConfigController.GetAllAiModelConfigs(OrgId, true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        _mockAiModelConfigBusiness.Verify(
            b => b.GetAllAiModelConfigs(OrgId, null, true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllAiModelConfigs_PassesToBusinessLayer()
    {
        // Arrange
        var expected =
            new List<AiModelConfigResponseDto>();

        _mockAiModelConfigBusiness
            .Setup(b => b.GetAllAiModelConfigs(OrgId, null, true))
            .ReturnsAsync(expected);

        // Act
        await _aiModelConfigController.GetAllAiModelConfigs(OrgId, true);

        // Assert
        _mockAiModelConfigBusiness.Verify(
            b => b.GetAllAiModelConfigs(OrgId, null, true),
            Times.Once);
    }

    [Fact]
    public void GetAllAiModelConfigs_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(AiModelConfigController.GetAllAiModelConfigs),
            "organizationId",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetAiModelConfig Tests
    // =========================================================================

    #region GetAiModelConfig Tests

    [Fact]
    public async Task GetAiModelConfig_Returns200_WithAiModelConfig()
    {
        // Arrange
        AiModelConfigResponseDto expected = new AiModelConfigResponseDto();

        _mockAiModelConfigBusiness
            .Setup(b => b.GetAiModelConfig(
                OrgId,
                null,
                AiModelConfigId,
                true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _aiModelConfigController.GetAiModelConfig(
            OrgId,
            AiModelConfigId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAiModelConfig_Returns200_WithNullAiModelConfig()
    {
        // Arrange

        _mockAiModelConfigBusiness
            .Setup(b => b.GetAiModelConfig(
                OrgId,
                null,
                AiModelConfigId,
                true))
            .ReturnsAsync((AiModelConfigResponseDto)null!);

        // Act
        var actionResult = await _aiModelConfigController.GetAiModelConfig(
            OrgId,
            AiModelConfigId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetAiModelConfig_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockAiModelConfigBusiness
            .Setup(b => b.GetAiModelConfig(
                OrgId,
                null,
                AiModelConfigId,
                true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _aiModelConfigController.GetAiModelConfig(
            OrgId,
            AiModelConfigId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetAiModelConfig_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new AiModelConfigResponseDto();

        _mockAiModelConfigBusiness
            .Setup(b => b.GetAiModelConfig(
                OrgId,
                null,
                AiModelConfigId,
                true))
            .ReturnsAsync(expected);

        // Act
        await _aiModelConfigController.GetAiModelConfig(
            OrgId,
            AiModelConfigId,
            true);

        // Assert
        _mockAiModelConfigBusiness.Verify(
            b => b.GetAiModelConfig(
                OrgId,
                null,
                AiModelConfigId,
                true),
            Times.Once);
    }

    [Fact]
    public void GetAiModelConfig_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(AiModelConfigController.GetAiModelConfig),
            "organizationId",
            "aiModelConfigId",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetDefaultAiModelConfig Tests
    // =========================================================================

    #region GetDefaultAiModelConfig Tests

    [Fact]
    public async Task GetDefaultAiModelConfig_Returns200_WithAiModelConfig()
    {
        // Arrange
        AiModelConfigResponseDto expected = new AiModelConfigResponseDto();

        _mockAiModelConfigBusiness
            .Setup(b => b.GetDefaultAiModelConfig(
                OrgId,
                null,
                ModelType))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _aiModelConfigController.GetDefaultAiModelConfig(
            OrgId,
            ModelType);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_Returns200_WithNullAiModelConfig()
    {
        // Arrange

        _mockAiModelConfigBusiness
            .Setup(b => b.GetDefaultAiModelConfig(
                OrgId,
                null,
                ModelType))
            .ReturnsAsync((AiModelConfigResponseDto)null!);

        // Act
        var actionResult = await _aiModelConfigController.GetDefaultAiModelConfig(
            OrgId,
            ModelType);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockAiModelConfigBusiness
            .Setup(b => b.GetDefaultAiModelConfig(
                OrgId,
                null,
                ModelType))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _aiModelConfigController.GetDefaultAiModelConfig(
            OrgId,
            ModelType);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new AiModelConfigResponseDto();

        _mockAiModelConfigBusiness
            .Setup(b => b.GetDefaultAiModelConfig(
                OrgId,
                null,
                ModelType))
            .ReturnsAsync(expected);

        // Act
        await _aiModelConfigController.GetDefaultAiModelConfig(
            OrgId,
            ModelType);

        // Assert
        _mockAiModelConfigBusiness.Verify(
            b => b.GetDefaultAiModelConfig(
                OrgId,
                null,
                ModelType),
            Times.Once);
    }

    [Fact]
    public void GetDefaultAiModelConfig_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(AiModelConfigController.GetDefaultAiModelConfig),
            "organizationId",
            "modelType");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // CreateAiModelConfig Tests
    // =========================================================================

    #region CreateAiModelConfig Tests

    [Fact]
    public async Task CreateAiModelConfig_Returns200_WithAiModelConfig()
    {
        // Arrange
        AiModelConfigResponseDto expected =
            new AiModelConfigResponseDto();
        CreateAiModelConfigDto input = new CreateAiModelConfigDto();

        _mockAiModelConfigBusiness
            .Setup(b => b.CreateAiModelConfig(
                UserId,
                OrgId,
                null,
                input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _aiModelConfigController.CreateAiModelConfig(OrgId, input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task CreateAiModelConfig_Returns500_OnUnexpectedException()
    {
        CreateAiModelConfigDto input = new CreateAiModelConfigDto();
        _mockAiModelConfigBusiness
            .Setup(b => b.CreateAiModelConfig(
                UserId,
                OrgId,
                null,
                input))
                .ThrowsAsync(new Exception("db error"));

        var result = (await _aiModelConfigController.CreateAiModelConfig(OrgId, input)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateAiModelConfig_PassesToBusinessLayer()
    {
        // Arrange
        CreateAiModelConfigDto input = new CreateAiModelConfigDto();

        var expected = new AiModelConfigResponseDto();

        _mockAiModelConfigBusiness
            .Setup(b => b.CreateAiModelConfig(
                UserId,
                OrgId,
                null,
                input))
            .ReturnsAsync(expected);

        // Act
        await _aiModelConfigController.CreateAiModelConfig(OrgId, input);

        // Assert
        _mockAiModelConfigBusiness.Verify(
            b => b.CreateAiModelConfig(
                UserId,
                OrgId,
                null,
                input),
            Times.Once);
    }

    [Fact]
    public void CreateAiModelConfig_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(AiModelConfigController.CreateAiModelConfig),
            "organizationId",
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // UpdateAiModelConfig Tests
    // =========================================================================

    #region UpdateAiModelConfig Tests

    [Fact]
    public async Task UpdateAiModelConfig_Returns200_WithAiModelConfig()
    {
        // Arrange
        AiModelConfigResponseDto expected =
            new AiModelConfigResponseDto();
        UpdateAiModelConfigDto input = new UpdateAiModelConfigDto();

        _mockAiModelConfigBusiness
            .Setup(b => b.UpdateAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId,
                input)).ReturnsAsync(expected);

        // Act
        var actionResult = await _aiModelConfigController.UpdateAiModelConfig(OrgId, AiModelConfigId, input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task UpdateAiModelConfig_Returns500_OnUnexpectedException()
    {
        UpdateAiModelConfigDto input = new UpdateAiModelConfigDto();
        _mockAiModelConfigBusiness
           .Setup(b => b.UpdateAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId,
                input)).ThrowsAsync(new Exception("db error"));

        var result = (await _aiModelConfigController.UpdateAiModelConfig(OrgId, AiModelConfigId, input)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdateAiModelConfig_PassesToBusinessLayer()
    {
        // Arrange
        UpdateAiModelConfigDto input = new UpdateAiModelConfigDto();

        var expected = new AiModelConfigResponseDto();

        _mockAiModelConfigBusiness
            .Setup(b => b.UpdateAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId,
                input)).ReturnsAsync(expected);

        // Act
        await _aiModelConfigController.UpdateAiModelConfig(OrgId, AiModelConfigId, input);

        // Assert
        _mockAiModelConfigBusiness.Verify(
            b => b.UpdateAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId,
                input),
            Times.Once);
    }

    [Fact]
    public void UpdateAiModelConfig_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(AiModelConfigController.UpdateAiModelConfig),
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
    }

    #endregion

    // =========================================================================
    // ArchiveAiModelConfig Tests
    // =========================================================================

    #region ArchiveAiModelConfig Tests

    [Fact]
    public async Task ArchiveAiModelConfig_Returns200_WithArchivedMessage()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockAiModelConfigBusiness
            .Setup(b => b.ArchiveAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _aiModelConfigController.ArchiveAiModelConfig(
            OrgId,
            AiModelConfigId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Archived class {AiModelConfigId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task ArchiveAiModelConfig_Returns200_WithUnarchivedMessage()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockAiModelConfigBusiness
            .Setup(b => b.UnarchiveAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _aiModelConfigController.ArchiveAiModelConfig(
            OrgId,
            AiModelConfigId,
            false);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Unarchived class {AiModelConfigId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task ArchiveAiModelConfig_Returns500_OnArchiveException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockAiModelConfigBusiness
            .Setup(b => b.ArchiveAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _aiModelConfigController.ArchiveAiModelConfig(
            OrgId,
            AiModelConfigId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An unexpected error occurred while archiving the AI Model Configuration.", message);
    }

    [Fact]
    public async Task ArchiveAiModelConfig_Returns500_OnUnarchiveException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockAiModelConfigBusiness
            .Setup(b => b.UnarchiveAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _aiModelConfigController.ArchiveAiModelConfig(
            OrgId,
            AiModelConfigId,
            false);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An unexpected error occurred while unarchiving the AI Model Configuration.", message);
    }

    [Fact]
    public async Task ArchiveAiModelConfig_PassesUserIdOrganizationIdAndAiModelConfigIdToBusinessLayer_WhenArchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockAiModelConfigBusiness
            .Setup(b => b.ArchiveAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId))
            .ReturnsAsync(true);

        // Act
        await _aiModelConfigController.ArchiveAiModelConfig(
            OrgId,
            AiModelConfigId,
            true);

        // Assert
        _mockAiModelConfigBusiness.Verify(
            b => b.ArchiveAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId),
            Times.Once);

        _mockAiModelConfigBusiness.Verify(
            b => b.UnarchiveAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveAiModelConfig_PassesUserIdOrganizationIdAndAiModelConfigIdToBusinessLayer_WhenUnarchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockAiModelConfigBusiness
            .Setup(b => b.UnarchiveAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId))
            .ReturnsAsync(true);

        // Act
        await _aiModelConfigController.ArchiveAiModelConfig(
            OrgId,
            AiModelConfigId,
            false);

        // Assert
        _mockAiModelConfigBusiness.Verify(
            b => b.UnarchiveAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId),
            Times.Once);

        _mockAiModelConfigBusiness.Verify(
            b => b.ArchiveAiModelConfig(
                UserId,
                OrgId,
                null,
                AiModelConfigId),
            Times.Never);
    }

    [Fact]
    public void ArchiveAiModelConfig_HasHttpPatch()
    {
        var method = GetControllerMethod(
            nameof(AiModelConfigController.ArchiveAiModelConfig),
            "organizationId",
            "aiModelConfigId",
            "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
    }

    #endregion

    // =========================================================================
    // DeleteAiModelConfig Tests
    // =========================================================================

    #region DeleteAiModelConfig Tests

    [Fact]
    public async Task DeleteAiModelConfig_Returns200_WithMessage()
    {
        // Arrange
        var expectedMessage = $"Deleted AI Model Configuration {AiModelConfigId}";

        _mockAiModelConfigBusiness
            .Setup(b => b.DeleteAiModelConfig(
                OrgId,
                null,
                AiModelConfigId)).ReturnsAsync(true);

        // Act
        var actionResult = await _aiModelConfigController.DeleteAiModelConfig(OrgId, AiModelConfigId);

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
    public async Task DeleteAiModelConfig_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockAiModelConfigBusiness
            .Setup(b => b.DeleteAiModelConfig(
                OrgId,
                null,
                AiModelConfigId)).ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _aiModelConfigController.DeleteAiModelConfig(OrgId, AiModelConfigId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains("An unexpected error occurred while deleting the AI Model Configuration.", message);
    }

    [Fact]
    public async Task DeleteAiModelConfig_PassesToBusinessLayer()
    {
        // Arrange
        _mockAiModelConfigBusiness
            .Setup(b => b.DeleteAiModelConfig(
                OrgId,
                null,
                AiModelConfigId)).ReturnsAsync(true);

        // Act
        await _aiModelConfigController.DeleteAiModelConfig(OrgId, AiModelConfigId);

        // Assert
        _mockAiModelConfigBusiness.Verify(
            b => b.DeleteAiModelConfig(
                OrgId,
                null,
                AiModelConfigId),
            Times.Once);
    }

    [Fact]
    public void DeleteAiModelConfig_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(AiModelConfigController.DeleteAiModelConfig),
            "organizationId",
            "aiModelConfigId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
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
        return Assert.Single(typeof(AiModelConfigController).GetMethods()
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
