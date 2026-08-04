using deeplynx.api.Controllers.V2;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for the V2 actions of <see cref="TagProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class TagProjectControllerTests : IDisposable
{
    private readonly Mock<ITagBusiness> _mockTagBusiness;
    private readonly Mock<ILogger<TagProjectController>> _mockLogger;
    private readonly TagProjectController _tagProjectController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long TagId = 67L;

    public TagProjectControllerTests()
    {
        _mockTagBusiness = new Mock<ITagBusiness>();
        _mockLogger = new Mock<ILogger<TagProjectController>>();

        _tagProjectController = new TagProjectController(
            _mockTagBusiness.Object,
            _mockLogger.Object);

        UserContextStorage.UserId = UserId;
        UserContextStorage.OrganizationId = OrgId;
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
    // GetAllTags Tests
    // =========================================================================

    #region GetAllTags Tests

    [Fact]
    public async Task GetAllTags_Returns200_WithTags()
    {
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetAllTags(
                UserId, OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), true, false, false))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.GetAllTags(ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllTags_Returns200_WithEmptyList()
    {
        _mockTagBusiness
            .Setup(b => b.GetAllTags(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        var result = (await _tagProjectController.GetAllTags(ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<TagResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllTags_ThrowsException_WhenBusinessThrows()
    {
        _mockTagBusiness
            .Setup(b => b.GetAllTags(
                UserId, OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), true, false, false))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.GetAllTags(ProjectId, true));
    }

    [Fact]
    public async Task GetAllTags_RebuildsProjectIdArray_AndPassesOrganizationIdFromContext()
    {
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetAllTags(
                UserId, OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), true, false, false))
            .ReturnsAsync(expected);

        await _tagProjectController.GetAllTags(ProjectId, true);

        _mockTagBusiness.Verify(
            b => b.GetAllTags(
                UserId, OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), true, false, false),
            Times.Once);
    }

    [Fact]
    public void GetAllTags_HasHttpGetAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.GetAllTags),
            "projectId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "tag");
    }

    #endregion

    // =========================================================================
    // GetTagsByName Tests
    // =========================================================================

    #region GetTagsByName Tests

    [Fact]
    public async Task GetTagsByName_Returns200_WithTags()
    {
        var expected = new List<TagResponseDto>();
        var tagNames = new List<string>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(OrgId, ProjectId, tagNames, true))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.GetTagsByName(
            ProjectId, tagNames, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetTagsByName_Returns200_WithEmptyList()
    {
        var tagNames = new List<string>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(It.IsAny<long>(), It.IsAny<long?>(), tagNames, It.IsAny<bool>()))
            .ReturnsAsync([]);

        var result = (await _tagProjectController.GetTagsByName(
            ProjectId, tagNames, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<TagResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetTagsByName_ThrowsException_WhenBusinessThrows()
    {
        var tagNames = new List<string>();
        _mockTagBusiness
            .Setup(b => b.GetTagsByName(OrgId, ProjectId, tagNames, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.GetTagsByName(
            ProjectId, tagNames, true));
    }

    [Fact]
    public async Task GetTagsByName_PassesProjectIdFromRouteAndOrganizationIdFromContext()
    {
        var tagNames = new List<string>();
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(OrgId, ProjectId, tagNames, true))
            .ReturnsAsync(expected);

        await _tagProjectController.GetTagsByName(ProjectId, tagNames, true);

        _mockTagBusiness.Verify(
            b => b.GetTagsByName(OrgId, ProjectId, tagNames, true),
            Times.Once);
    }

    [Fact]
    public void GetTagsByName_HasHttpPostAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.GetTagsByName),
            "projectId", "tagNames", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "read", "tag");
    }

    #endregion

    // =========================================================================
    // GetTag Tests
    // =========================================================================

    #region GetTag Tests

    [Fact]
    public async Task GetTag_Returns200_WithTag()
    {
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.GetTag(ProjectId, TagId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetTag_Returns200_WithNullTag()
    {
        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ReturnsAsync((TagResponseDto)null!);

        var result = (await _tagProjectController.GetTag(ProjectId, TagId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetTag_ThrowsException_WhenBusinessThrows()
    {
        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.GetTag(ProjectId, TagId, true));
    }

    [Fact]
    public async Task GetTag_PassesProjectIdFromRouteAndOrganizationIdFromContext()
    {
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ReturnsAsync(expected);

        await _tagProjectController.GetTag(ProjectId, TagId, true);

        _mockTagBusiness.Verify(
            b => b.GetTag(OrgId, ProjectId, TagId, true),
            Times.Once);
    }

    [Fact]
    public void GetTag_HasHttpGetAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.GetTag),
            "projectId", "tagId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "tag");
    }

    #endregion

    // =========================================================================
    // CreateTag Tests
    // =========================================================================

    #region CreateTag Tests

    [Fact]
    public async Task CreateTag_Returns200_WithTag()
    {
        var input = new CreateTagRequestDto();
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, ProjectId, input))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.CreateTag(ProjectId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateTag_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateTagRequestDto();
        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, ProjectId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.CreateTag(ProjectId, input));
    }

    [Fact]
    public async Task CreateTag_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var input = new CreateTagRequestDto();
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, ProjectId, input))
            .ReturnsAsync(expected);

        await _tagProjectController.CreateTag(ProjectId, input);

        _mockTagBusiness.Verify(
            b => b.CreateTag(OrgId, UserId, ProjectId, input),
            Times.Once);
    }

    [Fact]
    public void CreateTag_HasHttpPostAndWriteTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.CreateTag),
            "projectId", "tagRequestDto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "tag");
    }

    #endregion

    // =========================================================================
    // BulkCreateTag Tests
    // =========================================================================

    #region BulkCreateTag Tests

    [Fact]
    public async Task BulkCreateTag_Returns200_WithTags()
    {
        var expected = new List<TagResponseDto>();
        var tagRequestDto = new List<CreateTagRequestDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.BulkCreateTag(
            ProjectId, tagRequestDto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateTag_Returns200_WithEmptyList()
    {
        var tagRequestDto = new List<CreateTagRequestDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(It.IsAny<long>(), UserId, ProjectId, tagRequestDto))
            .ReturnsAsync([]);

        var result = (await _tagProjectController.BulkCreateTag(
            ProjectId, tagRequestDto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<TagResponseDto>>(result.Value);
    }

    [Fact]
    public async Task BulkCreateTag_ThrowsException_WhenBusinessThrows()
    {
        var tagRequestDto = new List<CreateTagRequestDto>();
        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.BulkCreateTag(
            ProjectId, tagRequestDto));
    }

    [Fact]
    public async Task BulkCreateTag_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var tagRequestDto = new List<CreateTagRequestDto>();
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto))
            .ReturnsAsync(expected);

        await _tagProjectController.BulkCreateTag(ProjectId, tagRequestDto);

        _mockTagBusiness.Verify(
            b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto),
            Times.Once);
    }

    [Fact]
    public void BulkCreateTag_HasHttpPostAndWriteTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.BulkCreateTag),
            "projectId", "tagRequestDto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "tag");
    }

    #endregion

    // =========================================================================
    // UpdateTag Tests
    // =========================================================================

    #region UpdateTag Tests

    [Fact]
    public async Task UpdateTag_Returns200_WithTag()
    {
        var input = new UpdateTagRequestDto();
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.UpdateTag(
            ProjectId, TagId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateTag_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateTagRequestDto();
        _mockTagBusiness
            .Setup(b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.UpdateTag(
            ProjectId, TagId, input));
    }

    [Fact]
    public async Task UpdateTag_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var input = new UpdateTagRequestDto();
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input))
            .ReturnsAsync(expected);

        await _tagProjectController.UpdateTag(ProjectId, TagId, input);

        _mockTagBusiness.Verify(
            b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateTag_HasHttpPutAndUpdateTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.UpdateTag),
            "projectId", "tagId", "tagRequestDto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
        AssertHasAuthAttribute(method, "update", "tag");
    }

    #endregion

    // =========================================================================
    // DeleteTag Tests
    // =========================================================================

    #region DeleteTag Tests

    [Fact]
    public async Task DeleteTag_Returns200_WithBooleanResult()
    {
        _mockTagBusiness
            .Setup(b => b.DeleteTag(OrgId, ProjectId, TagId))
            .ReturnsAsync(true);

        var actionResult = await _tagProjectController.DeleteTag(ProjectId, TagId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteTag_ThrowsException_WhenBusinessThrows()
    {
        _mockTagBusiness
            .Setup(b => b.DeleteTag(OrgId, ProjectId, TagId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.DeleteTag(ProjectId, TagId));
    }

    [Fact]
    public async Task DeleteTag_PassesOrganizationIdFromContextToBusinessLayer()
    {
        _mockTagBusiness
            .Setup(b => b.DeleteTag(OrgId, ProjectId, TagId))
            .ReturnsAsync(true);

        await _tagProjectController.DeleteTag(ProjectId, TagId);

        _mockTagBusiness.Verify(
            b => b.DeleteTag(OrgId, ProjectId, TagId),
            Times.Once);
    }

    [Fact]
    public void DeleteTag_HasHttpDeleteAndWriteTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.DeleteTag),
            "projectId", "tagId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "write", "tag");
    }

    #endregion

    // =========================================================================
    // ArchiveTag Tests
    // =========================================================================

    #region ArchiveTag Tests

    [Fact]
    public async Task ArchiveTag_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockTagBusiness
            .Setup(b => b.ArchiveTag(OrgId, UserId, ProjectId, TagId))
            .ReturnsAsync(true);

        var actionResult = await _tagProjectController.ArchiveTag(
            ProjectId, TagId, archive: true);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockTagBusiness.Verify(
            b => b.ArchiveTag(OrgId, UserId, ProjectId, TagId),
            Times.Once);
        _mockTagBusiness.Verify(
            b => b.UnarchiveTag(OrgId, UserId, ProjectId, TagId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveTag_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(OrgId, UserId, ProjectId, TagId))
            .ReturnsAsync(true);

        var actionResult = await _tagProjectController.ArchiveTag(
            ProjectId, TagId, archive: false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockTagBusiness.Verify(
            b => b.UnarchiveTag(OrgId, UserId, ProjectId, TagId),
            Times.Once);
        _mockTagBusiness.Verify(
            b => b.ArchiveTag(OrgId, UserId, ProjectId, TagId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveTag_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockTagBusiness
            .Setup(b => b.ArchiveTag(OrgId, UserId, ProjectId, TagId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _tagProjectController.ArchiveTag(ProjectId, TagId, archive: true));
    }

    [Fact]
    public async Task ArchiveTag_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(OrgId, UserId, ProjectId, TagId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _tagProjectController.ArchiveTag(ProjectId, TagId, archive: false));
    }

    [Fact]
    public void ArchiveTag_HasHttpPatchAndUpdateTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.ArchiveTag),
            "projectId", "tagId", "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
        AssertHasAuthAttribute(method, "update", "tag");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void TagProjectController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(TagProjectController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
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
        return Assert.Single(typeof(TagProjectController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}