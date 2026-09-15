// using deeplynx.api.Controllers.V2;
// using deeplynx.helpers.Context;
// using deeplynx.interfaces;
// using deeplynx.models;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Logging;
// using Moq;
//
// namespace deeplynx.tests.Controllers.V2;
//
// [Collection("Test Suite Collection")]
//
// /// <summary>
// ///     Unit tests for the V2 actions of <see cref="SensitivityLabelOrganizationController"/>.
// ///     All business dependencies are mocked with Moq.
// ///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
// ///
// ///     Implements IDisposable to reset UserContextStorage statics after every test,
// ///     preventing static state leaking across classes when the runner reuses threads.
// /// </summary>
// public class SensitivityLabelOrganizationControllerTests : IDisposable
// {
//     private readonly Mock<ISensitivityLabelBusiness> _mockSensitivityLabelBusiness;
//     private readonly Mock<IUserSensitivityLabelBusiness> _mockUserSensitivityLabelBusiness;
//     private readonly Mock<ILogger<SensitivityLabelOrganizationController>> _mockLogger;
//     private readonly SensitivityLabelOrganizationController _sensitivityLabelOrganizationController;
//
//     private const long OrgId = 1L;
//     private const long UserId = 10L;
//     private const long LabelId = 8L;
//     private static readonly long[] ProjectIds = { 11L, 13L };
//
//     public SensitivityLabelOrganizationControllerTests()
//     {
//         _mockSensitivityLabelBusiness = new Mock<ISensitivityLabelBusiness>();
//         _mockUserSensitivityLabelBusiness = new Mock<IUserSensitivityLabelBusiness>();
//         _mockLogger = new Mock<ILogger<SensitivityLabelOrganizationController>>();
//
//         _sensitivityLabelOrganizationController = new SensitivityLabelOrganizationController(
//             _mockSensitivityLabelBusiness.Object,
//             _mockUserSensitivityLabelBusiness.Object,
//             _mockLogger.Object);
//
//         UserContextStorage.UserId = UserId;
//     }
//
//     public void Dispose()
//     {
//         // Reset to safe sentinels so a mutated value never bleeds into another class's tests
//         UserContextStorage.UserId = default;
//         UserContextStorage.OrganizationId = default;
//         UserContextStorage.IsSysAdmin = default;
//         UserContextStorage.IsOrgAdmin = default;
//         UserContextStorage.IsProjectAdmin = default;
//     }
//
//     // =========================================================================
//     // GetAllSensitivityLabels Tests
//     // =========================================================================
//
//     #region GetAllSensitivityLabels Tests
//
//     [Fact]
//     public async Task GetAllSensitivityLabels_Returns200_WithSensLabels()
//     {
//         IEnumerable<SensitivityLabelResponseDto> expected = new List<SensitivityLabelResponseDto>();
//
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
//             .ReturnsAsync(expected);
//
//         var result = (await _sensitivityLabelOrganizationController.GetAllSensitivityLabels(
//             OrgId, ProjectIds, true)).Result as OkObjectResult;
//
//         Assert.NotNull(result);
//         Assert.Equal(200, result.StatusCode);
//         Assert.Equal(expected, result.Value);
//     }
//
//     [Fact]
//     public async Task GetAllSensitivityLabels_Returns200_WithEmptyList()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
//             .ReturnsAsync([]);
//
//         var result = (await _sensitivityLabelOrganizationController.GetAllSensitivityLabels(
//             OrgId, ProjectIds, true)).Result as OkObjectResult;
//
//         Assert.NotNull(result);
//         Assert.Equal(200, result.StatusCode);
//         Assert.IsAssignableFrom<IEnumerable<SensitivityLabelResponseDto>>(result.Value);
//     }
//
//     [Fact]
//     public async Task GetAllSensitivityLabels_ThrowsException_WhenBusinessThrows()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
//             .ThrowsAsync(new Exception("db error"));
//
//         await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelOrganizationController.GetAllSensitivityLabels(
//             OrgId, ProjectIds, true));
//     }
//
//     [Fact]
//     public async Task GetAllSensitivityLabels_PassesCurrentUserIdAndProjectIdsToBusinessLayer()
//     {
//         IEnumerable<SensitivityLabelResponseDto> expected = new List<SensitivityLabelResponseDto>();
//
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true))
//             .ReturnsAsync(expected);
//
//         await _sensitivityLabelOrganizationController.GetAllSensitivityLabels(OrgId, ProjectIds, true);
//
//         _mockSensitivityLabelBusiness.Verify(
//             b => b.GetAllSensitivityLabels(UserId, ProjectIds, OrgId, true),
//             Times.Once);
//     }
//
//     [Fact]
//     public void GetAllSensitivityLabels_HasHttpGetAndReadSensitivityLabelAuthorization()
//     {
//         var method = GetControllerMethod(
//             nameof(SensitivityLabelOrganizationController.GetAllSensitivityLabels),
//             "organizationId", "projectIds", "hideArchived");
//
//         AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
//         AssertHasAuthAttribute(method, "read", "sensitivity_label");
//     }
//
//     #endregion
//
//     // =========================================================================
//     // GetSensitivityLabel Tests
//     // =========================================================================
//
//     #region GetSensitivityLabel Tests
//
//     [Fact]
//     public async Task GetSensitivityLabel_Returns200_WithSensitivityLabel()
//     {
//         var expected = new SensitivityLabelResponseDto();
//
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.GetSensitivityLabel(LabelId, null, OrgId, true))
//             .ReturnsAsync(expected);
//
//         var result = (await _sensitivityLabelOrganizationController.GetSensitivityLabel(
//             OrgId, LabelId, true)).Result as OkObjectResult;
//
//         Assert.NotNull(result);
//         Assert.Equal(200, result.StatusCode);
//         Assert.Equal(expected, result.Value);
//     }
//
//     [Fact]
//     public async Task GetSensitivityLabel_Returns200_WithNullSensitivityLabel()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.GetSensitivityLabel(LabelId, null, OrgId, true))
//             .ReturnsAsync((SensitivityLabelResponseDto)null!);
//
//         var result = (await _sensitivityLabelOrganizationController.GetSensitivityLabel(
//             OrgId, LabelId, true)).Result as OkObjectResult;
//
//         Assert.NotNull(result);
//         Assert.Equal(200, result.StatusCode);
//         Assert.Null(result.Value);
//     }
//
//     [Fact]
//     public async Task GetSensitivityLabel_ThrowsException_WhenBusinessThrows()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.GetSensitivityLabel(LabelId, null, OrgId, true))
//             .ThrowsAsync(new Exception("db error"));
//
//         await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelOrganizationController.GetSensitivityLabel(
//             OrgId, LabelId, true));
//     }
//
//     [Fact]
//     public async Task GetSensitivityLabel_PassesNullProjectIdToBusinessLayer()
//     {
//         var expected = new SensitivityLabelResponseDto();
//
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.GetSensitivityLabel(LabelId, null, OrgId, true))
//             .ReturnsAsync(expected);
//
//         await _sensitivityLabelOrganizationController.GetSensitivityLabel(OrgId, LabelId, true);
//
//         _mockSensitivityLabelBusiness.Verify(
//             b => b.GetSensitivityLabel(LabelId, null, OrgId, true),
//             Times.Once);
//     }
//
//     [Fact]
//     public void GetSensitivityLabel_HasHttpGetAndReadSensitivityLabelAuthorization()
//     {
//         var method = GetControllerMethod(
//             nameof(SensitivityLabelOrganizationController.GetSensitivityLabel),
//             "organizationId", "labelId", "hideArchived");
//
//         AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
//         AssertHasAuthAttribute(method, "read", "sensitivity_label");
//     }
//
//     #endregion
//
//     // =========================================================================
//     // CreateSensitivityLabel Tests
//     // =========================================================================
//
//     #region CreateSensitivityLabel Tests
//
//     [Fact]
//     public async Task CreateSensitivityLabel_Returns200_WithSensitivityLabel()
//     {
//         var input = new CreateSensitivityLabelRequestDto();
//         var expected = new SensitivityLabelResponseDto();
//
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.CreateSensitivityLabel(UserId, input, null, OrgId))
//             .ReturnsAsync(expected);
//
//         var result = (await _sensitivityLabelOrganizationController.CreateSensitivityLabel(
//             OrgId, input)).Result as OkObjectResult;
//
//         Assert.NotNull(result);
//         Assert.Equal(200, result.StatusCode);
//         Assert.Equal(expected, result.Value);
//     }
//
//     [Fact]
//     public async Task CreateSensitivityLabel_ThrowsException_WhenBusinessThrows()
//     {
//         var input = new CreateSensitivityLabelRequestDto();
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.CreateSensitivityLabel(UserId, input, null, OrgId))
//             .ThrowsAsync(new Exception("db error"));
//
//         await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelOrganizationController.CreateSensitivityLabel(
//             OrgId, input));
//     }
//
//     [Fact]
//     public async Task CreateSensitivityLabel_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
//     {
//         var input = new CreateSensitivityLabelRequestDto();
//         var expected = new SensitivityLabelResponseDto();
//
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.CreateSensitivityLabel(UserId, input, null, OrgId))
//             .ReturnsAsync(expected);
//
//         await _sensitivityLabelOrganizationController.CreateSensitivityLabel(OrgId, input);
//
//         _mockSensitivityLabelBusiness.Verify(
//             b => b.CreateSensitivityLabel(UserId, input, null, OrgId),
//             Times.Once);
//     }
//
//     [Fact]
//     public void CreateSensitivityLabel_HasHttpPostAndWriteSensitivityLabelAuthorization()
//     {
//         var method = GetControllerMethod(
//             nameof(SensitivityLabelOrganizationController.CreateSensitivityLabel),
//             "organizationId", "dto");
//
//         AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
//         AssertHasAuthAttribute(method, "write", "sensitivity_label");
//     }
//
//     #endregion
//
//     // =========================================================================
//     // UpdateSensitivityLabel Tests
//     // =========================================================================
//
//     #region UpdateSensitivityLabel Tests
//
//     [Fact]
//     public async Task UpdateSensitivityLabel_Returns200_WithSensitivityLabel()
//     {
//         var expected = new SensitivityLabelResponseDto();
//         var input = new UpdateSensitivityLabelRequestDto();
//
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.UpdateSensitivityLabel(UserId, LabelId, null, OrgId, input))
//             .ReturnsAsync(expected);
//
//         var result = (await _sensitivityLabelOrganizationController.UpdateSensitivityLabel(
//             OrgId, LabelId, input)).Result as OkObjectResult;
//
//         Assert.NotNull(result);
//         Assert.Equal(200, result.StatusCode);
//         Assert.Equal(expected, result.Value);
//     }
//
//     [Fact]
//     public async Task UpdateSensitivityLabel_ThrowsException_WhenBusinessThrows()
//     {
//         var input = new UpdateSensitivityLabelRequestDto();
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.UpdateSensitivityLabel(UserId, LabelId, null, OrgId, input))
//             .ThrowsAsync(new Exception("db error"));
//
//         await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelOrganizationController.UpdateSensitivityLabel(
//             OrgId, LabelId, input));
//     }
//
//     [Fact]
//     public async Task UpdateSensitivityLabel_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
//     {
//         var expected = new SensitivityLabelResponseDto();
//         var input = new UpdateSensitivityLabelRequestDto();
//
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.UpdateSensitivityLabel(UserId, LabelId, null, OrgId, input))
//             .ReturnsAsync(expected);
//
//         await _sensitivityLabelOrganizationController.UpdateSensitivityLabel(OrgId, LabelId, input);
//
//         _mockSensitivityLabelBusiness.Verify(
//             b => b.UpdateSensitivityLabel(UserId, LabelId, null, OrgId, input),
//             Times.Once);
//     }
//
//     [Fact]
//     public void UpdateSensitivityLabel_HasHttpPutAndUpdateSensitivityLabelAuthorization()
//     {
//         var method = GetControllerMethod(
//             nameof(SensitivityLabelOrganizationController.UpdateSensitivityLabel),
//             "organizationId", "labelId", "dto");
//
//         AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
//         AssertHasAuthAttribute(method, "update", "sensitivity_label");
//     }
//
//     #endregion
//
//     // =========================================================================
//     // DeleteSensitivityLabel Tests
//     // =========================================================================
//
//     #region DeleteSensitivityLabel Tests
//
//     [Fact]
//     public async Task DeleteSensitivityLabel_Returns200_WithBooleanResult()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.DeleteSensitivityLabel(UserId, LabelId, null, OrgId))
//             .ReturnsAsync(true);
//
//         var actionResult = await _sensitivityLabelOrganizationController.DeleteSensitivityLabel(
//             OrgId, LabelId);
//
//         var result = Assert.IsType<OkObjectResult>(actionResult.Result);
//         Assert.Equal(200, result.StatusCode);
//         Assert.Equal(true, result.Value);
//     }
//
//     [Fact]
//     public async Task DeleteSensitivityLabel_ThrowsException_WhenBusinessThrows()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.DeleteSensitivityLabel(UserId, LabelId, null, OrgId))
//             .ThrowsAsync(new Exception("db error"));
//
//         await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelOrganizationController.DeleteSensitivityLabel(
//             OrgId, LabelId));
//     }
//
//     [Fact]
//     public async Task DeleteSensitivityLabel_PassesNullProjectIdToBusinessLayer()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.DeleteSensitivityLabel(UserId, LabelId, null, OrgId))
//             .ReturnsAsync(true);
//
//         await _sensitivityLabelOrganizationController.DeleteSensitivityLabel(OrgId, LabelId);
//
//         _mockSensitivityLabelBusiness.Verify(
//             b => b.DeleteSensitivityLabel(UserId, LabelId, null, OrgId),
//             Times.Once);
//     }
//
//     [Fact]
//     public void DeleteSensitivityLabel_HasHttpDeleteAndWriteSensitivityLabelAuthorization()
//     {
//         var method = GetControllerMethod(
//             nameof(SensitivityLabelOrganizationController.DeleteSensitivityLabel),
//             "organizationId", "labelId");
//
//         AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
//         AssertHasAuthAttribute(method, "write", "sensitivity_label");
//     }
//
//     #endregion
//
//     // =========================================================================
//     // ArchiveSensitivityLabel Tests
//     // =========================================================================
//
//     #region ArchiveSensitivityLabel Tests
//
//     [Fact]
//     public async Task ArchiveSensitivityLabel_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId))
//             .ReturnsAsync(true);
//
//         var actionResult = await _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(
//             OrgId, LabelId, archive: true);
//
//         var result = Assert.IsType<OkObjectResult>(actionResult.Result);
//         Assert.Equal(200, result.StatusCode);
//         Assert.Equal(true, result.Value);
//         _mockSensitivityLabelBusiness.Verify(
//             b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId),
//             Times.Once);
//         _mockSensitivityLabelBusiness.Verify(
//             b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId),
//             Times.Never);
//     }
//
//     [Fact]
//     public async Task ArchiveSensitivityLabel_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId))
//             .ReturnsAsync(true);
//
//         var actionResult = await _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(
//             OrgId, LabelId, archive: false);
//
//         var result = Assert.IsType<OkObjectResult>(actionResult.Result);
//         Assert.Equal(200, result.StatusCode);
//         Assert.Equal(true, result.Value);
//         _mockSensitivityLabelBusiness.Verify(
//             b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId),
//             Times.Once);
//         _mockSensitivityLabelBusiness.Verify(
//             b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId),
//             Times.Never);
//     }
//
//     [Fact]
//     public async Task ArchiveSensitivityLabel_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId))
//             .ThrowsAsync(new Exception("db error"));
//
//         await Assert.ThrowsAsync<Exception>(() =>
//             _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(OrgId, LabelId, archive: true));
//     }
//
//     [Fact]
//     public async Task ArchiveSensitivityLabel_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId))
//             .ThrowsAsync(new Exception("db error"));
//
//         await Assert.ThrowsAsync<Exception>(() =>
//             _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(OrgId, LabelId, archive: false));
//     }
//
//     [Fact]
//     public async Task ArchiveSensitivityLabel_PassesCurrentUserIdAndNullProjectIdToBusinessLayer_WhenArchiving()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId))
//             .ReturnsAsync(true);
//
//         await _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(OrgId, LabelId, archive: true);
//
//         _mockSensitivityLabelBusiness.Verify(
//             b => b.ArchiveSensitivityLabel(UserId, LabelId, null, OrgId),
//             Times.Once);
//     }
//
//     [Fact]
//     public async Task ArchiveSensitivityLabel_PassesCurrentUserIdAndNullProjectIdToBusinessLayer_WhenUnarchiving()
//     {
//         _mockSensitivityLabelBusiness
//             .Setup(b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId))
//             .ReturnsAsync(true);
//
//         await _sensitivityLabelOrganizationController.ArchiveSensitivityLabel(OrgId, LabelId, archive: false);
//
//         _mockSensitivityLabelBusiness.Verify(
//             b => b.UnarchiveSensitivityLabel(UserId, LabelId, null, OrgId),
//             Times.Once);
//     }
//
//     [Fact]
//     public void ArchiveSensitivityLabel_HasHttpPatchAndUpdateSensitivityLabelAuthorization()
//     {
//         var method = GetControllerMethod(
//             nameof(SensitivityLabelOrganizationController.ArchiveSensitivityLabel),
//             "organizationId", "labelId", "archive");
//
//         AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
//         AssertHasAuthAttribute(method, "update", "sensitivity_label");
//     }
//
//     #endregion
//
//     // =========================================================================
//     // GetUserPermissionsForLabel Tests
//     // =========================================================================
//
//     #region GetUserPermissionsForLabel Tests
//
//     [Fact]
//     public async Task GetUserPermissionsForLabel_Returns200_WithPermissions_WhenUserIdSupplied()
//     {
//         const long suppliedUserId = 20L;
//         IEnumerable<UserSensitivityLabelPermissionResponseDto> expected =
//             new List<UserSensitivityLabelPermissionResponseDto>();
//
//         _mockUserSensitivityLabelBusiness
//             .Setup(b => b.GetUserPermissionsForLabel(LabelId, suppliedUserId, OrgId, null))
//             .ReturnsAsync(expected);
//
//         var result = (await _sensitivityLabelOrganizationController.GetUserPermissionsForLabel(
//             OrgId, LabelId, suppliedUserId)).Result as OkObjectResult;
//
//         Assert.NotNull(result);
//         Assert.Equal(200, result.StatusCode);
//         Assert.Equal(expected, result.Value);
//     }
//
//     [Fact]
//     public async Task GetUserPermissionsForLabel_Returns200_WithEmptyList()
//     {
//         _mockUserSensitivityLabelBusiness
//             .Setup(b => b.GetUserPermissionsForLabel(LabelId, UserId, OrgId, null))
//             .ReturnsAsync([]);
//
//         var result = (await _sensitivityLabelOrganizationController.GetUserPermissionsForLabel(
//             OrgId, LabelId, null)).Result as OkObjectResult;
//
//         Assert.NotNull(result);
//         Assert.Equal(200, result.StatusCode);
//         Assert.IsAssignableFrom<IEnumerable<UserSensitivityLabelPermissionResponseDto>>(result.Value);
//     }
//
//     [Fact]
//     public async Task GetUserPermissionsForLabel_ThrowsException_WhenBusinessThrows()
//     {
//         _mockUserSensitivityLabelBusiness
//             .Setup(b => b.GetUserPermissionsForLabel(LabelId, UserId, OrgId, null))
//             .ThrowsAsync(new Exception("db error"));
//
//         await Assert.ThrowsAsync<Exception>(() => _sensitivityLabelOrganizationController.GetUserPermissionsForLabel(
//             OrgId, LabelId, null));
//     }
//
//     [Fact]
//     public async Task GetUserPermissionsForLabel_UsesCurrentUserId_WhenUserIdNotSupplied()
//     {
//         IEnumerable<UserSensitivityLabelPermissionResponseDto> expected =
//             new List<UserSensitivityLabelPermissionResponseDto>();
//
//         _mockUserSensitivityLabelBusiness
//             .Setup(b => b.GetUserPermissionsForLabel(LabelId, UserId, OrgId, null))
//             .ReturnsAsync(expected);
//
//         await _sensitivityLabelOrganizationController.GetUserPermissionsForLabel(OrgId, LabelId, null);
//
//         _mockUserSensitivityLabelBusiness.Verify(
//             b => b.GetUserPermissionsForLabel(LabelId, UserId, OrgId, null),
//             Times.Once);
//     }
//
//     [Fact]
//     public async Task GetUserPermissionsForLabel_UsesSuppliedUserId_InsteadOfCurrentUser()
//     {
//         const long suppliedUserId = 20L;
//         IEnumerable<UserSensitivityLabelPermissionResponseDto> expected =
//             new List<UserSensitivityLabelPermissionResponseDto>();
//
//         _mockUserSensitivityLabelBusiness
//             .Setup(b => b.GetUserPermissionsForLabel(LabelId, suppliedUserId, OrgId, null))
//             .ReturnsAsync(expected);
//
//         await _sensitivityLabelOrganizationController.GetUserPermissionsForLabel(OrgId, LabelId, suppliedUserId);
//
//         _mockUserSensitivityLabelBusiness.Verify(
//             b => b.GetUserPermissionsForLabel(LabelId, suppliedUserId, OrgId, null),
//             Times.Once);
//         _mockUserSensitivityLabelBusiness.Verify(
//             b => b.GetUserPermissionsForLabel(LabelId, UserId, OrgId, null),
//             Times.Never);
//     }
//
//     [Fact]
//     public async Task GetUserPermissionsForLabel_PassesNullProjectIdToBusinessLayer()
//     {
//         IEnumerable<UserSensitivityLabelPermissionResponseDto> expected =
//             new List<UserSensitivityLabelPermissionResponseDto>();
//
//         _mockUserSensitivityLabelBusiness
//             .Setup(b => b.GetUserPermissionsForLabel(LabelId, UserId, OrgId, null))
//             .ReturnsAsync(expected);
//
//         await _sensitivityLabelOrganizationController.GetUserPermissionsForLabel(OrgId, LabelId, null);
//
//         _mockUserSensitivityLabelBusiness.Verify(
//             b => b.GetUserPermissionsForLabel(LabelId, UserId, OrgId, null),
//             Times.Once);
//     }
//
//     [Fact]
//     public void GetUserPermissionsForLabel_HasHttpGetAndReadSensitivityLabelAuthorization()
//     {
//         var method = GetControllerMethod(
//             nameof(SensitivityLabelOrganizationController.GetUserPermissionsForLabel),
//             "organizationId", "labelId", "userId");
//
//         AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
//         AssertHasAuthAttribute(method, "read", "sensitivity_label");
//     }
//
//     #endregion
//     
//     // =========================================================================
//     // Auth / Middleware Metadata Tests
//     // =========================================================================
//
//     #region Auth / Middleware Metadata Tests
//
//     [Fact]
//     public void SensitivityLabelOrganizationController_HasAuthorizeAttribute()
//     {
//         Assert.Contains(typeof(SensitivityLabelOrganizationController).GetCustomAttributesData(), attribute =>
//             attribute.AttributeType.Name == "AuthorizeAttribute");
//     }
//
//     [Fact]
//     public void SensitivityLabelOrganizationController_HasForbidServiceAccountsAttribute()
//     {
//         Assert.Contains(typeof(SensitivityLabelOrganizationController).GetCustomAttributesData(), attribute =>
//             attribute.AttributeType.Name == "ForbidServiceAccountsAttribute");
//     }
//
//     #endregion
//
//     // =========================================================================
//     // Test Helpers
//     // =========================================================================
//
//     private static void AssertHasHttpAttribute(
//         System.Reflection.MethodInfo method,
//         string expectedAttributeName)
//     {
//         Assert.Contains(method.GetCustomAttributesData(), attribute =>
//             attribute.AttributeType.Name == expectedAttributeName);
//     }
//
//     private static void AssertHasAuthAttribute(
//         System.Reflection.MethodInfo method,
//         string expectedAction,
//         string expectedResource)
//     {
//         var authAttributes = method.GetCustomAttributesData()
//             .Where(attribute => attribute.AttributeType.Name == "AuthAttribute")
//             .ToList();
//
//         Assert.Contains(authAttributes, attribute =>
//             attribute.ConstructorArguments.Count >= 2 &&
//             attribute.ConstructorArguments[0].Value?.ToString() == expectedAction &&
//             attribute.ConstructorArguments[1].Value?.ToString() == expectedResource);
//     }
//
//     private static System.Reflection.MethodInfo GetControllerMethod(
//         string methodName,
//         params string[] parameterNames)
//     {
//         return Assert.Single(typeof(SensitivityLabelOrganizationController).GetMethods()
//             .Where(method => method.Name == methodName)
//             .Where(method => parameterNames.All(parameterName =>
//                 method.GetParameters().Any(parameter => parameter.Name == parameterName))));
//     }
// }
