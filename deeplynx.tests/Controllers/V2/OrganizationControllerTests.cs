using System.Reflection;
using deeplynx.api.Controllers.V2;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]
public class OrganizationControllerTests : IDisposable
{
    private const long OrgId = 1L;
    private const long TargetUserId = 20L;
    private const long UserId = 10L;
    private const string UserEmail = "test@example.com";

    private readonly OrganizationController _controller;
    private readonly Mock<IInvitationBusiness> _mockInvitationBusiness;
    private readonly Mock<IOrganizationBusiness> _mockOrganizationBusiness;

    public OrganizationControllerTests()
    {
        _mockInvitationBusiness = new Mock<IInvitationBusiness>();
        _mockOrganizationBusiness = new Mock<IOrganizationBusiness>();
        var mockLogger = new Mock<ILogger<OrganizationController>>();

        _controller = new OrganizationController(
            _mockOrganizationBusiness.Object,
            _mockInvitationBusiness.Object,
            mockLogger.Object);

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

    [Fact]
    public async Task GetAllOrganizations_ReturnsOrganizationsAndPassesContext()
    {
        var expected = new PaginatedResponse<OrganizationResponseDto>
        {
            Items = new List<OrganizationResponseDto> { new(), new() },
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 2
        };
        UserContextStorage.IsSysAdmin = true;
        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizationsPaginated(UserId, It.IsAny<PaginatedRequestDto>(), false, true))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetAllOrganizations(false, It.IsAny<PaginatedRequestDto>());

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockOrganizationBusiness.Verify(
            b => b.GetAllOrganizationsPaginated(UserId, It.IsAny<PaginatedRequestDto>(), false, true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllOrganizations_PropagatesUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizationsPaginated(It.IsAny<long>(), It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetAllOrganizations(true));
    }

    [Fact]
    public async Task GetAllOrganizationsForUser_ReturnsOrganizationsAndPassesContext()
    {
        var expected = new PaginatedResponse<OrganizationResponseDto>
        {
            Items = new List<OrganizationResponseDto> { new(), new() },
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 2
        };
        UserContextStorage.IsSysAdmin = true;
        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizationsForUserPaginated(UserId, It.IsAny<PaginatedRequestDto>(), false, true))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetAllOrganizationsForUser(false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockOrganizationBusiness.Verify(
            b => b.GetAllOrganizationsForUserPaginated(UserId, It.IsAny<PaginatedRequestDto>(), false, true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllOrganizationsForUser_PropagatesUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizationsForUserPaginated(It.IsAny<long>(), It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetAllOrganizationsForUser(true));
    }

    [Fact]
    public async Task GetOrganization_ReturnsOrganization()
    {
        var expected = new OrganizationResponseDto { Id = OrgId };
        _mockOrganizationBusiness
            .Setup(b => b.GetOrganization(OrgId, false))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetOrganization(OrgId, false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockOrganizationBusiness.Verify(b => b.GetOrganization(OrgId, false), Times.Once);
    }

    [Fact]
    public async Task GetOrganization_PropagatesNotFoundException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.GetOrganization(OrgId, true))
            .ThrowsAsync(new KeyNotFoundException("organization not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _controller.GetOrganization(OrgId));
    }

    [Fact]
    public async Task CreateOrganization_ReturnsOrganizationAndPassesCurrentUser()
    {
        var input = new CreateOrganizationRequestDto();
        var expected = new OrganizationResponseDto { Id = OrgId };
        _mockOrganizationBusiness
            .Setup(b => b.CreateOrganization(UserId, input, false))
            .ReturnsAsync(expected);

        var actionResult = await _controller.CreateOrganization(input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockOrganizationBusiness.Verify(
            b => b.CreateOrganization(UserId, input, false),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrganization_PropagatesValidationException()
    {
        var input = new CreateOrganizationRequestDto();
        _mockOrganizationBusiness
            .Setup(b => b.CreateOrganization(UserId, input, false))
            .ThrowsAsync(new System.ComponentModel.DataAnnotations.ValidationException("invalid organization"));

        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(
            () => _controller.CreateOrganization(input));
    }

    [Fact]
    public async Task UpdateOrganization_ReturnsOrganizationAndPassesArguments()
    {
        var input = new UpdateOrganizationRequestDto();
        var expected = new OrganizationResponseDto { Id = OrgId };
        _mockOrganizationBusiness
            .Setup(b => b.UpdateOrganization(UserId, OrgId, input))
            .ReturnsAsync(expected);

        var actionResult = await _controller.UpdateOrganization(OrgId, input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockOrganizationBusiness.Verify(
            b => b.UpdateOrganization(UserId, OrgId, input),
            Times.Once);
    }

    [Fact]
    public async Task UpdateOrganization_PropagatesUnexpectedException()
    {
        var input = new UpdateOrganizationRequestDto();
        _mockOrganizationBusiness
            .Setup(b => b.UpdateOrganization(UserId, OrgId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.UpdateOrganization(OrgId, input));
    }

    [Fact]
    public async Task DeleteOrganization_ReturnsOkAndPassesOrganizationId()
    {
        _mockOrganizationBusiness
            .Setup(b => b.DeleteOrganization(OrgId))
            .ReturnsAsync(true);

        var actionResult = await _controller.DeleteOrganization(OrgId);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(b => b.DeleteOrganization(OrgId), Times.Once);
    }

    [Fact]
    public async Task DeleteOrganization_PropagatesNotFoundException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.DeleteOrganization(OrgId))
            .ThrowsAsync(new KeyNotFoundException("organization not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _controller.DeleteOrganization(OrgId));
    }

    [Fact]
    public async Task ArchiveOrganization_ArchivesAndReturnsOk()
    {
        _mockOrganizationBusiness
            .Setup(b => b.ArchiveOrganization(UserId, OrgId))
            .ReturnsAsync(true);

        var actionResult = await _controller.ArchiveOrganization(OrgId, true);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(b => b.ArchiveOrganization(UserId, OrgId), Times.Once);
        _mockOrganizationBusiness.Verify(
            b => b.UnarchiveOrganization(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveOrganization_UnarchivesAndReturnsOk()
    {
        _mockOrganizationBusiness
            .Setup(b => b.UnarchiveOrganization(UserId, OrgId))
            .ReturnsAsync(true);

        var actionResult = await _controller.ArchiveOrganization(OrgId, false);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(b => b.UnarchiveOrganization(UserId, OrgId), Times.Once);
        _mockOrganizationBusiness.Verify(
            b => b.ArchiveOrganization(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchiveOrganization_PropagatesUnexpectedException(bool archive)
    {
        if (archive)
        {
            _mockOrganizationBusiness
                .Setup(b => b.ArchiveOrganization(UserId, OrgId))
                .ThrowsAsync(new Exception("db error"));
        }
        else
        {
            _mockOrganizationBusiness
                .Setup(b => b.UnarchiveOrganization(UserId, OrgId))
                .ThrowsAsync(new Exception("db error"));
        }

        await Assert.ThrowsAsync<Exception>(() => _controller.ArchiveOrganization(OrgId, archive));
    }

    [Fact]
    public async Task AddUserToOrganization_ReturnsOkAndPassesArguments()
    {
        const bool isAdmin = true;
        _mockOrganizationBusiness
            .Setup(b => b.AddUserToOrganization(OrgId, TargetUserId, isAdmin))
            .ReturnsAsync(true);

        var actionResult = await _controller.AddUserToOrganization(OrgId, TargetUserId, isAdmin);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(
            b => b.AddUserToOrganization(OrgId, TargetUserId, isAdmin),
            Times.Once);
    }

    [Fact]
    public async Task AddUserToOrganization_PropagatesUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.AddUserToOrganization(OrgId, TargetUserId, false))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(
            () => _controller.AddUserToOrganization(OrgId, TargetUserId));
    }

    [Fact]
    public async Task SetOrganizationAdminStatus_ReturnsOkAndPassesArguments()
    {
        const bool isAdmin = true;
        _mockOrganizationBusiness
            .Setup(b => b.SetOrganizationAdminStatus(OrgId, TargetUserId, isAdmin))
            .ReturnsAsync(true);

        var actionResult = await _controller.SetOrganizationAdminStatus(OrgId, TargetUserId, isAdmin);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(
            b => b.SetOrganizationAdminStatus(OrgId, TargetUserId, isAdmin),
            Times.Once);
    }

    [Fact]
    public async Task SetOrganizationAdminStatus_PropagatesUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.SetOrganizationAdminStatus(OrgId, TargetUserId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(
            () => _controller.SetOrganizationAdminStatus(OrgId, TargetUserId, true));
    }

    [Fact]
    public async Task RemoveUserFromOrganization_ReturnsOkAndPassesArguments()
    {
        _mockOrganizationBusiness
            .Setup(b => b.RemoveUserFromOrganization(OrgId, TargetUserId))
            .ReturnsAsync(true);

        var actionResult = await _controller.RemoveUserFromOrganization(OrgId, TargetUserId);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(
            b => b.RemoveUserFromOrganization(OrgId, TargetUserId),
            Times.Once);
    }

    [Fact]
    public async Task RemoveUserFromOrganization_PropagatesUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.RemoveUserFromOrganization(OrgId, TargetUserId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(
            () => _controller.RemoveUserFromOrganization(OrgId, TargetUserId));
    }

    [Fact]
    public async Task InviteUserToOrganization_ReturnsOkAndPassesArguments()
    {
        _mockInvitationBusiness
            .Setup(b => b.InviteAndAddUserToHierarchy(
                OrgId,
                null,
                null,
                null,
                TargetUserId,
                UserEmail))
            .ReturnsAsync(true);

        var actionResult = await _controller.InviteUserToOrganization(
            OrgId,
            UserEmail,
            TargetUserId);

        var result = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockInvitationBusiness.Verify(
            b => b.InviteAndAddUserToHierarchy(
                OrgId,
                null,
                null,
                null,
                TargetUserId,
                UserEmail),
            Times.Once);
    }

    [Fact]
    public async Task InviteUserToOrganization_PropagatesUnauthorizedException()
    {
        _mockInvitationBusiness
            .Setup(b => b.InviteAndAddUserToHierarchy(
                OrgId,
                null,
                null,
                null,
                TargetUserId,
                UserEmail))
            .ThrowsAsync(new UnauthorizedAccessException("service accounts are prohibited"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _controller.InviteUserToOrganization(OrgId, UserEmail, TargetUserId));
    }

    [Theory]
    [MemberData(nameof(V2ActionMetadata))]
    public void Version2Action_HasExpectedBadgeAndHttpMetadata(
        string methodName,
        string expectedHttpAttribute)
    {
        var method = GetControllerMethod(methodName);

        Assert.DoesNotContain(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "MapToApiVersionAttribute");
        AssertHasAttribute(method, "BadgeAttribute");
        AssertHasAttribute(method, expectedHttpAttribute);
    }

    public static IEnumerable<object[]> V2ActionMetadata()
    {
        yield return
        [
            nameof(OrganizationController.GetAllOrganizations),
            nameof(HttpGetAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.GetAllOrganizationsForUser),
            nameof(HttpGetAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.GetOrganization),
            nameof(HttpGetAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.CreateOrganization),
            nameof(HttpPostAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.UpdateOrganization),
            nameof(HttpPutAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.DeleteOrganization),
            nameof(HttpDeleteAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.ArchiveOrganization),
            nameof(HttpPatchAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.AddUserToOrganization),
            nameof(HttpPostAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.SetOrganizationAdminStatus),
            nameof(HttpPutAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.RemoveUserFromOrganization),
            nameof(HttpDeleteAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.InviteUserToOrganization),
            nameof(HttpPostAttribute)
        ];
    }

    private static readonly HashSet<string> SecurityAttributeNames =
    [
        "AuthAttribute",
        "OrgAdminAttribute",
        "ProjectAdminAttribute",
        "SensitivityAttribute",
        "SysAdminAttribute"
    ];

    private static readonly HashSet<string> HttpAttributeNames =
    [
        nameof(HttpDeleteAttribute),
        nameof(HttpGetAttribute),
        nameof(HttpPatchAttribute),
        nameof(HttpPostAttribute),
        nameof(HttpPutAttribute)
    ];

    private static MethodInfo GetControllerMethod(string methodName)
    {
        return Assert.Single(
            typeof(OrganizationController).GetMethods(),
            method => method.Name == methodName);
    }

    private static void AssertHasAttribute(MethodInfo method, string attributeName)
    {
        Assert.Contains(
            method.GetCustomAttributesData(),
            attribute => attribute.AttributeType.Name == attributeName);
    }

    private static string[] GetAttributeSignatures(
        MethodInfo method,
        IReadOnlySet<string> includedAttributeNames)
    {
        return method.GetCustomAttributesData()
            .Where(attribute => includedAttributeNames.Contains(attribute.AttributeType.Name))
            .Select(attribute =>
                $"{attribute.AttributeType.Name}:" +
                string.Join(",", attribute.ConstructorArguments.Select(argument => argument.Value?.ToString())) +
                ":" +
                string.Join(",", attribute.NamedArguments.Select(argument =>
                    $"{argument.MemberName}={argument.TypedValue.Value}")))
            .OrderBy(signature => signature)
            .ToArray();
    }

}
