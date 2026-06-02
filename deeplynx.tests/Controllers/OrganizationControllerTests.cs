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
        var actionResult = await _organizationController.GetAllOrganizations();

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
        var actionResult = await _organizationController.GetAllOrganizations();

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

        var result = (await _organizationController.GetAllOrganizations()).Result as ObjectResult;

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
    // GetAllOrganizations Tests
    // =========================================================================

    #region GetAllOrganizations Tests

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
        var actionResult = await _organizationController.GetAllOrganizationsForUser();

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
        var actionResult = await _organizationController.GetAllOrganizationsForUser();

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

        var result = (await _organizationController.GetAllOrganizationsForUser()).Result as ObjectResult;

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
            .Setup(b => b.CreateOrganization(UserId, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _organizationController.CreateOrganization(input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockOrganizationBusiness.Verify(
            b => b.CreateOrganization(UserId, input),
            Times.Once);
    }


    [Fact]
    public async Task CreateOrganization_Returns500_OnUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.CreateOrganization(It.IsAny<long>(), It.IsAny<CreateOrganizationRequestDto>()))
            .ThrowsAsync(new Exception("db error"));

        var result = (await _organizationController.CreateOrganization(It.IsAny<CreateOrganizationRequestDto>())).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateOrganization_PassesFiltersAndAdminFlagsToBusinessLayer()
    {
        // Arrange
        const bool hideArchived = false;
        CreateOrganizationRequestDto input = new CreateOrganizationRequestDto();

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new OrganizationResponseDto();

        _mockOrganizationBusiness
            .Setup(b => b.CreateOrganization(UserId, input))
            .ReturnsAsync(expected);

        // Act
        await _organizationController.CreateOrganization(input);

        // Assert
        _mockOrganizationBusiness.Verify(
            b => b.CreateOrganization(UserId, input),
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
}