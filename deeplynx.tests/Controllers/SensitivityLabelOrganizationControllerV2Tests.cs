using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for the V2 actions of <see cref="SensitivityLabelOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class SensitivityLabelOrganizationControllerTestsV2 : IDisposable
{
    private readonly Mock<ISensitivityLabelBusiness> _mockSensitivityLabelBusiness;
    private readonly Mock<ILogger<SensitivityLabelOrganizationController>> _mockLogger;
    private readonly SensitivityLabelOrganizationController _sensitivityLabelOrganizationController;

    private const long OrgId = 1L;
    private const long UserId = 10L;
    private const long LabelId = 8L;
    private static readonly long[] ProjectIds = { 11L, 13L };

    public SensitivityLabelOrganizationControllerTestsV2()
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
    // GetAllSensitivityLabelsV2 Tests
    // =========================================================================

    #region GetAllSensitivityLabelsV2 Tests

    [Fact]
    public async Task GetAllSensitivityLabelsV2_Returns200_WithSensLabels()
    {
        IEnumerable<SensitivityLabelResponseDto> expected = new List<SensitivityLabelResponseDto>();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
            .ReturnsAsync(expected);

        var result = (await _sensitivityLabelOrganizationController.GetAllSensitivityLabelsV2(
            OrgId, ProjectIds, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllSensitivityLabelsV2_Returns200_WithEmptyList()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
            .ReturnsAsync([]);

        var result = (await _sensitivityLabelOrganizationController.GetAllSensitivityLabelsV2(
            OrgId, ProjectIds, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<SensitivityLabelResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllSensitivityLabelsV2_ThrowsException_WhenBusinessThrows()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelOrganizationController.GetAllSensitivityLabelsV2(
            OrgId, ProjectIds, true));
    }

    [Fact]
    public async Task GetAllSensitivityLabelsV2_PassesCurrentUserIdAndProjectIdsToBusinessLayer()
    {
        IEnumerable<SensitivityLabelResponseDto> expected = new List<SensitivityLabelResponseDto>();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
            .ReturnsAsync(expected);

        await _sensitivityLabelOrganizationController.GetAllSensitivityLabelsV2(OrgId, ProjectIds, true);

        _mockSensitivityLabelBusiness.Verify(
            b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true),
            Times.Once);
    }

    [Fact]
    public void GetAllSensitivityLabelsV2_HasHttpGetAndReadSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.GetAllSensitivityLabelsV2),
            "organizationId", "projectIds", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // GetSensitivityLabelV2 Tests
    // =========================================================================

    #region GetSensitivityLabelV2 Tests

    [Fact]
    public async Task GetSensitivityLabelV2_Returns200_WithSensitivityLabel()
    {
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(LabelId, null, OrgId, true))
            .ReturnsAsync(expected);

        var result = (await _sensitivityLabelOrganizationController.GetSensitivityLabelV2(
            OrgId, LabelId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetSensitivityLabelV2_Returns200_WithNullSensitivityLabel()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(LabelId, null, OrgId, true))
            .ReturnsAsync((SensitivityLabelResponseDto)null!);

        var result = (await _sensitivityLabelOrganizationController.GetSensitivityLabelV2(
            OrgId, LabelId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetSensitivityLabelV2_ThrowsException_WhenBusinessThrows()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(LabelId, null, OrgId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelOrganizationController.GetSensitivityLabelV2(
            OrgId, LabelId, true));
    }

    [Fact]
    public async Task GetSensitivityLabelV2_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.GetSensitivityLabel(LabelId, null, OrgId, true))
            .ReturnsAsync(expected);

        await _sensitivityLabelOrganizationController.GetSensitivityLabelV2(OrgId, LabelId, true);

        _mockSensitivityLabelBusiness.Verify(
            b => b.GetSensitivityLabel(LabelId, null, OrgId, true),
            Times.Once);
    }

    [Fact]
    public void GetSensitivityLabelV2_HasHttpGetAndReadSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.GetSensitivityLabelV2),
            "organizationId", "labelId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // CreateSensitivityLabelV2 Tests
    // =========================================================================

    #region CreateSensitivityLabelV2 Tests

    [Fact]
    public async Task CreateSensitivityLabelV2_Returns200_WithSensitivityLabel()
    {
        var input = new CreateSensitivityLabelRequestDto();
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(UserId, input, null, OrgId))
            .ReturnsAsync(expected);

        var result = (await _sensitivityLabelOrganizationController.CreateSensitivityLabelV2(
            OrgId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateSensitivityLabelV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateSensitivityLabelRequestDto();
        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(UserId, input, null, OrgId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelOrganizationController.CreateSensitivityLabelV2(
            OrgId, input));
    }

    [Fact]
    public async Task CreateSensitivityLabelV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new CreateSensitivityLabelRequestDto();
        var expected = new SensitivityLabelResponseDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.CreateSensitivityLabel(UserId, input, null, OrgId))
            .ReturnsAsync(expected);

        await _sensitivityLabelOrganizationController.CreateSensitivityLabelV2(OrgId, input);

        _mockSensitivityLabelBusiness.Verify(
            b => b.CreateSensitivityLabel(UserId, input, null, OrgId),
            Times.Once);
    }

    [Fact]
    public void CreateSensitivityLabelV2_HasHttpPostAndWriteSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.CreateSensitivityLabelV2),
            "organizationId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // UpdateSensitivityLabelV2 Tests
    // =========================================================================

    #region UpdateSensitivityLabelV2 Tests

    [Fact]
    public async Task UpdateSensitivityLabelV2_Returns200_WithSensitivityLabel()
    {
        var expected = new SensitivityLabelResponseDto();
        var input = new UpdateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.UpdateSensitivityLabel(UserId, LabelId, null, OrgId, input))
            .ReturnsAsync(expected);

        var result = (await _sensitivityLabelOrganizationController.UpdateSensitivityLabelV2(
            OrgId, LabelId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateSensitivityLabelV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateSensitivityLabelRequestDto();
        _mockSensitivityLabelBusiness
            .Setup(b => b.UpdateSensitivityLabel(UserId, LabelId, null, OrgId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelOrganizationController.UpdateSensitivityLabelV2(
            OrgId, LabelId, input));
    }

    [Fact]
    public async Task UpdateSensitivityLabelV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var expected = new SensitivityLabelResponseDto();
        var input = new UpdateSensitivityLabelRequestDto();

        _mockSensitivityLabelBusiness
            .Setup(b => b.UpdateSensitivityLabel(UserId, LabelId, null, OrgId, input))
            .ReturnsAsync(expected);

        await _sensitivityLabelOrganizationController.UpdateSensitivityLabelV2(OrgId, LabelId, input);

        _mockSensitivityLabelBusiness.Verify(
            b => b.UpdateSensitivityLabel(UserId, LabelId, null, OrgId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateSensitivityLabelV2_HasHttpPutAndUpdateSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.UpdateSensitivityLabelV2),
            "organizationId", "labelId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
        AssertHasAuthAttribute(method, "update", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // DeleteSensitivityLabelV2 Tests
    // =========================================================================

    #region DeleteSensitivityLabelV2 Tests

    [Fact]
    public async Task DeleteSensitivityLabelV2_Returns200_WithBooleanResult()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(UserId, LabelId, null, OrgId))
            .ReturnsAsync(true);

        var result = await _sensitivityLabelOrganizationController.DeleteSensitivityLabelV2(
            OrgId, LabelId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteSensitivityLabelV2_ThrowsException_WhenBusinessThrows()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(UserId, LabelId, null, OrgId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelOrganizationController.DeleteSensitivityLabelV2(
            OrgId, LabelId));
    }

    [Fact]
    public async Task DeleteSensitivityLabelV2_PassesNullProjectIdToBusinessLayer()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.DeleteSensitivityLabel(UserId, LabelId, null, OrgId))
            .ReturnsAsync(true);

        await _sensitivityLabelOrganizationController.DeleteSensitivityLabelV2(OrgId, LabelId);

        _mockSensitivityLabelBusiness.Verify(
            b => b.DeleteSensitivityLabel(UserId, LabelId, null, OrgId),
            Times.Once);
    }

    [Fact]
    public void DeleteSensitivityLabelV2_HasHttpDeleteAndWriteSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.DeleteSensitivityLabelV2),
            "organizationId", "labelId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "write", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // ArchiveSensitivityLabelV2 Tests
    // =========================================================================

    #region ArchiveSensitivityLabelV2 Tests

    [Fact]
    public async Task ArchiveSensitivityLabelV2_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId))
            .ReturnsAsync(true);

        var result = await _sensitivityLabelOrganizationController.ArchiveSensitivityLabelV2(
            OrgId, LabelId, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockSensitivityLabelBusiness.Verify(
            b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId),
            Times.Once);
        _mockSensitivityLabelBusiness.Verify(
            b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveSensitivityLabelV2_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId))
            .ReturnsAsync(true);

        var result = await _sensitivityLabelOrganizationController.ArchiveSensitivityLabelV2(
            OrgId, LabelId, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockSensitivityLabelBusiness.Verify(
            b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId),
            Times.Once);
        _mockSensitivityLabelBusiness.Verify(
            b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveSensitivityLabelV2_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _sensitivityLabelOrganizationController.ArchiveSensitivityLabelV2(OrgId, LabelId, archive: true));
    }

    [Fact]
    public async Task ArchiveSensitivityLabelV2_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _sensitivityLabelOrganizationController.ArchiveSensitivityLabelV2(OrgId, LabelId, archive: false));
    }

    [Fact]
    public async Task ArchiveSensitivityLabelV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer_WhenArchiving()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId))
            .ReturnsAsync(true);

        await _sensitivityLabelOrganizationController.ArchiveSensitivityLabelV2(OrgId, LabelId, archive: true);

        _mockSensitivityLabelBusiness.Verify(
            b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveSensitivityLabelV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer_WhenUnarchiving()
    {
        _mockSensitivityLabelBusiness
            .Setup(b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId))
            .ReturnsAsync(true);

        await _sensitivityLabelOrganizationController.ArchiveSensitivityLabelV2(OrgId, LabelId, archive: false);

        _mockSensitivityLabelBusiness.Verify(
            b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId),
            Times.Once);
    }

    [Fact]
    public void ArchiveSensitivityLabelV2_HasHttpPatchAndUpdateSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(SensitivityLabelOrganizationController.ArchiveSensitivityLabelV2),
            "organizationId", "labelId", "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
        AssertHasAuthAttribute(method, "update", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void SensitivityLabelOrganizationController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(SensitivityLabelOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void SensitivityLabelOrganizationController_HasForbidServiceAccountsAttribute()
    {
        Assert.Contains(typeof(SensitivityLabelOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "ForbidServiceAccountsAttribute");
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
        return Assert.Single(typeof(SensitivityLabelOrganizationController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}