using System.Reflection;
using Asp.Versioning;
using deeplynx.api.Controllers;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Scalar.AspNetCore;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class UserControllerV2Tests : IDisposable
{
    private const long UserId = 10L;
    private const long CandidateId = 11L;
    private const long OrganizationId = 20L;
    private const long ProjectId = 30L;

    private readonly Mock<IUserBusiness> _mockUserBusiness = new();
    private readonly UserController _controller;

    public UserControllerV2Tests()
    {
        _controller = new UserController(
            _mockUserBusiness.Object,
            Mock.Of<ILogger<UserController>>());

        UserContextStorage.UserId = UserId;
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
    public async Task GetAllUsersV2_ReturnsUsersAndForwardsAllFilters()
    {
        var expected = new List<UserResponseDto> { new() { Id = UserId } };
        _mockUserBusiness
            .Setup(business => business.GetAllUsers(ProjectId, OrganizationId, true, true, true))
            .ReturnsAsync(expected);

        var result = (await _controller.GetAllUsersV2(
            ProjectId,
            OrganizationId,
            includeArchived: true,
            includeServiceAccounts: true,
            includeTestAccounts: true)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(
            business => business.GetAllUsers(ProjectId, OrganizationId, true, true, true),
            Times.Once);
    }

    [Fact]
    public async Task GetUserV2_ReturnsRequestedUser()
    {
        var expected = new UserResponseDto { Id = UserId };
        _mockUserBusiness.Setup(business => business.GetUser(UserId)).ReturnsAsync(expected);

        var result = (await _controller.GetUserV2(UserId)).Result;

        AssertOkObject(result, expected);
    }

    [Fact]
    public async Task GetLocalDevUserV2_ReturnsLocalDevelopmentUser()
    {
        var expected = new UserResponseDto { Id = UserId };
        _mockUserBusiness.Setup(business => business.GetLocalDevUser()).ReturnsAsync(expected);

        var result = (await _controller.GetLocalDevUserV2()).Result;

        AssertOkObject(result, expected);
    }

    [Fact]
    public async Task CreateUserV2_ReturnsCreatedUserAndForwardsDto()
    {
        var request = new CreateUserRequestDto();
        var expected = new UserResponseDto { Id = UserId };
        _mockUserBusiness.Setup(business => business.CreateUser(request)).ReturnsAsync(expected);

        var result = (await _controller.CreateUserV2(request)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(business => business.CreateUser(request), Times.Once);
    }

    [Fact]
    public async Task CreateTestAccountV2_ReturnsCreatedAccountAndForwardsName()
    {
        const string name = "Test Account";
        var expected = new UserResponseDto { Id = UserId };
        _mockUserBusiness.Setup(business => business.CreateTestAccount(name)).ReturnsAsync(expected);

        var result = (await _controller.CreateTestAccountV2(name)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(business => business.CreateTestAccount(name), Times.Once);
    }

    [Fact]
    public async Task UpdateUserV2_ReturnsUpdatedUserAndForwardsArguments()
    {
        var request = new UpdateUserRequestDto();
        var expected = new UserResponseDto { Id = UserId };
        _mockUserBusiness.Setup(business => business.UpdateUser(UserId, request)).ReturnsAsync(expected);

        var result = (await _controller.UpdateUserV2(UserId, request)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(business => business.UpdateUser(UserId, request), Times.Once);
    }

    [Fact]
    public async Task DeleteUserV2_ReturnsEmptyOkAndForwardsUserId()
    {
        _mockUserBusiness.Setup(business => business.DeleteUser(UserId)).ReturnsAsync(true);

        var result = await _controller.DeleteUserV2(UserId);

        Assert.IsType<OkObjectResult>(result);
        _mockUserBusiness.Verify(business => business.DeleteUser(UserId), Times.Once);
    }

    [Fact]
    public async Task ArchiveUserV2_WhenArchiveIsTrue_ReturnsEmptyOkAndArchivesUser()
    {
        _mockUserBusiness.Setup(business => business.ArchiveUser(UserId)).ReturnsAsync(true);

        var result = await _controller.ArchiveUserV2(UserId, archive: true);

        Assert.IsType<OkObjectResult>(result);
        _mockUserBusiness.Verify(business => business.ArchiveUser(UserId), Times.Once);
        _mockUserBusiness.Verify(business => business.UnarchiveUser(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveUserV2_WhenArchiveIsFalse_ReturnsEmptyOkAndUnarchivesUser()
    {
        _mockUserBusiness.Setup(business => business.UnarchiveUser(UserId)).ReturnsAsync(true);

        var result = await _controller.ArchiveUserV2(UserId, archive: false);

        Assert.IsType<OkObjectResult>(result);
        _mockUserBusiness.Verify(business => business.UnarchiveUser(UserId), Times.Once);
        _mockUserBusiness.Verify(business => business.ArchiveUser(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task SetSysAdminV2_ReturnsEmptyOkAndUsesAuthenticatedUserAsAuthorizer()
    {
        _mockUserBusiness
            .Setup(business => business.SetSysAdmin(UserId, CandidateId, false))
            .ReturnsAsync(true);

        var result = await _controller.SetSysAdminV2(CandidateId, isAdmin: false);

        Assert.IsType<OkObjectResult>(result);
        _mockUserBusiness.Verify(
            business => business.SetSysAdmin(UserId, CandidateId, false),
            Times.Once);
    }

    [Fact]
    public async Task GetDataOverviewV2_ReturnsRequestedOverview()
    {
        var expected = new DataOverviewDto();
        _mockUserBusiness.Setup(business => business.GetUserOverview(UserId)).ReturnsAsync(expected);

        var result = (await _controller.GetDataOverviewV2(UserId)).Result;

        AssertOkObject(result, expected);
    }

    [Fact]
    public async Task GetCurrentUserV2_ReturnsAdminInfoAndUsesAuthenticatedUser()
    {
        var expected = new UserAdminInfoDto();
        _mockUserBusiness
            .Setup(business => business.GetUserAdminInfo(UserId, OrganizationId, ProjectId))
            .ReturnsAsync(expected);

        var result = (await _controller.GetCurrentUserV2(OrganizationId, ProjectId)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(
            business => business.GetUserAdminInfo(UserId, OrganizationId, ProjectId),
            Times.Once);
    }

    [Fact]
    public async Task GetActiveUserCountsV2_ReturnsCountsAndForwardsFilters()
    {
        var expected = new UserActivityCountsDto();
        _mockUserBusiness
            .Setup(business => business.GetActiveUserCounts(ProjectId, OrganizationId, true))
            .ReturnsAsync(expected);

        var result = (await _controller.GetActiveUserCountsV2(
            ProjectId,
            OrganizationId,
            includeServiceAccounts: true)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(
            business => business.GetActiveUserCounts(ProjectId, OrganizationId, true),
            Times.Once);
    }

    [Fact]
    public async Task GetActiveUsersV2_ReturnsActivityAndForwardsFilters()
    {
        var expected = new UserActivityUsersDto();
        _mockUserBusiness
            .Setup(business => business.GetActiveUsers(ProjectId, OrganizationId, true))
            .ReturnsAsync(expected);

        var result = (await _controller.GetActiveUsersV2(
            ProjectId,
            OrganizationId,
            includeServiceAccounts: true)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(
            business => business.GetActiveUsers(ProjectId, OrganizationId, true),
            Times.Once);
    }

    [Fact]
    public async Task V2ReadAction_DoesNotConvertBusinessExceptionToLegacy500Response()
    {
        var expected = new InvalidOperationException("database failure");
        _mockUserBusiness.Setup(business => business.GetUser(UserId)).ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.GetUserV2(UserId));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task V2WriteAction_DoesNotConvertBusinessExceptionToLegacy500Response()
    {
        var expected = new InvalidOperationException("database failure");
        _mockUserBusiness.Setup(business => business.DeleteUser(UserId)).ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.DeleteUserV2(UserId));

        Assert.Same(expected, actual);
    }

    public static TheoryData<string, string> VersionedActionPairs => new()
    {
        { nameof(UserController.GetAllUsers), nameof(UserController.GetAllUsersV2) },
        { nameof(UserController.GetUser), nameof(UserController.GetUserV2) },
        { nameof(UserController.GetLocalDevUser), nameof(UserController.GetLocalDevUserV2) },
        { nameof(UserController.CreateUser), nameof(UserController.CreateUserV2) },
        { nameof(UserController.CreateTestAccount), nameof(UserController.CreateTestAccountV2) },
        { nameof(UserController.UpdateUser), nameof(UserController.UpdateUserV2) },
        { nameof(UserController.DeleteUser), nameof(UserController.DeleteUserV2) },
        { nameof(UserController.ArchiveUser), nameof(UserController.ArchiveUserV2) },
        { nameof(UserController.SetSysAdmin), nameof(UserController.SetSysAdminV2) },
        { nameof(UserController.GetDataOverview), nameof(UserController.GetDataOverviewV2) },
        { nameof(UserController.GetCurrentUser), nameof(UserController.GetCurrentUserV2) },
        { nameof(UserController.GetActiveUserCounts), nameof(UserController.GetActiveUserCountsV2) },
        { nameof(UserController.GetActiveUsers), nameof(UserController.GetActiveUsersV2) }
    };

    [Theory]
    [MemberData(nameof(VersionedActionPairs))]
    public void VersionedActions_PreserveRouteAndSecurityMetadata(string v1Name, string v2Name)
    {
        var v1 = GetAction(v1Name);
        var v2 = GetAction(v2Name);

        AssertMappedVersion(v1, 1D);
        AssertMappedVersion(v2, 2D);
        Assert.NotNull(v2.GetCustomAttribute<BadgeAttribute>());
        Assert.Equal(GetHttpMetadata(v1), GetHttpMetadata(v2));
        Assert.Equal(GetProtectedMetadata(v1), GetProtectedMetadata(v2));
    }

    private static void AssertMappedVersion(MethodInfo method, double expectedVersion)
    {
        var attribute = Assert.Single(method.GetCustomAttributesData(), metadata =>
            metadata.AttributeType == typeof(MapToApiVersionAttribute));
        Assert.Equal(expectedVersion, GetDeclaredVersion(attribute));
    }

    private static double GetDeclaredVersion(CustomAttributeData attribute)
    {
        return Assert.IsType<double>(attribute.ConstructorArguments[0].Value);
    }

    private static MethodInfo GetAction(string name)
    {
        return Assert.Single(typeof(UserController).GetMethods(), method => method.Name == name);
    }

    private static (string Attribute, string? Template, string? Name) GetHttpMetadata(MethodInfo method)
    {
        var attribute = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
        return (attribute.GetType().Name, attribute.Template, attribute.Name);
    }

    private static string[] GetProtectedMetadata(MethodInfo method)
    {
        return method.GetCustomAttributes()
            .Where(attribute => attribute is AuthAttribute
                or OrgAdminAttribute
                or SysAdminAttribute
                or ForbidServiceAccountsAttribute
                or TagsAttribute)
            .Select(DescribeAttribute)
            .OrderBy(description => description)
            .ToArray();
    }

    private static string DescribeAttribute(object attribute)
    {
        return attribute switch
        {
            AuthAttribute auth => $"Auth:{auth.Action}:{auth.Resource}:{auth.IncludeArchived}",
            OrgAdminAttribute orgAdmin => $"OrgAdmin:{orgAdmin.IncludeArchived}:{orgAdmin.Unscoped}",
            TagsAttribute tags => $"Tags:{string.Join(',', tags.Tags)}",
            _ => attribute.GetType().Name
        };
    }

    private static void AssertOkObject<T>(IActionResult? result, T expected)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }
}
