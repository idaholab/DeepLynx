using System.Reflection;
using Asp.Versioning;
using deeplynx.api.Controllers.V2;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Scalar.AspNetCore;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]
public class UserControllerTests : IDisposable
{
    private const long UserId = 10L;
    private const long CandidateId = 11L;
    private const long OrganizationId = 20L;
    private const long ProjectId = 30L;

    private readonly Mock<IUserBusiness> _mockUserBusiness = new();
    private readonly UserController _controller;

    public UserControllerTests()
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
    public async Task GetAllUsers_ReturnsUsersAndForwardsAllFiltersAndPagination()
    {
        var paginatedRequest = new PaginatedRequestDto { PageNumber = 2, PageSize = 10 };
        var expected = new PaginatedResponse<UserResponseDto>
        {
            Items = new List<UserResponseDto> { new() { Id = UserId } },
            PageNumber = 2,
            PageSize = 10,
            TotalCount = 11
        };
        _mockUserBusiness
            .Setup(business => business.GetAllUsersPaginated(
                paginatedRequest, ProjectId, OrganizationId, true, true, true))
            .ReturnsAsync(expected);

        var result = (await _controller.GetAllUsers(
            ProjectId,
            OrganizationId,
            includeArchived: true,
            includeServiceAccounts: true,
            includeTestAccounts: true,
            paginatedRequestDto: paginatedRequest)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(
            business => business.GetAllUsersPaginated(
                paginatedRequest, ProjectId, OrganizationId, true, true, true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllUsers_WhenPaginationDtoIsNull_UsesDefaultPagination()
    {
        var expected = new PaginatedResponse<UserResponseDto>
        {
            Items = new List<UserResponseDto> { new() { Id = UserId } },
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 1
        };
        _mockUserBusiness
            .Setup(business => business.GetAllUsersPaginated(
                It.Is<PaginatedRequestDto>(dto => dto.PageNumber == 1 && dto.PageSize == 25),
                ProjectId,
                OrganizationId,
                false,
                false,
                false))
            .ReturnsAsync(expected);

        var result = (await _controller.GetAllUsers(
            ProjectId,
            OrganizationId,
            paginatedRequestDto: null)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(
            business => business.GetAllUsersPaginated(
                It.Is<PaginatedRequestDto>(dto => dto.PageNumber == 1 && dto.PageSize == 25),
                ProjectId,
                OrganizationId,
                false,
                false,
                false),
            Times.Once);
    }

    [Fact]
    public async Task GetUser_ReturnsRequestedUser()
    {
        var expected = new UserResponseDto { Id = UserId };
        _mockUserBusiness.Setup(business => business.GetUser(UserId)).ReturnsAsync(expected);

        var result = (await _controller.GetUser(UserId)).Result;

        AssertOkObject(result, expected);
    }

    [Fact]
    public async Task GetLocalDevUser_ReturnsLocalDevelopmentUser()
    {
        var expected = new UserResponseDto { Id = UserId };
        _mockUserBusiness.Setup(business => business.GetLocalDevUser()).ReturnsAsync(expected);

        var result = (await _controller.GetLocalDevUser()).Result;

        AssertOkObject(result, expected);
    }

    [Fact]
    public async Task CreateUser_ReturnsCreatedUserAndForwardsDto()
    {
        var request = new CreateUserRequestDto();
        var expected = new UserResponseDto { Id = UserId };
        _mockUserBusiness.Setup(business => business.CreateUser(request)).ReturnsAsync(expected);

        var result = (await _controller.CreateUser(request)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(business => business.CreateUser(request), Times.Once);
    }

    [Fact]
    public async Task CreateTestAccount_ReturnsCreatedAccountAndForwardsName()
    {
        const string name = "Test Account";
        var expected = new UserResponseDto { Id = UserId };
        _mockUserBusiness.Setup(business => business.CreateTestAccount(name)).ReturnsAsync(expected);

        var result = (await _controller.CreateTestAccount(name)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(business => business.CreateTestAccount(name), Times.Once);
    }

    [Fact]
    public async Task UpdateUser_ReturnsUpdatedUserAndForwardsArguments()
    {
        var request = new UpdateUserRequestDto();
        var expected = new UserResponseDto { Id = UserId };
        _mockUserBusiness.Setup(business => business.UpdateUser(UserId, request)).ReturnsAsync(expected);

        var result = (await _controller.UpdateUser(UserId, request)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(business => business.UpdateUser(UserId, request), Times.Once);
    }

    [Fact]
    public async Task DeleteUser_ReturnsEmptyOkAndForwardsUserId()
    {
        _mockUserBusiness.Setup(business => business.DeleteUser(UserId)).ReturnsAsync(true);

        var result = await _controller.DeleteUser(UserId);

        Assert.IsType<OkObjectResult>(result);
        _mockUserBusiness.Verify(business => business.DeleteUser(UserId), Times.Once);
    }

    [Fact]
    public async Task ArchiveUser_WhenArchiveIsTrue_ReturnsEmptyOkAndArchivesUser()
    {
        _mockUserBusiness.Setup(business => business.ArchiveUser(UserId)).ReturnsAsync(true);

        var result = await _controller.ArchiveUser(UserId, archive: true);

        Assert.IsType<OkObjectResult>(result);
        _mockUserBusiness.Verify(business => business.ArchiveUser(UserId), Times.Once);
        _mockUserBusiness.Verify(business => business.UnarchiveUser(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveUser_WhenArchiveIsFalse_ReturnsEmptyOkAndUnarchivesUser()
    {
        _mockUserBusiness.Setup(business => business.UnarchiveUser(UserId)).ReturnsAsync(true);

        var result = await _controller.ArchiveUser(UserId, archive: false);

        Assert.IsType<OkObjectResult>(result);
        _mockUserBusiness.Verify(business => business.UnarchiveUser(UserId), Times.Once);
        _mockUserBusiness.Verify(business => business.ArchiveUser(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task SetSysAdmin_ReturnsEmptyOkAndUsesAuthenticatedUserAsAuthorizer()
    {
        _mockUserBusiness
            .Setup(business => business.SetSysAdmin(UserId, CandidateId, false))
            .ReturnsAsync(true);

        var result = await _controller.SetSysAdmin(CandidateId, isAdmin: false);

        Assert.IsType<OkObjectResult>(result);
        _mockUserBusiness.Verify(
            business => business.SetSysAdmin(UserId, CandidateId, false),
            Times.Once);
    }

    [Fact]
    public async Task GetDataOverview_ReturnsRequestedOverview()
    {
        var expected = new DataOverviewDto();
        _mockUserBusiness.Setup(business => business.GetUserOverview(UserId)).ReturnsAsync(expected);

        var result = (await _controller.GetDataOverview(UserId)).Result;

        AssertOkObject(result, expected);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsAdminInfoAndUsesAuthenticatedUser()
    {
        var expected = new UserAdminInfoDto();
        _mockUserBusiness
            .Setup(business => business.GetUserAdminInfo(UserId, OrganizationId, ProjectId))
            .ReturnsAsync(expected);

        var result = (await _controller.GetCurrentUser(OrganizationId, ProjectId)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(
            business => business.GetUserAdminInfo(UserId, OrganizationId, ProjectId),
            Times.Once);
    }

    [Fact]
    public async Task GetActiveUserCounts_ReturnsCountsAndForwardsFilters()
    {
        var expected = new UserActivityCountsDto();
        _mockUserBusiness
            .Setup(business => business.GetActiveUserCounts(ProjectId, OrganizationId, true))
            .ReturnsAsync(expected);

        var result = (await _controller.GetActiveUserCounts(
            ProjectId,
            OrganizationId,
            includeServiceAccounts: true)).Result;

        AssertOkObject(result, expected);
        _mockUserBusiness.Verify(
            business => business.GetActiveUserCounts(ProjectId, OrganizationId, true),
            Times.Once);
    }

    [Fact]
    public async Task GetActiveUsers_ReturnsActivityAndForwardsFilters()
    {
        var expected = new UserActivityUsersDto();
        _mockUserBusiness
            .Setup(business => business.GetActiveUsers(ProjectId, OrganizationId, true))
            .ReturnsAsync(expected);

        var result = (await _controller.GetActiveUsers(
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
            () => _controller.GetUser(UserId));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task V2WriteAction_DoesNotConvertBusinessExceptionToLegacy500Response()
    {
        var expected = new InvalidOperationException("database failure");
        _mockUserBusiness.Setup(business => business.DeleteUser(UserId)).ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.DeleteUser(UserId));

        Assert.Same(expected, actual);
    }

    public static TheoryData<string> VersionedActionPairs => new()
    {
        { nameof(UserController.GetAllUsers) },
        { nameof(UserController.GetUser) },
        { nameof(UserController.GetLocalDevUser) },
        { nameof(UserController.CreateUser) },
        { nameof(UserController.CreateTestAccount) },
        { nameof(UserController.UpdateUser) },
        { nameof(UserController.DeleteUser) },
        { nameof(UserController.ArchiveUser) },
        { nameof(UserController.SetSysAdmin) },
        { nameof(UserController.GetDataOverview) },
        { nameof(UserController.GetCurrentUser) },
        { nameof(UserController.GetActiveUserCounts) },
        { nameof(UserController.GetActiveUsers) }
    };

    [Theory]
    [MemberData(nameof(VersionedActionPairs))]
    public void Version2Actions_HaveExpectedMetadata(string methodName)
    {
        var method = GetAction(methodName);

        Assert.DoesNotContain(method.GetCustomAttributesData(), metadata =>
            metadata.AttributeType == typeof(MapToApiVersionAttribute));
        Assert.NotNull(method.GetCustomAttribute<BadgeAttribute>());
        Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
        Assert.NotNull(typeof(UserController).GetCustomAttribute<AuthorizeAttribute>());
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
