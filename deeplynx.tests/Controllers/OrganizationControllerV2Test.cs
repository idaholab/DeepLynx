using System.Reflection;
using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class OrganizationControllerV2Tests : IDisposable
{
    private const long OrgId = 1L;
    private const long TargetUserId = 20L;
    private const long UserId = 10L;
    private const string UserEmail = "test@example.com";

    private readonly OrganizationController _controller;
    private readonly Mock<IInvitationBusiness> _mockInvitationBusiness;
    private readonly Mock<IOrganizationBusiness> _mockOrganizationBusiness;

    public OrganizationControllerV2Tests()
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
    public async Task GetAllOrganizationsV2_ReturnsOrganizationsAndPassesContext()
    {
        var expected = new List<OrganizationResponseDto> { new() { Id = OrgId } };
        UserContextStorage.IsSysAdmin = true;
        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizations(UserId, false, true))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetAllOrganizationsV2(false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockOrganizationBusiness.Verify(
            b => b.GetAllOrganizations(UserId, false, true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllOrganizationsV2_PropagatesUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizations(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetAllOrganizationsV2(true));
    }

    [Fact]
    public async Task GetAllOrganizationsForUserV2_ReturnsOrganizationsAndPassesContext()
    {
        var expected = new List<OrganizationResponseDto> { new() { Id = OrgId } };
        UserContextStorage.IsSysAdmin = true;
        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizationsForUser(UserId, false, true))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetAllOrganizationsForUserV2(false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockOrganizationBusiness.Verify(
            b => b.GetAllOrganizationsForUser(UserId, false, true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllOrganizationsForUserV2_PropagatesUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.GetAllOrganizationsForUser(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetAllOrganizationsForUserV2(true));
    }

    [Fact]
    public async Task GetOrganizationV2_ReturnsOrganization()
    {
        var expected = new OrganizationResponseDto { Id = OrgId };
        _mockOrganizationBusiness
            .Setup(b => b.GetOrganization(OrgId, false))
            .ReturnsAsync(expected);

        var actionResult = await _controller.GetOrganizationV2(OrgId, false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockOrganizationBusiness.Verify(b => b.GetOrganization(OrgId, false), Times.Once);
    }

    [Fact]
    public async Task GetOrganizationV2_PropagatesNotFoundException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.GetOrganization(OrgId, true))
            .ThrowsAsync(new KeyNotFoundException("organization not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _controller.GetOrganizationV2(OrgId));
    }

    [Fact]
    public async Task CreateOrganizationV2_ReturnsOrganizationAndPassesCurrentUser()
    {
        var input = new CreateOrganizationRequestDto();
        var expected = new OrganizationResponseDto { Id = OrgId };
        _mockOrganizationBusiness
            .Setup(b => b.CreateOrganization(UserId, input, false))
            .ReturnsAsync(expected);

        var actionResult = await _controller.CreateOrganizationV2(input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockOrganizationBusiness.Verify(
            b => b.CreateOrganization(UserId, input, false),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrganizationV2_PropagatesValidationException()
    {
        var input = new CreateOrganizationRequestDto();
        _mockOrganizationBusiness
            .Setup(b => b.CreateOrganization(UserId, input, false))
            .ThrowsAsync(new System.ComponentModel.DataAnnotations.ValidationException("invalid organization"));

        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(
            () => _controller.CreateOrganizationV2(input));
    }

    [Fact]
    public async Task UpdateOrganizationV2_ReturnsOrganizationAndPassesArguments()
    {
        var input = new UpdateOrganizationRequestDto();
        var expected = new OrganizationResponseDto { Id = OrgId };
        _mockOrganizationBusiness
            .Setup(b => b.UpdateOrganization(UserId, OrgId, input))
            .ReturnsAsync(expected);

        var actionResult = await _controller.UpdateOrganizationV2(OrgId, input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(expected, result.Value);
        _mockOrganizationBusiness.Verify(
            b => b.UpdateOrganization(UserId, OrgId, input),
            Times.Once);
    }

    [Fact]
    public async Task UpdateOrganizationV2_PropagatesUnexpectedException()
    {
        var input = new UpdateOrganizationRequestDto();
        _mockOrganizationBusiness
            .Setup(b => b.UpdateOrganization(UserId, OrgId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.UpdateOrganizationV2(OrgId, input));
    }

    [Fact]
    public async Task DeleteOrganizationV2_ReturnsOkAndPassesOrganizationId()
    {
        _mockOrganizationBusiness
            .Setup(b => b.DeleteOrganization(OrgId))
            .ReturnsAsync(true);

        var actionResult = await _controller.DeleteOrganizationV2(OrgId);

        var result = Assert.IsType<OkResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(b => b.DeleteOrganization(OrgId), Times.Once);
    }

    [Fact]
    public async Task DeleteOrganizationV2_PropagatesNotFoundException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.DeleteOrganization(OrgId))
            .ThrowsAsync(new KeyNotFoundException("organization not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _controller.DeleteOrganizationV2(OrgId));
    }

    [Fact]
    public async Task ArchiveOrganizationV2_ArchivesAndReturnsOk()
    {
        _mockOrganizationBusiness
            .Setup(b => b.ArchiveOrganization(UserId, OrgId))
            .ReturnsAsync(true);

        var actionResult = await _controller.ArchiveOrganizationV2(OrgId, true);

        var result = Assert.IsType<OkResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(b => b.ArchiveOrganization(UserId, OrgId), Times.Once);
        _mockOrganizationBusiness.Verify(
            b => b.UnarchiveOrganization(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveOrganizationV2_UnarchivesAndReturnsOk()
    {
        _mockOrganizationBusiness
            .Setup(b => b.UnarchiveOrganization(UserId, OrgId))
            .ReturnsAsync(true);

        var actionResult = await _controller.ArchiveOrganizationV2(OrgId, false);

        var result = Assert.IsType<OkResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(b => b.UnarchiveOrganization(UserId, OrgId), Times.Once);
        _mockOrganizationBusiness.Verify(
            b => b.ArchiveOrganization(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchiveOrganizationV2_PropagatesUnexpectedException(bool archive)
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

        await Assert.ThrowsAsync<Exception>(() => _controller.ArchiveOrganizationV2(OrgId, archive));
    }

    [Fact]
    public async Task AddUserToOrganizationV2_ReturnsOkAndPassesArguments()
    {
        const bool isAdmin = true;
        _mockOrganizationBusiness
            .Setup(b => b.AddUserToOrganization(OrgId, TargetUserId, isAdmin))
            .ReturnsAsync(true);

        var actionResult = await _controller.AddUserToOrganizationV2(OrgId, TargetUserId, isAdmin);

        var result = Assert.IsType<OkResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(
            b => b.AddUserToOrganization(OrgId, TargetUserId, isAdmin),
            Times.Once);
    }

    [Fact]
    public async Task AddUserToOrganizationV2_PropagatesUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.AddUserToOrganization(OrgId, TargetUserId, false))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(
            () => _controller.AddUserToOrganizationV2(OrgId, TargetUserId));
    }

    [Fact]
    public async Task SetOrganizationAdminStatusV2_ReturnsOkAndPassesArguments()
    {
        const bool isAdmin = true;
        _mockOrganizationBusiness
            .Setup(b => b.SetOrganizationAdminStatus(OrgId, TargetUserId, isAdmin))
            .ReturnsAsync(true);

        var actionResult = await _controller.SetOrganizationAdminStatusV2(OrgId, TargetUserId, isAdmin);

        var result = Assert.IsType<OkResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(
            b => b.SetOrganizationAdminStatus(OrgId, TargetUserId, isAdmin),
            Times.Once);
    }

    [Fact]
    public async Task SetOrganizationAdminStatusV2_PropagatesUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.SetOrganizationAdminStatus(OrgId, TargetUserId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(
            () => _controller.SetOrganizationAdminStatusV2(OrgId, TargetUserId, true));
    }

    [Fact]
    public async Task RemoveUserFromOrganizationV2_ReturnsOkAndPassesArguments()
    {
        _mockOrganizationBusiness
            .Setup(b => b.RemoveUserFromOrganization(OrgId, TargetUserId))
            .ReturnsAsync(true);

        var actionResult = await _controller.RemoveUserFromOrganizationV2(OrgId, TargetUserId);

        var result = Assert.IsType<OkResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        _mockOrganizationBusiness.Verify(
            b => b.RemoveUserFromOrganization(OrgId, TargetUserId),
            Times.Once);
    }

    [Fact]
    public async Task RemoveUserFromOrganizationV2_PropagatesUnexpectedException()
    {
        _mockOrganizationBusiness
            .Setup(b => b.RemoveUserFromOrganization(OrgId, TargetUserId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(
            () => _controller.RemoveUserFromOrganizationV2(OrgId, TargetUserId));
    }

    [Fact]
    public async Task InviteUserToOrganizationV2_ReturnsOkAndPassesArguments()
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

        var actionResult = await _controller.InviteUserToOrganizationV2(
            OrgId,
            UserEmail,
            TargetUserId);

        var result = Assert.IsType<OkResult>(actionResult);
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
    public async Task InviteUserToOrganizationV2_PropagatesUnauthorizedException()
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
            () => _controller.InviteUserToOrganizationV2(OrgId, UserEmail, TargetUserId));
    }

    [Theory]
    [MemberData(nameof(V2ActionMetadata))]
    public void V2Action_HasExpectedVersionBadgeHttpMetadataAndSecurityAttributes(
        string v1MethodName,
        string v2MethodName,
        string expectedHttpAttribute)
    {
        var v1Method = GetControllerMethod(v1MethodName);
        var v2Method = GetControllerMethod(v2MethodName);

        AssertHasAttribute(v2Method, "MapToApiVersionAttribute");
        AssertHasAttribute(v2Method, "BadgeAttribute");
        AssertHasAttribute(v2Method, expectedHttpAttribute);
        Assert.Equal(
            GetAttributeSignatures(v1Method, SecurityAttributeNames),
            GetAttributeSignatures(v2Method, SecurityAttributeNames));
        Assert.Equal(
            GetAttributeSignatures(v1Method, HttpAttributeNames),
            GetAttributeSignatures(v2Method, HttpAttributeNames));
    }

    public static IEnumerable<object[]> V2ActionMetadata()
    {
        yield return
        [
            nameof(OrganizationController.GetAllOrganizations),
            nameof(OrganizationController.GetAllOrganizationsV2),
            nameof(HttpGetAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.GetAllOrganizationsForUser),
            nameof(OrganizationController.GetAllOrganizationsForUserV2),
            nameof(HttpGetAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.GetOrganization),
            nameof(OrganizationController.GetOrganizationV2),
            nameof(HttpGetAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.CreateOrganization),
            nameof(OrganizationController.CreateOrganizationV2),
            nameof(HttpPostAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.UpdateOrganization),
            nameof(OrganizationController.UpdateOrganizationV2),
            nameof(HttpPutAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.DeleteOrganization),
            nameof(OrganizationController.DeleteOrganizationV2),
            nameof(HttpDeleteAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.ArchiveOrganization),
            nameof(OrganizationController.ArchiveOrganizationV2),
            nameof(HttpPatchAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.AddUserToOrganization),
            nameof(OrganizationController.AddUserToOrganizationV2),
            nameof(HttpPostAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.SetOrganizationAdminStatus),
            nameof(OrganizationController.SetOrganizationAdminStatusV2),
            nameof(HttpPutAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.RemoveUserFromOrganization),
            nameof(OrganizationController.RemoveUserFromOrganizationV2),
            nameof(HttpDeleteAttribute)
        ];
        yield return
        [
            nameof(OrganizationController.InviteUserToOrganization),
            nameof(OrganizationController.InviteUserToOrganizationV2),
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
