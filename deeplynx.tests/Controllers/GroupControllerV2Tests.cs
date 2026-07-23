using System.Reflection;
using Asp.Versioning;
using deeplynx.api.Controllers;
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

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class GroupControllerV2Tests : IDisposable
{
    private const long CurrentUserId = 10L;
    private const long OrganizationId = 20L;
    private const long GroupId = 30L;
    private const long MemberId = 40L;

    private readonly Mock<IGroupBusiness> _mockGroupBusiness = new();
    private readonly GroupController _controller;

    public GroupControllerV2Tests()
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
    public async Task GetAllGroupsV2_ReturnsGroupsAndForwardsFilters()
    {
        var expected = new List<GroupResponseDto> { new() { Id = GroupId, Name = "Group" } };
        _mockGroupBusiness
            .Setup(business => business.GetAllGroups(OrganizationId, false))
            .ReturnsAsync(expected);

        var result = (await _controller.GetAllGroupsV2(OrganizationId, hideArchived: false)).Result;

        AssertOkObject(result, expected);
        _mockGroupBusiness.Verify(
            business => business.GetAllGroups(OrganizationId, false),
            Times.Once);
    }

    [Fact]
    public async Task GetGroupV2_ReturnsGroupAndForwardsFilters()
    {
        var expected = new GroupResponseDto { Id = GroupId, Name = "Group" };
        _mockGroupBusiness
            .Setup(business => business.GetGroup(OrganizationId, GroupId, false))
            .ReturnsAsync(expected);

        var result = (await _controller.GetGroupV2(
            OrganizationId,
            GroupId,
            hideArchived: false)).Result;

        AssertOkObject(result, expected);
        _mockGroupBusiness.Verify(
            business => business.GetGroup(OrganizationId, GroupId, false),
            Times.Once);
    }

    [Fact]
    public async Task GetGroupMembersV2_ReturnsMembers()
    {
        var expected = new List<UserResponseDto> { new() { Id = MemberId } };
        _mockGroupBusiness
            .Setup(business => business.GetGroupMembers(OrganizationId, GroupId))
            .ReturnsAsync(expected);

        var result = (await _controller.GetGroupMembersV2(OrganizationId, GroupId)).Result;

        AssertOkObject(result, expected);
    }

    [Fact]
    public async Task CreateGroupV2_ReturnsGroupAndUsesCurrentUser()
    {
        var request = new CreateGroupRequestDto { Name = "Group" };
        var expected = new GroupResponseDto { Id = GroupId, Name = request.Name };
        _mockGroupBusiness
            .Setup(business => business.CreateGroup(CurrentUserId, OrganizationId, request))
            .ReturnsAsync(expected);

        var result = (await _controller.CreateGroupV2(OrganizationId, request)).Result;

        AssertOkObject(result, expected);
        _mockGroupBusiness.Verify(
            business => business.CreateGroup(CurrentUserId, OrganizationId, request),
            Times.Once);
    }

    [Fact]
    public async Task UpdateGroupV2_ReturnsGroupAndUsesCurrentUser()
    {
        var request = new UpdateGroupRequestDto { Name = "Updated Group" };
        var expected = new GroupResponseDto { Id = GroupId, Name = request.Name };
        _mockGroupBusiness
            .Setup(business => business.UpdateGroup(CurrentUserId, OrganizationId, GroupId, request))
            .ReturnsAsync(expected);

        var result = (await _controller.UpdateGroupV2(OrganizationId, GroupId, request)).Result;

        AssertOkObject(result, expected);
        _mockGroupBusiness.Verify(
            business => business.UpdateGroup(CurrentUserId, OrganizationId, GroupId, request),
            Times.Once);
    }

    [Fact]
    public async Task DeleteGroupV2_ReturnsBooleanAndUsesCurrentUser()
    {
        _mockGroupBusiness
            .Setup(business => business.DeleteGroup(CurrentUserId, OrganizationId, GroupId))
            .ReturnsAsync(true);

        var result = await _controller.DeleteGroupV2(OrganizationId, GroupId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<bool>(ok.Value));
        _mockGroupBusiness.Verify(
            business => business.DeleteGroup(CurrentUserId, OrganizationId, GroupId),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveGroupV2_WhenArchiveIsTrue_ReturnsBooleanAndArchivesGroup()
    {
        _mockGroupBusiness
            .Setup(business => business.ArchiveGroup(CurrentUserId, OrganizationId, GroupId))
            .ReturnsAsync(true);

        var result = await _controller.ArchiveGroupV2(OrganizationId, GroupId, archive: true);

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
    public async Task ArchiveGroupV2_WhenArchiveIsFalse_ReturnsBooleanAndUnarchivesGroup()
    {
        _mockGroupBusiness
            .Setup(business => business.UnarchiveGroup(CurrentUserId, OrganizationId, GroupId))
            .ReturnsAsync(true);

        var result = await _controller.ArchiveGroupV2(OrganizationId, GroupId, archive: false);

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
    public async Task AddUserToGroupV2_ReturnsBooleanAndForwardsArguments()
    {
        _mockGroupBusiness
            .Setup(business => business.AddUserToGroup(MemberId, OrganizationId, GroupId))
            .ReturnsAsync(true);

        var result = await _controller.AddUserToGroupV2(OrganizationId, GroupId, MemberId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<bool>(ok.Value));
        _mockGroupBusiness.Verify(
            business => business.AddUserToGroup(MemberId, OrganizationId, GroupId),
            Times.Once);
    }

    [Fact]
    public async Task RemoveUserFromGroupV2_ReturnsBooleanAndForwardsArguments()
    {
        _mockGroupBusiness
            .Setup(business => business.RemoveUserFromGroup(MemberId, OrganizationId, GroupId))
            .ReturnsAsync(true);

        var result = await _controller.RemoveUserFromGroupV2(OrganizationId, GroupId, MemberId);

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

        var result = (await _controller.GetGroup(OrganizationId, GroupId)).Result;

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
            () => _controller.GetGroupV2(OrganizationId, GroupId));

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
            () => _controller.DeleteGroupV2(OrganizationId, GroupId));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Controller_DeclaresExpectedRouteVersionsAndSecurity()
    {
        var controller = typeof(GroupController);
        var versions = controller.GetCustomAttributes<ApiVersionAttribute>().ToList();
        var route = Assert.IsType<RouteAttribute>(controller.GetCustomAttribute<RouteAttribute>());

        Assert.Equal("organizations/{organizationId:long}/groups", route.Template);
        Assert.Equal(2, versions.Count);
        Assert.All(versions, version => Assert.False(version.Deprecated));
        Assert.Contains(versions, version => version.Versions.Any(apiVersion => apiVersion.MajorVersion == 1));
        Assert.Contains(versions, version => version.Versions.Any(apiVersion => apiVersion.MajorVersion == 2));
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<ForbidServiceAccountsAttribute>());
    }

    public static TheoryData<string, string> VersionedActionPairs => new()
    {
        { nameof(GroupController.GetAllGroups), nameof(GroupController.GetAllGroupsV2) },
        { nameof(GroupController.GetGroup), nameof(GroupController.GetGroupV2) },
        { nameof(GroupController.GetGroupMembers), nameof(GroupController.GetGroupMembersV2) },
        { nameof(GroupController.CreateGroup), nameof(GroupController.CreateGroupV2) },
        { nameof(GroupController.UpdateGroup), nameof(GroupController.UpdateGroupV2) },
        { nameof(GroupController.DeleteGroup), nameof(GroupController.DeleteGroupV2) },
        { nameof(GroupController.ArchiveGroup), nameof(GroupController.ArchiveGroupV2) },
        { nameof(GroupController.AddUserToGroup), nameof(GroupController.AddUserToGroupV2) },
        { nameof(GroupController.RemoveUserFromGroup), nameof(GroupController.RemoveUserFromGroupV2) }
    };

    [Theory]
    [MemberData(nameof(VersionedActionPairs))]
    public void VersionedActions_PreserveRouteAndAuthorizationMetadata(string v1Name, string v2Name)
    {
        var v1 = GetAction(v1Name);
        var v2 = GetAction(v2Name);

        AssertMappedVersion(v1, 1D);
        AssertMappedVersion(v2, 2D);
        Assert.NotNull(v2.GetCustomAttribute<BadgeAttribute>());
        Assert.Equal(GetHttpMetadata(v1), GetHttpMetadata(v2));
        Assert.Equal(GetAuthMetadata(v1), GetAuthMetadata(v2));
    }

    private static MethodInfo GetAction(string name)
    {
        return Assert.Single(typeof(GroupController).GetMethods(), method => method.Name == name);
    }

    private static void AssertMappedVersion(MethodInfo method, double expectedVersion)
    {
        var attribute = Assert.Single(method.GetCustomAttributesData(), metadata =>
            metadata.AttributeType == typeof(MapToApiVersionAttribute));
        Assert.Equal(expectedVersion, Assert.IsType<double>(attribute.ConstructorArguments[0].Value));
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
