using deeplynx.api.Controllers.V2;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for the V2 actions of <see cref="SensitivityLabelProjectController"/>.
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
    private const long UserId = 10L;
    private const long LabelId = 8L;

    public SensitivityLabelProjectControllerTests()
    {
        _mockSensitivityLabelBusiness = new Mock<ISensitivityLabelBusiness>();
        _mockLogger = new Mock<ILogger<SensitivityLabelProjectController>>();

        _sensitivityLabelProjectController = new SensitivityLabelProjectController(
            _mockSensitivityLabelBusiness.Object,
            _mockLogger.Object);

        UserContextStorage.UserId = UserId;
        UserContextStorage.OrganizationId = OrgId;
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
        IEnumerable<SensitivityLabelResponseDto> expected = new List<SensitivityLabelResponseDto>();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(
                UserId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), OrgId, true))
            .ReturnsAsync(expected);

        var result = (await _sensitivityLabelProjectController.GetAllSensitivityLabels(
            ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_Returns200_WithEmptyList()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(
                UserId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), OrgId, true))
            .ReturnsAsync([]);

        var result = (await _sensitivityLabelProjectController.GetAllSensitivityLabels(
            ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<SensitivityLabelResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllSensitivityLabels_ThrowsException_WhenBusinessThrows()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(
                UserId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), OrgId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelProjectController.GetAllSensitivityLabels(
            ProjectId, true));
    }

    [Fact]
    public async Task GetAllSensitivityLabels_RebuildsProjectIdArray_AndPassesOrganizationIdFromContext()
    {
        IEnumerable<SensitivityLabelResponseDto> expected = new List<SensitivityLabelResponseDto>();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(
                UserId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), OrgId, true))
            .ReturnsAsync(expected);

        await _sensitivityLabelProjectController.GetAllSensitivityLabels(ProjectId, true);

        _mockSensitivityLabelBusiness.Verify(
            b => b.GetAllSensitivityLabels(
                UserId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), OrgId, true),
            Times.Once);
    }

    [Fact]
    public void GetAllSensitivityLabels_HasHttpGetAndReadSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.GetAllSensitivityLabels),
            "projectId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // GetSensitivityLabel Tests
    // =========================================================================

    #region GetSensitivityLabel Tests

    [Fact]
    public async Task GetSensitivityLabel_Returns200_WithSensitivityLabel()
    {
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(LabelId, ProjectId, OrgId, true))
            .ReturnsAsync(expected);

        var result = (await _sensitivityLabelProjectController.GetSensitivityLabel(
            ProjectId, LabelId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetSensitivityLabel_Returns200_WithNullSensitivityLabel()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(LabelId, ProjectId, OrgId, true))
            .ReturnsAsync((SensitivityLabelResponseDto)null!);

        var result = (await _sensitivityLabelProjectController.GetSensitivityLabel(
            ProjectId, LabelId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetSensitivityLabel_ThrowsException_WhenBusinessThrows()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(LabelId, ProjectId, OrgId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelProjectController.GetSensitivityLabel(
            ProjectId, LabelId, true));
    }

    [Fact]
    public async Task GetSensitivityLabel_PassesProjectIdFromRouteAndOrganizationIdFromContext()
    {
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(LabelId, ProjectId, OrgId, true))
            .ReturnsAsync(expected);

        await _sensitivityLabelProjectController.GetSensitivityLabel(ProjectId, LabelId, true);

        _mockSensitivityLabelBusiness.Verify(
            b => b.GetSensitivityLabel(LabelId, ProjectId, OrgId, true),
            Times.Once);
    }

    [Fact]
    public void GetSensitivityLabel_HasHttpGetAndReadSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.GetSensitivityLabel),
            "projectId", "labelId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // CreateSensitivityLabel Tests
    // =========================================================================

    #region CreateSensitivityLabel Tests

    [Fact]
    public async Task CreateSensitivityLabel_Returns200_WithSensitivityLabel()
    {
        var input = new CreateSensitivityLabelRequestDto();
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(UserId, input, ProjectId, OrgId))
            .ReturnsAsync(expected);

        var result = (await _sensitivityLabelProjectController.CreateSensitivityLabel(
            ProjectId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateSensitivityLabel_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateSensitivityLabelRequestDto();
        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(UserId, input, ProjectId, OrgId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelProjectController.CreateSensitivityLabel(
            ProjectId, input));
    }

    [Fact]
    public async Task CreateSensitivityLabel_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var input = new CreateSensitivityLabelRequestDto();
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(UserId, input, ProjectId, OrgId))
            .ReturnsAsync(expected);

        await _sensitivityLabelProjectController.CreateSensitivityLabel(ProjectId, input);

        _mockSensitivityLabelBusiness.Verify(
            b => b.CreateSensitivityLabel(UserId, input, ProjectId, OrgId),
            Times.Once);
    }

    [Fact]
    public void CreateSensitivityLabel_HasHttpPostAndWriteSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.CreateSensitivityLabel),
            "projectId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // UpdateSensitivityLabel Tests
    // =========================================================================

    #region UpdateSensitivityLabel Tests

    [Fact]
    public async Task UpdateSensitivityLabel_Returns200_WithSensitivityLabel()
    {
        var expected = new SensitivityLabelResponseDto();
        var input = new UpdateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.UpdateSensitivityLabel(UserId, LabelId, ProjectId, OrgId, input))
            .ReturnsAsync(expected);

        var result = (await _sensitivityLabelProjectController.UpdateSensitivityLabel(
            ProjectId, LabelId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateSensitivityLabel_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateSensitivityLabelRequestDto();
        _mockSensitivityLabelBusiness
            .Setup(b => b.UpdateSensitivityLabel(UserId, LabelId, ProjectId, OrgId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelProjectController.UpdateSensitivityLabel(
            ProjectId, LabelId, input));
    }

    [Fact]
    public async Task UpdateSensitivityLabel_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var expected = new SensitivityLabelResponseDto();
        var input = new UpdateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.UpdateSensitivityLabel(UserId, LabelId, ProjectId, OrgId, input))
            .ReturnsAsync(expected);

        await _sensitivityLabelProjectController.UpdateSensitivityLabel(ProjectId, LabelId, input);

        _mockSensitivityLabelBusiness.Verify(
            b => b.UpdateSensitivityLabel(UserId, LabelId, ProjectId, OrgId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateSensitivityLabel_HasHttpPutAndUpdateSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.UpdateSensitivityLabel),
            "projectId", "labelId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
        AssertHasAuthAttribute(method, "update", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // DeleteSensitivityLabel Tests
    // =========================================================================

    #region DeleteSensitivityLabel Tests

    [Fact]
    public async Task DeleteSensitivityLabel_Returns200_WithBooleanResult()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(UserId, LabelId, ProjectId, OrgId))
            .ReturnsAsync(true);

        var actionResult = await _sensitivityLabelProjectController.DeleteSensitivityLabel(
            ProjectId, LabelId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteSensitivityLabel_ThrowsException_WhenBusinessThrows()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(UserId, LabelId, ProjectId, OrgId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelProjectController.DeleteSensitivityLabel(
            ProjectId, LabelId));
    }

    [Fact]
    public async Task DeleteSensitivityLabel_PassesOrganizationIdFromContextToBusinessLayer()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(UserId, LabelId, ProjectId, OrgId))
            .ReturnsAsync(true);

        await _sensitivityLabelProjectController.DeleteSensitivityLabel(ProjectId, LabelId);

        _mockSensitivityLabelBusiness.Verify(
            b => b.DeleteSensitivityLabel(UserId, LabelId, ProjectId, OrgId),
            Times.Once);
    }

    [Fact]
    public void DeleteSensitivityLabel_HasHttpDeleteAndWriteSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.DeleteSensitivityLabel),
            "projectId", "labelId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "write", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // ArchiveSensitivityLabel Tests
    // =========================================================================

    #region ArchiveSensitivityLabel Tests

    [Fact]
    public async Task ArchiveSensitivityLabel_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId))
            .ReturnsAsync(true);

        var actionResult = await _sensitivityLabelProjectController.ArchiveSensitivityLabel(
            ProjectId, LabelId, archive: true);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockSensitivityLabelBusiness.Verify(
            b => b.ArchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId),
            Times.Once);
        _mockSensitivityLabelBusiness.Verify(
            b => b.UnarchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId))
            .ReturnsAsync(true);

        var actionResult = await _sensitivityLabelProjectController.ArchiveSensitivityLabel(
            ProjectId, LabelId, archive: false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockSensitivityLabelBusiness.Verify(
            b => b.UnarchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId),
            Times.Once);
        _mockSensitivityLabelBusiness.Verify(
            b => b.ArchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _sensitivityLabelProjectController.ArchiveSensitivityLabel(ProjectId, LabelId, archive: true));
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _sensitivityLabelProjectController.ArchiveSensitivityLabel(ProjectId, LabelId, archive: false));
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_PassesOrganizationIdFromContextToBusinessLayer_WhenArchiving()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId))
            .ReturnsAsync(true);

        await _sensitivityLabelProjectController.ArchiveSensitivityLabel(ProjectId, LabelId, archive: true);

        _mockSensitivityLabelBusiness.Verify(
            b => b.ArchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveSensitivityLabel_PassesOrganizationIdFromContextToBusinessLayer_WhenUnarchiving()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId))
            .ReturnsAsync(true);

        await _sensitivityLabelProjectController.ArchiveSensitivityLabel(ProjectId, LabelId, archive: false);

        _mockSensitivityLabelBusiness.Verify(
            b => b.UnarchiveSensitivityLabel(UserId, LabelId, ProjectId, OrgId),
            Times.Once);
    }

    [Fact]
    public void ArchiveSensitivityLabel_HasHttpPatchAndUpdateSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelProjectController.ArchiveSensitivityLabel),
            "projectId", "labelId", "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
        AssertHasAuthAttribute(method, "update", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void SensitivityLabelProjectController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(SensitivityLabelProjectController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
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

    private static void AssertHasAuthAttribute(
        System.Reflection.MethodInfo method,
        string expectedAction,
        string expectedResource)
    {
        var authAttributes = method.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "AuthAttribute")
            .ToList();

        Assert.Contains(authAttributes, attribute =>
            attribute.ConstructorArguments.Count >= 2 &&
            attribute.ConstructorArguments[0].Value?.ToString() == expectedAction &&
            attribute.ConstructorArguments[1].Value?.ToString() == expectedResource);
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
}