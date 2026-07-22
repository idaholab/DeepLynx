using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for the V2 actions of <see cref="TagProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class TagProjectControllerTestsV2 : IDisposable
{
    private readonly Mock<ITagBusiness> _mockTagBusiness;
    private readonly Mock<ILogger<TagProjectController>> _mockLogger;
    private readonly TagProjectController _tagProjectController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long TagId = 67L;

    public TagProjectControllerTestsV2()
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
    // GetAllTagsV2 Tests
    // =========================================================================

    #region GetAllTagsV2 Tests

    [Fact]
    public async Task GetAllTagsV2_Returns200_WithTags()
    {
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetAllTags(
                OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), true))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.GetAllTagsV2(ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllTagsV2_Returns200_WithEmptyList()
    {
        _mockTagBusiness
            .Setup(b => b.GetAllTags(It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        var result = (await _tagProjectController.GetAllTagsV2(ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<TagResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllTagsV2_ThrowsException_WhenBusinessThrows()
    {
        _mockTagBusiness
            .Setup(b => b.GetAllTags(
                OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.GetAllTagsV2(ProjectId, true));
    }

    [Fact]
    public async Task GetAllTagsV2_RebuildsProjectIdArray_AndPassesOrganizationIdFromContext()
    {
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetAllTags(
                OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), true))
            .ReturnsAsync(expected);

        await _tagProjectController.GetAllTagsV2(ProjectId, true);

        _mockTagBusiness.Verify(
            b => b.GetAllTags(
                OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), true),
            Times.Once);
    }

    [Fact]
    public void GetAllTagsV2_HasHttpGetAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.GetAllTagsV2),
            "projectId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "tag");
    }

    #endregion

    // =========================================================================
    // GetTagsByNameV2 Tests
    // =========================================================================

    #region GetTagsByNameV2 Tests

    [Fact]
    public async Task GetTagsByNameV2_Returns200_WithTags()
    {
        var expected = new List<TagResponseDto>();
        var tagNames = new List<string>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(OrgId, ProjectId, tagNames, true))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.GetTagsByNameV2(
            ProjectId, tagNames, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetTagsByNameV2_Returns200_WithEmptyList()
    {
        var tagNames = new List<string>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(It.IsAny<long>(), It.IsAny<long?>(), tagNames, It.IsAny<bool>()))
            .ReturnsAsync([]);

        var result = (await _tagProjectController.GetTagsByNameV2(
            ProjectId, tagNames, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<TagResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetTagsByNameV2_ThrowsException_WhenBusinessThrows()
    {
        var tagNames = new List<string>();
        _mockTagBusiness
            .Setup(b => b.GetTagsByName(OrgId, ProjectId, tagNames, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.GetTagsByNameV2(
            ProjectId, tagNames, true));
    }

    [Fact]
    public async Task GetTagsByNameV2_PassesProjectIdFromRouteAndOrganizationIdFromContext()
    {
        var tagNames = new List<string>();
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(OrgId, ProjectId, tagNames, true))
            .ReturnsAsync(expected);

        await _tagProjectController.GetTagsByNameV2(ProjectId, tagNames, true);

        _mockTagBusiness.Verify(
            b => b.GetTagsByName(OrgId, ProjectId, tagNames, true),
            Times.Once);
    }

    [Fact]
    public void GetTagsByNameV2_HasHttpPostAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.GetTagsByNameV2),
            "projectId", "tagNames", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "read", "tag");
    }

    #endregion

    // =========================================================================
    // GetTagV2 Tests
    // =========================================================================

    #region GetTagV2 Tests

    [Fact]
    public async Task GetTagV2_Returns200_WithTag()
    {
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.GetTagV2(ProjectId, TagId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetTagV2_Returns200_WithNullTag()
    {
        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ReturnsAsync((TagResponseDto)null!);

        var result = (await _tagProjectController.GetTagV2(ProjectId, TagId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetTagV2_ThrowsException_WhenBusinessThrows()
    {
        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.GetTagV2(ProjectId, TagId, true));
    }

    [Fact]
    public async Task GetTagV2_PassesProjectIdFromRouteAndOrganizationIdFromContext()
    {
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ReturnsAsync(expected);

        await _tagProjectController.GetTagV2(ProjectId, TagId, true);

        _mockTagBusiness.Verify(
            b => b.GetTag(OrgId, ProjectId, TagId, true),
            Times.Once);
    }

    [Fact]
    public void GetTagV2_HasHttpGetAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.GetTagV2),
            "projectId", "tagId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "tag");
    }

    #endregion

    // =========================================================================
    // CreateTagV2 Tests
    // =========================================================================

    #region CreateTagV2 Tests

    [Fact]
    public async Task CreateTagV2_Returns200_WithTag()
    {
        var input = new CreateTagRequestDto();
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, ProjectId, input))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.CreateTagV2(ProjectId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateTagV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateTagRequestDto();
        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, ProjectId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.CreateTagV2(ProjectId, input));
    }

    [Fact]
    public async Task CreateTagV2_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var input = new CreateTagRequestDto();
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, ProjectId, input))
            .ReturnsAsync(expected);

        await _tagProjectController.CreateTagV2(ProjectId, input);

        _mockTagBusiness.Verify(
            b => b.CreateTag(OrgId, UserId, ProjectId, input),
            Times.Once);
    }

    [Fact]
    public void CreateTagV2_HasHttpPostAndWriteTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.CreateTagV2),
            "projectId", "tagRequestDto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "tag");
    }

    #endregion

    // =========================================================================
    // BulkCreateTagV2 Tests
    // =========================================================================

    #region BulkCreateTagV2 Tests

    [Fact]
    public async Task BulkCreateTagV2_Returns200_WithTags()
    {
        var expected = new List<TagResponseDto>();
        var tagRequestDto = new List<CreateTagRequestDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.BulkCreateTagV2(
            ProjectId, tagRequestDto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateTagV2_Returns200_WithEmptyList()
    {
        var tagRequestDto = new List<CreateTagRequestDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(It.IsAny<long>(), UserId, ProjectId, tagRequestDto))
            .ReturnsAsync([]);

        var result = (await _tagProjectController.BulkCreateTagV2(
            ProjectId, tagRequestDto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<TagResponseDto>>(result.Value);
    }

    [Fact]
    public async Task BulkCreateTagV2_ThrowsException_WhenBusinessThrows()
    {
        var tagRequestDto = new List<CreateTagRequestDto>();
        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.BulkCreateTagV2(
            ProjectId, tagRequestDto));
    }

    [Fact]
    public async Task BulkCreateTagV2_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var tagRequestDto = new List<CreateTagRequestDto>();
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto))
            .ReturnsAsync(expected);

        await _tagProjectController.BulkCreateTagV2(ProjectId, tagRequestDto);

        _mockTagBusiness.Verify(
            b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto),
            Times.Once);
    }

    [Fact]
    public void BulkCreateTagV2_HasHttpPostAndWriteTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.BulkCreateTagV2),
            "projectId", "tagRequestDto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "tag");
    }

    #endregion

    // =========================================================================
    // UpdateTagV2 Tests
    // =========================================================================

    #region UpdateTagV2 Tests

    [Fact]
    public async Task UpdateTagV2_Returns200_WithTag()
    {
        var input = new UpdateTagRequestDto();
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input))
            .ReturnsAsync(expected);

        var result = (await _tagProjectController.UpdateTagV2(
            ProjectId, TagId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateTagV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateTagRequestDto();
        _mockTagBusiness
            .Setup(b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.UpdateTagV2(
            ProjectId, TagId, input));
    }

    [Fact]
    public async Task UpdateTagV2_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var input = new UpdateTagRequestDto();
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input))
            .ReturnsAsync(expected);

        await _tagProjectController.UpdateTagV2(ProjectId, TagId, input);

        _mockTagBusiness.Verify(
            b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateTagV2_HasHttpPutAndUpdateTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.UpdateTagV2),
            "projectId", "tagId", "tagRequestDto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
        AssertHasAuthAttribute(method, "update", "tag");
    }

    #endregion

    // =========================================================================
    // DeleteTagV2 Tests
    // =========================================================================

    #region DeleteTagV2 Tests

    [Fact]
    public async Task DeleteTagV2_Returns200_WithBooleanResult()
    {
        _mockTagBusiness
            .Setup(b => b.DeleteTag(OrgId, ProjectId, TagId))
            .ReturnsAsync(true);

        var result = await _tagProjectController.DeleteTagV2(ProjectId, TagId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteTagV2_ThrowsException_WhenBusinessThrows()
    {
        _mockTagBusiness
            .Setup(b => b.DeleteTag(OrgId, ProjectId, TagId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagProjectController.DeleteTagV2(ProjectId, TagId));
    }

    [Fact]
    public async Task DeleteTagV2_PassesOrganizationIdFromContextToBusinessLayer()
    {
        _mockTagBusiness
            .Setup(b => b.DeleteTag(OrgId, ProjectId, TagId))
            .ReturnsAsync(true);

        await _tagProjectController.DeleteTagV2(ProjectId, TagId);

        _mockTagBusiness.Verify(
            b => b.DeleteTag(OrgId, ProjectId, TagId),
            Times.Once);
    }

    [Fact]
    public void DeleteTagV2_HasHttpDeleteAndWriteTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.DeleteTagV2),
            "projectId", "tagId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "write", "tag");
    }

    #endregion

    // =========================================================================
    // ArchiveTagV2 Tests
    // =========================================================================

    #region ArchiveTagV2 Tests

    [Fact]
    public async Task ArchiveTagV2_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockTagBusiness
            .Setup(b => b.ArchiveTag(OrgId, UserId, ProjectId, TagId))
            .ReturnsAsync(true);

        var result = await _tagProjectController.ArchiveTagV2(
            ProjectId, TagId, archive: true) as OkObjectResult;

        Assert.NotNull(result);
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
    public async Task ArchiveTagV2_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(OrgId, UserId, ProjectId, TagId))
            .ReturnsAsync(true);

        var result = await _tagProjectController.ArchiveTagV2(
            ProjectId, TagId, archive: false) as OkObjectResult;

        Assert.NotNull(result);
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
    public async Task ArchiveTagV2_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockTagBusiness
            .Setup(b => b.ArchiveTag(OrgId, UserId, ProjectId, TagId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _tagProjectController.ArchiveTagV2(ProjectId, TagId, archive: true));
    }

    [Fact]
    public async Task ArchiveTagV2_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(OrgId, UserId, ProjectId, TagId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _tagProjectController.ArchiveTagV2(ProjectId, TagId, archive: false));
    }

    [Fact]
    public void ArchiveTagV2_HasHttpPatchAndUpdateTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.ArchiveTagV2),
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