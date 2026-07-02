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
public class TagProjectControllerTests : IDisposable
{
    private readonly Mock<ITagBusiness> _mockTagBusiness;
    private readonly Mock<ILogger<TagProjectController>> _mockLogger;
    private readonly TagProjectController _tagProjectController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long PermissionId = 15L;
    private static readonly long[] ProjectList = { 13L, 14L };
    private static readonly long[] PermissionList = { 13L, 14L };
    private const long RelationshipId = 22L;
    private const long TagId = 67L;


    public TagProjectControllerTests()
    {
        _mockTagBusiness = new Mock<ITagBusiness>();
        _mockLogger = new Mock<ILogger<TagProjectController>>();

        _tagProjectController = new TagProjectController(
            _mockTagBusiness.Object,
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
    // GetAllTags Tests
    // =========================================================================

    #region GetAllTags Tests

    [Fact]
    public async Task GetAllTags_Returns200_WithTags()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        List<TagResponseDto> expected =
            new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetAllTags(
                OrgId,
                It.Is<long[]?>(ids =>
                    ids != null &&
                    ids.Length == 1 &&
                    ids[0] == ProjectId),
               true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tagProjectController.GetAllTags(ProjectId, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllTags_Returns200_WithEmptyList()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        _mockTagBusiness
            .Setup(b => b.GetAllTags(
                OrgId,
                It.Is<long[]?>(ids =>
                    ids != null &&
                    ids.Length == 1 &&
                    ids[0] == ProjectId),
               true))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _tagProjectController.GetAllTags(ProjectId, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetAllTags_Returns500_OnUnexpectedException()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        UserContextStorage.UserId = UserId;

        _mockTagBusiness
            .Setup(b => b.GetAllTags(
                OrgId,
                It.Is<long[]?>(ids =>
                    ids != null &&
                    ids.Length == 1 &&
                    ids[0] == ProjectId),
               true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _tagProjectController.GetAllTags(
            ProjectId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetAllTags_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetAllTags(
                OrgId,
                It.Is<long[]?>(ids =>
                    ids != null &&
                    ids.Length == 1 &&
                    ids[0] == ProjectId),
               true))
            .ReturnsAsync(expected);

        // Act
        await _tagProjectController.GetAllTags(ProjectId, true);

        // Assert
        _mockTagBusiness.Verify(
            b => b.GetAllTags(
                OrgId,
                It.Is<long[]?>(ids =>
                    ids != null &&
                    ids.Length == 1 &&
                    ids[0] == ProjectId),
               true),
            Times.Once);
    }

    [Fact]
    public void GetAllTags_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.GetAllTags),
            "projectId",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetTagsByName Tests
    // =========================================================================

    #region GetTagsByName Tests

    [Fact]
    public async Task GetTagsByName_Returns200_WithTags()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        List<TagResponseDto> expected =
            new List<TagResponseDto>();

        List<string> tagNames =
            new List<string>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(OrgId, ProjectId, tagNames, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tagProjectController.GetTagsByName(
            ProjectId,
            tagNames,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetTagsByName_Returns200_WithEmptyList()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        List<string> tagNames =
            new List<string>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(It.IsAny<long>(), It.IsAny<long>(), tagNames, It.IsAny<bool>()))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _tagProjectController.GetTagsByName(
            It.IsAny<long>(),
            tagNames,
            It.IsAny<bool>());

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetTagsByName_Returns500_OnUnexpectedException()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        UserContextStorage.UserId = UserId;

        List<string> tagNames =
            new List<string>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(OrgId, ProjectId, tagNames, true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _tagProjectController.GetTagsByName(
            ProjectId,
            tagNames,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetTagsByName_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        List<string> tagNames =
            new List<string>();

        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetTagsByName(OrgId, ProjectId, tagNames, true))
            .ReturnsAsync(expected);

        // Act
        await _tagProjectController.GetTagsByName(
            ProjectId,
            tagNames,
            true);

        // Assert
        _mockTagBusiness.Verify(
            b => b.GetTagsByName(OrgId, ProjectId, tagNames, true),
            Times.Once);
    }

    [Fact]
    public void GetTagsByName_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.GetTagsByName),
            "projectId",
            "tagNames",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // GetTag Tests
    // =========================================================================

    #region GetTag Tests

    [Fact]
    public async Task GetTag_Returns200_WithTag()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        TagResponseDto expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tagProjectController.GetTag(
            ProjectId,
            TagId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetTag_Returns200_WithEmptyTag()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ReturnsAsync((TagResponseDto)null!);

        // Act
        var actionResult = await _tagProjectController.GetTag(
            ProjectId,
            TagId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetTag_Returns500_OnUnexpectedException()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _tagProjectController.GetTag(
            ProjectId,
            TagId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetTag_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.OrganizationId = OrgId;

        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, ProjectId, TagId, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tagProjectController.GetTag(
            ProjectId,
            TagId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockTagBusiness.Verify(
            b => b.GetTag(
                OrgId,
                ProjectId,
                TagId,
                true),
            Times.Once);
    }

    [Fact]
    public void GetTag_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.GetTag),
            "projectId",
            "tagId",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // CreateTag Tests
    // =========================================================================

    #region CreateTag Tests

    [Fact]
    public async Task CreateTag_Returns200_WithTag()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        TagResponseDto expected =
            new TagResponseDto();
        CreateTagRequestDto input = new CreateTagRequestDto();

        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, ProjectId, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tagProjectController.CreateTag(
            ProjectId,
            input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task CreateTag_Returns500_OnUnexpectedException()
    {
        CreateTagRequestDto input = new CreateTagRequestDto();
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, ProjectId, input))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _tagProjectController.CreateTag(
            ProjectId,
            input);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateTag_PassesToBusinessLayer()
    {
        // Arrange
        CreateTagRequestDto input = new CreateTagRequestDto();
        var expected = new TagResponseDto();

        UserContextStorage.OrganizationId = OrgId;
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, ProjectId, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tagProjectController.CreateTag(
            ProjectId,
            input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockTagBusiness.Verify(
            b => b.CreateTag(OrgId, UserId, ProjectId, input),
            Times.Once);
    }

    [Fact]
    public void CreateTag_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.CreateTag),
            "projectId",
            "tagRequestDto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // BulkCreateTag Tests
    // =========================================================================

    #region BulkCreateTag Tests

    [Fact]
    public async Task BulkCreateTag_Returns200_WithTags()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        UserContextStorage.UserId = UserId;

        List<TagResponseDto> expected =
            new List<TagResponseDto>();

        List<CreateTagRequestDto> tagRequestDto =
            new List<CreateTagRequestDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tagProjectController.BulkCreateTag(
            ProjectId,
            tagRequestDto);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateTag_Returns200_WithEmptyList()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.OrganizationId = OrgId;

        List<CreateTagRequestDto> tagRequestDto =
            new List<CreateTagRequestDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(It.IsAny<long>(), UserId, ProjectId, tagRequestDto))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _tagProjectController.BulkCreateTag(
            It.IsAny<long>(),
            tagRequestDto);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task BulkCreateTag_Returns500_OnUnexpectedException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.OrganizationId = OrgId;

        List<CreateTagRequestDto> tagRequestDto =
            new List<CreateTagRequestDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _tagProjectController.BulkCreateTag(
            ProjectId,
            tagRequestDto);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task BulkCreateTag_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.OrganizationId = OrgId;

        List<CreateTagRequestDto> tagRequestDto =
            new List<CreateTagRequestDto>();

        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto))
            .ReturnsAsync(expected);

        // Act
        await _tagProjectController.BulkCreateTag(
            ProjectId,
            tagRequestDto);

        // Assert
        _mockTagBusiness.Verify(
            b => b.BulkCreateTags(OrgId, UserId, ProjectId, tagRequestDto),
            Times.Once);
    }

    [Fact]
    public void BulkCreateTag_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.BulkCreateTag),
            "projectId",
            "tagRequestDto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // UpdateTag Tests
    // =========================================================================

    #region UpdateTag Tests

    [Fact]
    public async Task UpdateTag_Returns200_WithTag()
    {
        // Arrange
        TagResponseDto expected =
            new TagResponseDto();
        UpdateTagRequestDto input = new UpdateTagRequestDto();
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tagProjectController.UpdateTag(
            ProjectId,
            TagId,
            input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task UpdateTag_Returns500_OnUnexpectedException()
    {
        UpdateTagRequestDto input = new UpdateTagRequestDto();
        UserContextStorage.OrganizationId = OrgId;
        _mockTagBusiness
            .Setup(b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _tagProjectController.UpdateTag(
            ProjectId,
            TagId,
            input);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdateTag_PassesToBusinessLayer()
    {
        // Arrange
        UpdateTagRequestDto input = new UpdateTagRequestDto();
        var expected = new TagResponseDto();

        UserContextStorage.OrganizationId = OrgId;
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockTagBusiness
            .Setup(b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tagProjectController.UpdateTag(
            ProjectId,
            TagId,
            input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockTagBusiness.Verify(
            b => b.UpdateTag(OrgId, UserId, ProjectId, TagId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateTag_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.UpdateTag),
            "projectId",
            "tagId",
            "tagRequestDto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
    }

    #endregion

    // =========================================================================
    // DeleteTag Tests
    // =========================================================================

    #region DeleteTag Tests

    [Fact]
    public async Task DeleteTag_Returns200()
    {
        // Arrange
        var expectedMessage = $"Tag deleted successfully";
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.DeleteTag(OrgId, ProjectId, TagId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _tagProjectController.DeleteTag(
            ProjectId,
            TagId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);

        var messageProperty = result.Value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var actualMessage = messageProperty.GetValue(result.Value) as string;
        Assert.Equal(expectedMessage, actualMessage);
    }


    [Fact]
    public async Task DeleteTag_Returns500_OnUnexpectedException()
    {
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.DeleteTag(OrgId, ProjectId, TagId))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _tagProjectController.DeleteTag(
            ProjectId,
            TagId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task DeleteTag_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.OrganizationId = OrgId;
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockTagBusiness
            .Setup(b => b.DeleteTag(OrgId, ProjectId, TagId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _tagProjectController.DeleteTag(
            ProjectId,
            TagId);

        var result = Assert.IsType<OkObjectResult>(actionResult);

        // Assert
        _mockTagBusiness.Verify(
            b => b.DeleteTag(OrgId, ProjectId, TagId),
            Times.Once);
    }

    [Fact]
    public void DeleteTag_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.DeleteTag),
            "projectId",
            "tagId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // ArchiveTag Tests
    // =========================================================================

    #region ArchiveTag Tests

    [Fact]
    public async Task ArchiveTag_Returns200_WhenArchiving()
    {
        // Arrange
        var expectedMessage = $"Archived tag {TagId}";

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.ArchiveTag(OrgId, UserId, ProjectId, TagId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _tagProjectController.ArchiveTag(
            ProjectId,
            TagId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);

        var messageProperty = result.Value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var actualMessage = messageProperty.GetValue(result.Value) as string;
        Assert.Equal(expectedMessage, actualMessage);
    }

    [Fact]
    public async Task ArchiveTag_Returns200_WhenUnarchiving()
    {
        // Arrange
        var expectedMessage = $"Unarchived tag {TagId}";

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(OrgId, UserId, ProjectId, TagId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _tagProjectController.ArchiveTag(
            ProjectId,
            TagId,
            false);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);

        var messageProperty = result.Value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var actualMessage = messageProperty.GetValue(result.Value) as string;
        Assert.Equal(expectedMessage, actualMessage);
    }

    [Fact]
    public async Task ArchiveTag_Returns500_OnUnexpectedException_WhenArchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.ArchiveTag(OrgId, UserId, ProjectId, TagId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _tagProjectController.ArchiveTag(
            ProjectId,
            TagId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);

        Assert.Contains(
            $"An error occurred while archiving tag {TagId}",
            message);
    }

    [Fact]
    public async Task ArchiveTag_Returns500_OnUnexpectedException_WhenUnarchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(OrgId, UserId, ProjectId, TagId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _tagProjectController.ArchiveTag(
            ProjectId,
            TagId,
            false);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);

        Assert.Contains(
            $"An error occurred while unarchiving tag {TagId}",
            message);
    }

    [Fact]
    public async Task ArchiveTag_PassesToBusinessLayer_WhenArchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.ArchiveTag(OrgId, UserId, ProjectId, TagId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _tagProjectController.ArchiveTag(
            ProjectId,
            TagId,
            true);

        var result = Assert.IsType<OkObjectResult>(actionResult);

        // Assert
        _mockTagBusiness.Verify(
            b => b.ArchiveTag(OrgId, UserId, ProjectId, TagId),
            Times.Once);

        _mockTagBusiness.Verify(
            b => b.UnarchiveTag(OrgId, UserId, ProjectId, TagId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveTag_PassesToBusinessLayer_WhenUnarchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.OrganizationId = OrgId;

        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(OrgId, UserId, ProjectId, TagId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _tagProjectController.ArchiveTag(
            ProjectId,
            TagId,
            false);

        var result = Assert.IsType<OkObjectResult>(actionResult);

        // Assert
        _mockTagBusiness.Verify(
            b => b.UnarchiveTag(OrgId, UserId, ProjectId, TagId),
            Times.Once);

        _mockTagBusiness.Verify(
            b => b.ArchiveTag(OrgId, UserId, ProjectId, TagId),
            Times.Never);
    }

    [Fact]
    public void ArchiveTag_HasHttpPatch()
    {
        var method = GetControllerMethod(
            nameof(TagProjectController.ArchiveTag),
            "projectId",
            "tagId",
            "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
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
        return Assert.Single(typeof(TagProjectController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}