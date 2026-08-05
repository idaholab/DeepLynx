using System.Reflection;
using Asp.Versioning;
using deeplynx.api.Controllers.V2;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Scalar.AspNetCore;
using V1GroupController = deeplynx.api.Controllers.V1.GroupController;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]
public class GroupControllerTests : IDisposable
{
    private const long CurrentUserId = 10L;
    private const long OrganizationId = 20L;
    private const long GroupId = 30L;
    private const long MemberId = 40L;

    private readonly Mock<IGroupBusiness> _mockGroupBusiness = new();
    private readonly GroupController _controller;

    public GroupControllerTests()
    {
        _controller = new GroupController(
            _mockGroupBusiness.Object,
            Mock.Of<ILogger<GroupController>>());

        UserContextStorage.UserId = CurrentUserId;
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
    public async Task GetAllGroups_ReturnsGroupsAndForwardsFilters()
    {
        var expected = new List<GroupResponseDto> { new() { Id = GroupId, Name = "Group" } };
        _mockGroupBusiness
            .Setup(business => business.GetAllGroups(OrganizationId, false))
            .ReturnsAsync(expected);

        var result = (await _controller.GetAllGroups(OrganizationId, hideArchived: false)).Result;

        AssertOkObject(result, expected);
        _mockGroupBusiness.Verify(
            business => business.GetAllGroups(OrganizationId, false),
            Times.Once);
    }

    [Fact]
    public async Task GetGroup_ReturnsGroupAndForwardsFilters()
    {
        var expected = new GroupResponseDto { Id = GroupId, Name = "Group" };
        _mockGroupBusiness
            .Setup(business => business.GetGroup(OrganizationId, GroupId, false))
            .ReturnsAsync(expected);

        var result = (await _controller.GetGroup(
            OrganizationId,
            GroupId,
            hideArchived: false)).Result;

        AssertOkObject(result, expected);
        _mockGroupBusiness.Verify(
            business => business.GetGroup(OrganizationId, GroupId, false),
            Times.Once);
    }

    [Fact]
    public async Task GetGroupMembers_ReturnsMembers()
    {
        var expected = new List<UserResponseDto> { new() { Id = MemberId } };
        _mockGroupBusiness
            .Setup(business => business.GetGroupMembers(OrganizationId, GroupId))
            .ReturnsAsync(expected);

        var result = (await _controller.GetGroupMembers(OrganizationId, GroupId)).Result;

        AssertOkObject(result, expected);
    }

    [Fact]
    public async Task CreateGroup_ReturnsGroupAndUsesCurrentUser()
    {
        var request = new CreateGroupRequestDto { Name = "Group" };
        var expected = new GroupResponseDto { Id = GroupId, Name = request.Name };
        _mockGroupBusiness
            .Setup(business => business.CreateGroup(CurrentUserId, OrganizationId, request))
            .ReturnsAsync(expected);

        var result = (await _controller.CreateGroup(OrganizationId, request)).Result;

        AssertOkObject(result, expected);
        _mockGroupBusiness.Verify(
            business => business.CreateGroup(CurrentUserId, OrganizationId, request),
            Times.Once);
    }

    [Fact]
    public async Task UpdateGroup_ReturnsGroupAndUsesCurrentUser()
    {
        var request = new UpdateGroupRequestDto { Name = "Updated Group" };
        var expected = new GroupResponseDto { Id = GroupId, Name = request.Name };
        _mockGroupBusiness
            .Setup(business => business.UpdateGroup(CurrentUserId, OrganizationId, GroupId, request))
            .ReturnsAsync(expected);

        var result = (await _controller.UpdateGroup(OrganizationId, GroupId, request)).Result;

        AssertOkObject(result, expected);
        _mockGroupBusiness.Verify(
            business => business.UpdateGroup(CurrentUserId, OrganizationId, GroupId, request),
            Times.Once);
    }

    [Fact]
    public async Task DeleteGroup_ReturnsBooleanAndUsesCurrentUser()
    {
        _mockGroupBusiness
            .Setup(business => business.DeleteGroup(CurrentUserId, OrganizationId, GroupId))
            .ReturnsAsync(true);

        var result = await _controller.DeleteGroup(OrganizationId, GroupId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<bool>(ok.Value));
        _mockGroupBusiness.Verify(
            business => business.DeleteGroup(CurrentUserId, OrganizationId, GroupId),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveGroup_WhenArchiveIsTrue_ReturnsBooleanAndArchivesGroup()
    {
        _mockGroupBusiness
            .Setup(business => business.ArchiveGroup(CurrentUserId, OrganizationId, GroupId))
            .ReturnsAsync(true);

        var result = await _controller.ArchiveGroup(OrganizationId, GroupId, archive: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<bool>(ok.Value));
        _mockGroupBusiness.Verify(
            business => business.ArchiveGroup(CurrentUserId, OrganizationId, GroupId),
            Times.Once);
        _mockGroupBusiness.Verify(
            business => business.UnarchiveGroup(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveGroup_WhenArchiveIsFalse_ReturnsBooleanAndUnarchivesGroup()
    {
        _mockGroupBusiness
            .Setup(business => business.UnarchiveGroup(CurrentUserId, OrganizationId, GroupId))
            .ReturnsAsync(true);

        var result = await _controller.ArchiveGroup(OrganizationId, GroupId, archive: false);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<bool>(ok.Value));
        _mockGroupBusiness.Verify(
            business => business.UnarchiveGroup(CurrentUserId, OrganizationId, GroupId),
            Times.Once);
        _mockGroupBusiness.Verify(
            business => business.ArchiveGroup(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task AddUserToGroup_ReturnsBooleanAndForwardsArguments()
    {
        _mockGroupBusiness
            .Setup(business => business.AddUserToGroup(MemberId, OrganizationId, GroupId))
            .ReturnsAsync(true);

        var result = await _controller.AddUserToGroup(OrganizationId, GroupId, MemberId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<bool>(ok.Value));
        _mockGroupBusiness.Verify(
            business => business.AddUserToGroup(MemberId, OrganizationId, GroupId),
            Times.Once);
    }

    [Fact]
    public async Task RemoveUserFromGroup_ReturnsBooleanAndForwardsArguments()
    {
        _mockGroupBusiness
            .Setup(business => business.RemoveUserFromGroup(MemberId, OrganizationId, GroupId))
            .ReturnsAsync(true);

        var result = await _controller.RemoveUserFromGroup(OrganizationId, GroupId, MemberId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<bool>(ok.Value));
        _mockGroupBusiness.Verify(
            business => business.RemoveUserFromGroup(MemberId, OrganizationId, GroupId),
            Times.Once);
    }

    [Fact]
    public async Task V1Action_StillConvertsBusinessExceptionToLegacy500Response()
    {
        _mockGroupBusiness
            .Setup(business => business.GetGroup(OrganizationId, GroupId, true))
            .ThrowsAsync(new InvalidOperationException("database failure"));

        var v1Controller = new V1GroupController(
            _mockGroupBusiness.Object,
            Mock.Of<ILogger<V1GroupController>>());

        var result = (await v1Controller.GetGroup(OrganizationId, GroupId)).Result;

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.IsType<string>(error.Value);
    }

    [Fact]
    public async Task V2ReadAction_DoesNotConvertBusinessExceptionToLegacy500Response()
    {
        var expected = new InvalidOperationException("database failure");
        _mockGroupBusiness
            .Setup(business => business.GetGroup(OrganizationId, GroupId, true))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.GetGroup(OrganizationId, GroupId));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task V2BooleanAction_DoesNotConvertBusinessExceptionToLegacy500Response()
    {
        var expected = new InvalidOperationException("database failure");
        _mockGroupBusiness
            .Setup(business => business.DeleteGroup(CurrentUserId, OrganizationId, GroupId))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.DeleteGroup(OrganizationId, GroupId));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Controller_DeclaresExpectedRouteVersionsAndSecurity()
    {
        var controller = typeof(GroupController);
        var versions = controller.GetCustomAttributes<ApiVersionAttribute>().ToList();
        var route = Assert.IsType<RouteAttribute>(controller.GetCustomAttribute<RouteAttribute>());

        Assert.Equal("organizations/{organizationId:long}/groups", route.Template);
        var version = Assert.Single(versions);
        Assert.False(version.Deprecated);
        Assert.Equal(2, Assert.Single(version.Versions).MajorVersion);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<ForbidServiceAccountsAttribute>());
    }

    public static TheoryData<string> VersionedActionPairs => new()
    {
        { nameof(GroupController.GetAllGroups) },
        { nameof(GroupController.GetGroup) },
        { nameof(GroupController.GetGroupMembers) },
        { nameof(GroupController.CreateGroup) },
        { nameof(GroupController.UpdateGroup) },
        { nameof(GroupController.DeleteGroup) },
        { nameof(GroupController.ArchiveGroup) },
        { nameof(GroupController.AddUserToGroup) },
        { nameof(GroupController.RemoveUserFromGroup) }
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
        Assert.NotEmpty(GetAuthMetadata(method));
    }

    private static MethodInfo GetAction(string name)
    {
        return Assert.Single(typeof(GroupController).GetMethods(), method => method.Name == name);
    }


    private static (string Attribute, string? Template, string? Name) GetHttpMetadata(MethodInfo method)
    {
        var attribute = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
        return (attribute.GetType().Name, attribute.Template, attribute.Name);
    }

    private static string[] GetAuthMetadata(MethodInfo method)
    {
        return method.GetCustomAttributes<AuthAttribute>()
            .Select(auth => $"{auth.Action}:{auth.Resource}:{auth.IncludeArchived}")
            .OrderBy(description => description)
            .ToArray();
    }

    private static void AssertOkObject<T>(IActionResult? result, T expected)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }
}
