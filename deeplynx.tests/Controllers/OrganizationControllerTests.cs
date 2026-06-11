using deeplynx.api.Controllers;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class OrganizationControllerTests : IDisposable
{
    private readonly Mock<IInvitationBusiness> _mockInvitationBusiness;
    private readonly Mock<ILogger<OrganizationController>> _mockLogger;
    private readonly Mock<IOrganizationBusiness> _mockOrganizationBusiness;
    private readonly OrganizationController _organizationController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long DataSourceId = 20L;
    private const long RecordIdConst = 7L;
    private const long ClassId = 30L;
    private const long TagId = 40L;
    private const long LabelId = 50L;
    private const long NotFoundId = 99L;
    private const long TargetUserId = 20L;
    private const string UserEmail = "test@example.com";

    public OrganizationControllerTests()
    {
        _mockInvitationBusiness = new Mock<IInvitationBusiness>();
        _mockLogger = new Mock<ILogger<OrganizationController>>();
        _mockOrganizationBusiness = new Mock<IOrganizationBusiness>();

        _organizationController = new OrganizationController(
            _mockOrganizationBusiness.Object,
            _mockInvitationBusiness.Object,
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
    // GetAllOrganizations Tests
    // =========================================================================

    #region GetAllOrganizations Tests

    [Fact]
    public async Task GetAllOrganizations_Returns200_WithOrganizations()
    {
        // Arrange
        IEnumerable<OrganizationResponseDto> expected =
            new List<OrganizationResponseDto>();

        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizations(UserId, true, false))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _organizationController.GetAllOrganizations(true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockOrganizationBusiness.Verify(
            b => b.GetAllOrganizations(UserId, true, false),
            Times.Once);
    }

    [Fact]
    public async Task GetAllOrganizations_Returns200_WithEmptyList()
    {
        // Arrange

        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizations(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _organizationController.GetAllOrganizations(true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetAllOrganizations_Returns500_OnUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizations(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        var result = (await _organizationController.GetAllOrganizations(true)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetAllOrganizations_PassesFiltersAndAdminFlagsToBusinessLayer()
    {
        // Arrange
        const bool hideArchived = false;

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new List<OrganizationResponseDto>();

        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizations(UserId, hideArchived, true))
            .ReturnsAsync(expected);

        // Act
        await _organizationController.GetAllOrganizations(hideArchived);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.GetAllOrganizations(UserId, hideArchived, true),
            Times.Once);
    }

    [Fact]
    public void GetAllOrganizations_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(OrganizationController.GetAllOrganizations),
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetAllOrganizationsForUser Tests
    // =========================================================================

    #region GetAllOrganizationsForUser Tests

    [Fact]
    public async Task GetAllOrganizationsForUser_Returns200_WithOrganizations()
    {
        // Arrange
        IEnumerable<OrganizationResponseDto> expected =
            new List<OrganizationResponseDto>();

        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizationsForUser(UserId, true, false))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _organizationController.GetAllOrganizationsForUser(true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockOrganizationBusiness.Verify(
            b => b.GetAllOrganizationsForUser(UserId, true, false),
            Times.Once);
    }

    [Fact]
    public async Task GetAllOrganizationsForUser_Returns200_WithEmptyList()
    {
        // Arrange

        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizationsForUser(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _organizationController.GetAllOrganizationsForUser(true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetAllOrganizationsForUser_Returns500_OnUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizationsForUser(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        var result = (await _organizationController.GetAllOrganizationsForUser(true)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetAllOrganizationsForUser_PassesFiltersAndAdminFlagsToBusinessLayer()
    {
        // Arrange
        const bool hideArchived = false;

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new List<OrganizationResponseDto>();

        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizationsForUser(UserId, hideArchived, true))
            .ReturnsAsync(expected);

        // Act
        await _organizationController.GetAllOrganizationsForUser(hideArchived);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.GetAllOrganizationsForUser(UserId, hideArchived, true),
            Times.Once);
    }

    [Fact]
    public void GetAllOrganizationsForUser_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(OrganizationController.GetAllOrganizationsForUser),
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetOrganization Tests
    // =========================================================================

    #region GetOrganization Tests

    [Fact]
    public async Task GetOrganization_Returns200_WithOrganization()
    {
        // Arrange
        OrganizationResponseDto expected =
            new OrganizationResponseDto();

        _mockOrganizationBusiness
            .Setup(b => b.GetOrganization(OrgId, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _organizationController.GetOrganization(OrgId, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockOrganizationBusiness.Verify(
            b => b.GetOrganization(OrgId, true),
            Times.Once);
    }

    [Fact]
    public async Task GetOrganization_Returns200_WithNullOrganization()
    {
        // Arrange
        const long organizationId = OrgId;
        const bool hideArchived = true;

        _mockOrganizationBusiness
            .Setup(b => b.GetOrganization(organizationId, hideArchived))
            .ReturnsAsync((OrganizationResponseDto)null!);

        // Act
        var actionResult = await _organizationController.GetOrganization(
            organizationId,
            hideArchived);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetOrganization_Returns500_OnUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.GetOrganization(It.IsAny<long>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        var result = (await _organizationController.GetOrganization(It.IsAny<long>(), It.IsAny<bool>())).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetOrganization_PassesFiltersAndAdminFlagsToBusinessLayer()
    {
        // Arrange
        const bool hideArchived = false;

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new OrganizationResponseDto();

        _mockOrganizationBusiness
            .Setup(b => b.GetOrganization(OrgId, hideArchived))
            .ReturnsAsync(expected);

        // Act
        await _organizationController.GetOrganization(OrgId, hideArchived);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.GetOrganization(OrgId, hideArchived),
            Times.Once);
    }

    [Fact]
    public void GetOrganization_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(OrganizationController.GetOrganization),
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // CreateOrganization Tests
    // =========================================================================

    #region CreateOrganization Tests

    [Fact]
    public async Task CreateOrganization_Returns200_WithOrganization()
    {
        // Arrange
        OrganizationResponseDto expected =
            new OrganizationResponseDto();
        CreateOrganizationRequestDto input = new CreateOrganizationRequestDto();

        _mockOrganizationBusiness
            .Setup(b => b.CreateOrganization(UserId, input, false))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _organizationController.CreateOrganization(input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockOrganizationBusiness.Verify(
            b => b.CreateOrganization(UserId, input, false),
            Times.Once);
    }


    [Fact]
    public async Task CreateOrganization_Returns500_OnUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.CreateOrganization(It.IsAny<long>(), It.IsAny<CreateOrganizationRequestDto>(), false))
            .ThrowsAsync(new Exception("db error"));

        var result = (await _organizationController.CreateOrganization(It.IsAny<CreateOrganizationRequestDto>())).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateOrganization_PassesFiltersAndAdminFlagsToBusinessLayer()
    {
        // Arrange
        CreateOrganizationRequestDto input = new CreateOrganizationRequestDto();

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new OrganizationResponseDto();

        _mockOrganizationBusiness
            .Setup(b => b.CreateOrganization(UserId, input, false))
            .ReturnsAsync(expected);

        // Act
        await _organizationController.CreateOrganization(input);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.CreateOrganization(UserId, input, false),
            Times.Once);
    }

    [Fact]
    public void CreateOrganization_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(OrganizationController.CreateOrganization),
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // UpdateOrganization Tests
    // =========================================================================

    #region UpdateOrganization Tests

    [Fact]
    public async Task UpdateOrganization_Returns200_WithOrganization()
    {
        // Arrange
        OrganizationResponseDto expected =
            new OrganizationResponseDto();
        UpdateOrganizationRequestDto input = new UpdateOrganizationRequestDto();

        _mockOrganizationBusiness
            .Setup(b => b.UpdateOrganization(UserId, OrgId, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _organizationController.UpdateOrganization(OrgId, input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockOrganizationBusiness.Verify(
            b => b.UpdateOrganization(UserId, OrgId, input),
            Times.Once);
    }


    [Fact]
    public async Task UpdateOrganization_Returns500_OnUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.UpdateOrganization(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<UpdateOrganizationRequestDto>()))
            .ThrowsAsync(new Exception("db error"));

        var result = (await _organizationController.UpdateOrganization(It.IsAny<long>(), It.IsAny<UpdateOrganizationRequestDto>())).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdateOrganization_PassesFiltersAndAdminFlagsToBusinessLayer()
    {
        // Arrange
        UpdateOrganizationRequestDto input = new UpdateOrganizationRequestDto();

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new OrganizationResponseDto();

        _mockOrganizationBusiness
            .Setup(b => b.UpdateOrganization(UserId, OrgId, input))
            .ReturnsAsync(expected);

        // Act
        await _organizationController.UpdateOrganization(OrgId, input);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.UpdateOrganization(UserId, OrgId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateOrganization_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(OrganizationController.UpdateOrganization),
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
    }

    #endregion

    // =========================================================================
    // DeleteOrganization Tests
    // =========================================================================

    #region DeleteOrganization Tests

    [Fact]
    public async Task DeleteOrganization_Returns200_WithMessage()
    {
        // Arrange
        var expectedMessage = $"Deleted organization {OrgId}";

        _mockOrganizationBusiness
            .Setup(b => b.DeleteOrganization(OrgId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _organizationController.DeleteOrganization(OrgId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);

        var messageProperty = result.Value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var actualMessage = messageProperty.GetValue(result.Value) as string;
        Assert.Equal(expectedMessage, actualMessage);

        _mockOrganizationBusiness.Verify(
            b => b.DeleteOrganization(OrgId),
            Times.Once);
    }

    [Fact]
    public async Task DeleteOrganization_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockOrganizationBusiness
            .Setup(b => b.DeleteOrganization(It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _organizationController.DeleteOrganization(OrgId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while deleting organization {OrgId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task DeleteOrganization_PassesOrganizationIdToBusinessLayer()
    {
        // Arrange
        _mockOrganizationBusiness
            .Setup(b => b.DeleteOrganization(OrgId))
            .ReturnsAsync(true);

        // Act
        await _organizationController.DeleteOrganization(OrgId);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.DeleteOrganization(OrgId),
            Times.Once);
    }

    [Fact]
    public void DeleteOrganization_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(OrganizationController.DeleteOrganization),
            "organizationId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // ArchiveOrganization Tests
    // =========================================================================

    #region ArchiveOrganization Tests

    [Fact]
    public async Task ArchiveOrganization_Returns200_WithArchivedMessage()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockOrganizationBusiness
            .Setup(b => b.ArchiveOrganization(UserId, OrgId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _organizationController.ArchiveOrganization(OrgId, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Archived organization {OrgId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task ArchiveOrganization_Returns200_WithUnarchivedMessage()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockOrganizationBusiness
            .Setup(b => b.UnarchiveOrganization(UserId, OrgId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _organizationController.ArchiveOrganization(OrgId, false);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Unarchived organization {OrgId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task ArchiveOrganization_Returns500_OnArchiveException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockOrganizationBusiness
            .Setup(b => b.ArchiveOrganization(UserId, OrgId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _organizationController.ArchiveOrganization(OrgId, true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while archiving organization {OrgId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task ArchiveOrganization_Returns500_OnUnarchiveException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockOrganizationBusiness
            .Setup(b => b.UnarchiveOrganization(UserId, OrgId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _organizationController.ArchiveOrganization(OrgId, false);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while unarchiving organization {OrgId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task ArchiveOrganization_PassesUserIdAndOrganizationIdToBusinessLayer_WhenArchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockOrganizationBusiness
            .Setup(b => b.ArchiveOrganization(UserId, OrgId))
            .ReturnsAsync(true);

        // Act
        await _organizationController.ArchiveOrganization(OrgId, true);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.ArchiveOrganization(UserId, OrgId),
            Times.Once);

        _mockOrganizationBusiness.Verify(
            b => b.UnarchiveOrganization(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveOrganization_PassesUserIdAndOrganizationIdToBusinessLayer_WhenUnarchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockOrganizationBusiness
            .Setup(b => b.UnarchiveOrganization(UserId, OrgId))
            .ReturnsAsync(true);

        // Act
        await _organizationController.ArchiveOrganization(OrgId, false);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.UnarchiveOrganization(UserId, OrgId),
            Times.Once);

        _mockOrganizationBusiness.Verify(
            b => b.ArchiveOrganization(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public void ArchiveOrganization_HasHttpPatch()
    {
        var method = GetControllerMethod(
            nameof(OrganizationController.ArchiveOrganization),
            "organizationId",
            "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
    }

    #endregion

    // =========================================================================
    // AddUserToOrganization Tests
    // =========================================================================

    #region AddUserToOrganization Tests

    [Fact]
    public async Task AddUserToOrganization_Returns200_WithMessage()
    {
        // Arrange
        const bool isAdmin = true;

        _mockOrganizationBusiness
            .Setup(b => b.AddUserToOrganization(OrgId, TargetUserId, isAdmin))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _organizationController.AddUserToOrganization(
            OrgId,
            TargetUserId,
            isAdmin);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Added user {TargetUserId} to organization {OrgId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task AddUserToOrganization_Returns500_OnUnexpectedException()
    {
        // Arrange
        const bool isAdmin = true;

        _mockOrganizationBusiness
            .Setup(b => b.AddUserToOrganization(OrgId, TargetUserId, isAdmin))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _organizationController.AddUserToOrganization(
            OrgId,
            TargetUserId,
            isAdmin);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while adding user {TargetUserId} to organization {OrgId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task AddUserToOrganization_PassesArgumentsToBusinessLayer()
    {
        // Arrange
        const bool isAdmin = true;

        _mockOrganizationBusiness
            .Setup(b => b.AddUserToOrganization(OrgId, TargetUserId, isAdmin))
            .ReturnsAsync(true);

        // Act
        await _organizationController.AddUserToOrganization(
            OrgId,
            TargetUserId,
            isAdmin);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.AddUserToOrganization(OrgId, TargetUserId, isAdmin),
            Times.Once);
    }

    [Fact]
    public void AddUserToOrganization_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(OrganizationController.AddUserToOrganization),
            "organizationId",
            "userId",
            "isAdmin");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // SetOrganizationAdminStatus Tests
    // =========================================================================

    #region SetOrganizationAdminStatus Tests

    [Fact]
    public async Task SetOrganizationAdminStatus_Returns200_WithMessage()
    {
        // Arrange
        const bool isAdmin = true;

        _mockOrganizationBusiness
            .Setup(b => b.SetOrganizationAdminStatus(OrgId, TargetUserId, isAdmin))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _organizationController.SetOrganizationAdminStatus(
            OrgId,
            TargetUserId,
            isAdmin);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Adjusted admin status for user {TargetUserId} in organization {OrgId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task SetOrganizationAdminStatus_Returns500_OnUnexpectedException()
    {
        // Arrange
        const bool isAdmin = true;

        _mockOrganizationBusiness
            .Setup(b => b.SetOrganizationAdminStatus(OrgId, TargetUserId, isAdmin))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _organizationController.SetOrganizationAdminStatus(
            OrgId,
            TargetUserId,
            isAdmin);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains(
            $"An error occurred while setting admin status for user {TargetUserId} in organization {OrgId}",
            message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task SetOrganizationAdminStatus_PassesArgumentsToBusinessLayer()
    {
        // Arrange
        const bool isAdmin = true;

        _mockOrganizationBusiness
            .Setup(b => b.SetOrganizationAdminStatus(OrgId, TargetUserId, isAdmin))
            .ReturnsAsync(true);

        // Act
        await _organizationController.SetOrganizationAdminStatus(
            OrgId,
            TargetUserId,
            isAdmin);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.SetOrganizationAdminStatus(OrgId, TargetUserId, isAdmin),
            Times.Once);
    }

    [Fact]
    public void SetOrganizationAdminStatus_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(OrganizationController.SetOrganizationAdminStatus),
            "organizationId",
            "userId",
            "isAdmin");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
    }

    #endregion

    // =========================================================================
    // RemoveUserFromOrganization Tests
    // =========================================================================

    #region RemoveUserFromOrganization Tests

    [Fact]
    public async Task RemoveUserFromOrganization_Returns200_WithMessage()
    {
        // Arrange
        _mockOrganizationBusiness
            .Setup(b => b.RemoveUserFromOrganization(OrgId, TargetUserId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _organizationController.RemoveUserFromOrganization(
            OrgId,
            TargetUserId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Removed user {TargetUserId} from organization {OrgId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task RemoveUserFromOrganization_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockOrganizationBusiness
            .Setup(b => b.RemoveUserFromOrganization(OrgId, TargetUserId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _organizationController.RemoveUserFromOrganization(
            OrgId,
            TargetUserId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while removing user {TargetUserId} from organization {OrgId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task RemoveUserFromOrganization_PassesArgumentsToBusinessLayer()
    {
        // Arrange
        _mockOrganizationBusiness
            .Setup(b => b.RemoveUserFromOrganization(OrgId, TargetUserId))
            .ReturnsAsync(true);

        // Act
        await _organizationController.RemoveUserFromOrganization(
            OrgId,
            TargetUserId);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.RemoveUserFromOrganization(OrgId, TargetUserId),
            Times.Once);
    }

    [Fact]
    public void RemoveUserFromOrganization_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(OrganizationController.RemoveUserFromOrganization),
            "organizationId",
            "userId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // InviteUserToOrganization Tests
    // =========================================================================

    #region InviteUserToOrganization Tests

    [Fact]
    public async Task InviteUserToOrganization_Returns200_WithMessage()
    {
        // Arrange
        _mockInvitationBusiness
            .Setup(b => b.InviteAndAddUserToHierarchy(
                OrgId,
                (long?)null,
                (long?)null,
                (long?)null,
                TargetUserId,
                UserEmail))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _organizationController.InviteUserToOrganization(
            OrgId,
            UserEmail,
            TargetUserId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Invited and added inactive user with email {UserEmail} to organization {OrgId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task InviteUserToOrganization_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockInvitationBusiness
            .Setup(b => b.InviteAndAddUserToHierarchy(
                OrgId,
                (long?)null,
                (long?)null,
                (long?)null,
                TargetUserId,
                UserEmail))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _organizationController.InviteUserToOrganization(
            OrgId,
            UserEmail,
            TargetUserId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while adding user with email {UserEmail} to organization {OrgId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task InviteUserToOrganization_PassesArgumentsToInvitationBusinessLayer()
    {
        // Arrange
        _mockInvitationBusiness
            .Setup(b => b.InviteAndAddUserToHierarchy(
                OrgId,
                (long?)null,
                (long?)null,
                (long?)null,
                TargetUserId,
                UserEmail))
            .ReturnsAsync(true);

        // Act
        await _organizationController.InviteUserToOrganization(
            OrgId,
            UserEmail,
            TargetUserId);

        // Assert
        _mockInvitationBusiness.Verify(
            b => b.InviteAndAddUserToHierarchy(
                OrgId,
                (long?)null,
                (long?)null,
                (long?)null,
                TargetUserId,
                UserEmail),
            Times.Once);
    }

    [Fact]
    public void InviteUserToOrganization_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(OrganizationController.InviteUserToOrganization),
            "organizationId",
            "userEmail",
            "userId");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
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
        return Assert.Single(typeof(OrganizationController).GetMethods()
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