using deeplynx.api.Controllers;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for the V2 actions of <see cref="TagOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class TagOrganizationControllerTestsV2 : IDisposable
{
    private readonly Mock<ITagBusiness> _mockTagBusiness;
    private readonly Mock<ILogger<TagProjectController>> _mockLogger;
    private readonly TagOrganizationController _tagOrganizationController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long TagId = 67L;

    public TagOrganizationControllerTestsV2()
    {
        _mockTagBusiness = new Mock<ITagBusiness>();
        _mockLogger = new Mock<ILogger<TagProjectController>>();

        _tagOrganizationController = new TagOrganizationController(
            _mockTagBusiness.Object,
            _mockLogger.Object);

        UserContextStorage.UserId = UserId;
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
            .Setup(b => b.GetAllTags(UserId, OrgId, null, true, false, false))
            .ReturnsAsync(expected);

        var result = (await _tagOrganizationController.GetAllTagsV2(OrgId, null, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllTagsV2_Returns200_WithEmptyList()
    {
        _mockTagBusiness
            .Setup(b => b.GetAllTags(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        var result = (await _tagOrganizationController.GetAllTagsV2(OrgId, null, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<TagResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllTagsV2_ThrowsException_WhenBusinessThrows()
    {
        _mockTagBusiness
            .Setup(b => b.GetAllTags(UserId, OrgId, null, true, false, false))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagOrganizationController.GetAllTagsV2(OrgId, null, true));
    }

    [Fact]
    public async Task GetAllTagsV2_PassesOrganizationIdAndHideArchivedToBusinessLayer()
    {
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetAllTags(UserId, OrgId, null, true, false, false))
            .ReturnsAsync(expected);

        await _tagOrganizationController.GetAllTagsV2(OrgId, null, true);

        _mockTagBusiness.Verify(
            b => b.GetAllTags(UserId, OrgId, null, true, false, false),
            Times.Once);
    }

    [Fact]
    public void GetAllTagsV2_HasHttpGetAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.GetAllTagsV2),
            "organizationId", "hideArchived");

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
            .Setup(b => b.GetTagsByName(OrgId, null, tagNames, true))
            .ReturnsAsync(expected);

        var result = (await _tagOrganizationController.GetTagsByNameV2(
            OrgId, tagNames, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetTagsByNameV2_Returns200_WithEmptyList()
    {
        var tagNames = new List<string>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(It.IsAny<long>(), null, tagNames, It.IsAny<bool>()))
            .ReturnsAsync([]);

        var result = (await _tagOrganizationController.GetTagsByNameV2(
            OrgId, tagNames, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<TagResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetTagsByNameV2_ThrowsException_WhenBusinessThrows()
    {
        var tagNames = new List<string>();
        _mockTagBusiness
            .Setup(b => b.GetTagsByName(OrgId, null, tagNames, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagOrganizationController.GetTagsByNameV2(
            OrgId, tagNames, true));
    }

    [Fact]
    public async Task GetTagsByNameV2_PassesNullProjectIdAndTagNamesToBusinessLayer()
    {
        var tagNames = new List<string>();
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(OrgId, null, tagNames, true))
            .ReturnsAsync(expected);

        await _tagOrganizationController.GetTagsByNameV2(OrgId, tagNames, true);

        _mockTagBusiness.Verify(
            b => b.GetTagsByName(OrgId, null, tagNames, true),
            Times.Once);
    }

    [Fact]
    public void GetTagsByNameV2_HasHttpPostAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.GetTagsByNameV2),
            "organizationId", "tagNames", "hideArchived");

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
            .Setup(b => b.GetTag(OrgId, null, TagId, true))
            .ReturnsAsync(expected);

        var result = (await _tagOrganizationController.GetTagV2(OrgId, TagId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetTagV2_Returns200_WithNullTag()
    {
        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, null, TagId, true))
            .ReturnsAsync((TagResponseDto)null!);

        var result = (await _tagOrganizationController.GetTagV2(OrgId, TagId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetTagV2_ThrowsException_WhenBusinessThrows()
    {
        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, null, TagId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagOrganizationController.GetTagV2(OrgId, TagId, true));
    }

    [Fact]
    public async Task GetTagV2_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, null, TagId, true))
            .ReturnsAsync(expected);

        await _tagOrganizationController.GetTagV2(OrgId, TagId, true);

        _mockTagBusiness.Verify(
            b => b.GetTag(OrgId, null, TagId, true),
            Times.Once);
    }

    [Fact]
    public void GetTagV2_HasHttpGetAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.GetTagV2),
            "organizationId", "tagId", "hideArchived");

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
            .Setup(b => b.CreateTag(OrgId, UserId, null, input))
            .ReturnsAsync(expected);

        var result = (await _tagOrganizationController.CreateTagV2(OrgId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateTagV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateTagRequestDto();
        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, null, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagOrganizationController.CreateTagV2(OrgId, input));
    }

    [Fact]
    public async Task CreateTagV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new CreateTagRequestDto();
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, null, input))
            .ReturnsAsync(expected);

        await _tagOrganizationController.CreateTagV2(OrgId, input);

        _mockTagBusiness.Verify(
            b => b.CreateTag(OrgId, UserId, null, input),
            Times.Once);
    }

    [Fact]
    public void CreateTagV2_HasHttpPostAndWriteTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.CreateTagV2),
            "organizationId", "tagRequestDto");

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
            .Setup(b => b.BulkCreateTags(OrgId, UserId, null, tagRequestDto))
            .ReturnsAsync(expected);

        var result = (await _tagOrganizationController.BulkCreateTagV2(
            OrgId, tagRequestDto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateTagV2_Returns200_WithEmptyList()
    {
        var tagRequestDto = new List<CreateTagRequestDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(It.IsAny<long>(), UserId, null, tagRequestDto))
            .ReturnsAsync([]);

        var result = (await _tagOrganizationController.BulkCreateTagV2(
            OrgId, tagRequestDto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<TagResponseDto>>(result.Value);
    }

    [Fact]
    public async Task BulkCreateTagV2_ThrowsException_WhenBusinessThrows()
    {
        var tagRequestDto = new List<CreateTagRequestDto>();
        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(OrgId, UserId, null, tagRequestDto))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagOrganizationController.BulkCreateTagV2(
            OrgId, tagRequestDto));
    }

    [Fact]
    public async Task BulkCreateTagV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var tagRequestDto = new List<CreateTagRequestDto>();
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(OrgId, UserId, null, tagRequestDto))
            .ReturnsAsync(expected);

        await _tagOrganizationController.BulkCreateTagV2(OrgId, tagRequestDto);

        _mockTagBusiness.Verify(
            b => b.BulkCreateTags(OrgId, UserId, null, tagRequestDto),
            Times.Once);
    }

    [Fact]
    public void BulkCreateTagV2_HasHttpPostAndWriteTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.BulkCreateTagV2),
            "organizationId", "tagRequestDto");

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
            .Setup(b => b.UpdateTag(OrgId, UserId, null, TagId, input))
            .ReturnsAsync(expected);

        var result = (await _tagOrganizationController.UpdateTagV2(
            OrgId, TagId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateTagV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateTagRequestDto();
        _mockTagBusiness
            .Setup(b => b.UpdateTag(OrgId, UserId, null, TagId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagOrganizationController.UpdateTagV2(
            OrgId, TagId, input));
    }

    [Fact]
    public async Task UpdateTagV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new UpdateTagRequestDto();
        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.UpdateTag(OrgId, UserId, null, TagId, input))
            .ReturnsAsync(expected);

        await _tagOrganizationController.UpdateTagV2(OrgId, TagId, input);

        _mockTagBusiness.Verify(
            b => b.UpdateTag(OrgId, UserId, null, TagId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateTagV2_HasHttpPutAndUpdateTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.UpdateTagV2),
            "organizationId", "tagId", "tagRequestDto");

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
            .Setup(b => b.DeleteTag(OrgId, null, TagId))
            .ReturnsAsync(true);

        var actionResult = await _tagOrganizationController.DeleteTagV2(OrgId, TagId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteTagV2_ThrowsException_WhenBusinessThrows()
    {
        _mockTagBusiness
            .Setup(b => b.DeleteTag(OrgId, null, TagId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _tagOrganizationController.DeleteTagV2(OrgId, TagId));
    }

    [Fact]
    public async Task DeleteTagV2_PassesNullProjectIdToBusinessLayer()
    {
        _mockTagBusiness
            .Setup(b => b.DeleteTag(OrgId, null, TagId))
            .ReturnsAsync(true);

        await _tagOrganizationController.DeleteTagV2(OrgId, TagId);

        _mockTagBusiness.Verify(
            b => b.DeleteTag(OrgId, null, TagId),
            Times.Once);
    }

    [Fact]
    public void DeleteTagV2_HasHttpDeleteAndWriteTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.DeleteTagV2),
            "organizationId", "tagId");

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
            .Setup(b => b.ArchiveTag(OrgId, UserId, null, TagId))
            .ReturnsAsync(true);

        var actionResult = await _tagOrganizationController.ArchiveTagV2(
            OrgId, TagId, archive: true);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockTagBusiness.Verify(
            b => b.ArchiveTag(OrgId, UserId, null, TagId),
            Times.Once);
        _mockTagBusiness.Verify(
            b => b.UnarchiveTag(OrgId, UserId, null, TagId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveTagV2_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(OrgId, UserId, null, TagId))
            .ReturnsAsync(true);

        var actionResult = await _tagOrganizationController.ArchiveTagV2(
            OrgId, TagId, archive: false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockTagBusiness.Verify(
            b => b.UnarchiveTag(OrgId, UserId, null, TagId),
            Times.Once);
        _mockTagBusiness.Verify(
            b => b.ArchiveTag(OrgId, UserId, null, TagId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveTagV2_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockTagBusiness
            .Setup(b => b.ArchiveTag(OrgId, UserId, null, TagId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _tagOrganizationController.ArchiveTagV2(OrgId, TagId, archive: true));
    }

    [Fact]
    public async Task ArchiveTagV2_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(OrgId, UserId, null, TagId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _tagOrganizationController.ArchiveTagV2(OrgId, TagId, archive: false));
    }

    [Fact]
    public void ArchiveTagV2_HasHttpPatchAndUpdateTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.ArchiveTagV2),
            "organizationId", "tagId", "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
        AssertHasAuthAttribute(method, "update", "tag");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void TagOrganizationController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(TagOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void TagOrganizationController_HasForbidServiceAccountsAttribute()
    {
        Assert.Contains(typeof(TagOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "ForbidServiceAccountsAttribute");
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
        return Assert.Single(typeof(TagOrganizationController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}