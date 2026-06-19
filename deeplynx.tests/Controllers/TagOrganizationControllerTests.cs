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
public class TagOrganizationControllerTests : IDisposable
{
    private readonly Mock<ITagBusiness> _mockTagBusiness;
    private readonly Mock<ILogger<TagProjectController>> _mockLogger;
    private readonly TagOrganizationController _TagOrganizationController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long PermissionId = 15L;
    private static readonly long[] ProjectList = { 13L, 14L };
    private static readonly long[] PermissionList = { 13L, 14L };
    private const long RelationshipId = 22L;
    private const long TagId = 67L;


    public TagOrganizationControllerTests()
    {
        _mockTagBusiness = new Mock<ITagBusiness>();
        _mockLogger = new Mock<ILogger<TagProjectController>>();

        _TagOrganizationController = new TagOrganizationController(
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
        List<TagResponseDto> expected =
            new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetAllTags(OrgId, null, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _TagOrganizationController.GetAllTags(OrgId, null, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllTags_Returns200_WithEmptyList()
    {
        // Arrange

        _mockTagBusiness
            .Setup(b => b.GetAllTags(It.IsAny<long>(), null, It.IsAny<bool>()))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _TagOrganizationController.GetAllTags(It.IsAny<long>(), null, It.IsAny<bool>());

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetAllTags_Returns500_OnUnexpectedException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockTagBusiness
            .Setup(b => b.GetAllTags(OrgId, null, true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _TagOrganizationController.GetAllTags(
            OrgId,
            null,
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

        var expected = new List<TagResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetAllTags(OrgId, null, true))
            .ReturnsAsync(expected);

        // Act
        await _TagOrganizationController.GetAllTags(OrgId, null, true);

        // Assert
        _mockTagBusiness.Verify(
            b => b.GetAllTags(OrgId, null, true),
            Times.Once);
    }

    [Fact]
    public void GetAllTags_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.GetAllTags),
            "organizationId",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
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
        TagResponseDto expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, null, TagId, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _TagOrganizationController.GetTag(
            OrgId,
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

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, null, TagId, true))
            .ReturnsAsync((TagResponseDto)null!);

        // Act
        var actionResult = await _TagOrganizationController.GetTag(
            OrgId,
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
        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, null, TagId, true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _TagOrganizationController.GetTag(
            OrgId,
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

        var expected = new TagResponseDto();

        _mockTagBusiness
            .Setup(b => b.GetTag(OrgId, null, TagId, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _TagOrganizationController.GetTag(
            OrgId,
            TagId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockTagBusiness.Verify(
            b => b.GetTag(
                OrgId,
                null,
                TagId,
                true),
            Times.Once);
    }

    [Fact]
    public void GetTag_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.GetTag),
            "organizationId",
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
        TagResponseDto expected =
            new TagResponseDto();
        CreateTagRequestDto input = new CreateTagRequestDto();

        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, null, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _TagOrganizationController.CreateTag(
            OrgId,
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
        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, null, input))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _TagOrganizationController.CreateTag(
            OrgId,
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

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockTagBusiness
            .Setup(b => b.CreateTag(OrgId, UserId, null, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _TagOrganizationController.CreateTag(
            OrgId,
            input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockTagBusiness.Verify(
            b => b.CreateTag(OrgId, UserId, null, input),
            Times.Once);
    }

    [Fact]
    public void CreateTag_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.CreateTag),
            "organizationId",
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

        _mockTagBusiness
            .Setup(b => b.UpdateTag(UserId, TagId, OrgId, null, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _TagOrganizationController.UpdateTag(
            OrgId,
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
        _mockTagBusiness
            .Setup(b => b.UpdateTag(UserId, TagId, OrgId, null, input))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _TagOrganizationController.UpdateTag(
            OrgId,
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

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockTagBusiness
            .Setup(b => b.UpdateTag(UserId, TagId, OrgId, null, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _TagOrganizationController.UpdateTag(
            OrgId,
            TagId,
            input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockTagBusiness.Verify(
            b => b.UpdateTag(UserId, TagId, OrgId, null, input),
            Times.Once);
    }

    [Fact]
    public void UpdateTag_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.UpdateTag),
            "organizationId",
            "TagId",
            "dto");

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
        var expectedMessage = $"Deleted Tag {TagId}";

        _mockTagBusiness
            .Setup(b => b.DeleteTag(UserId, TagId, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.DeleteTag(
            OrgId,
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
        _mockTagBusiness
            .Setup(b => b.DeleteTag(UserId, TagId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _TagOrganizationController.DeleteTag(
            OrgId,
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
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockTagBusiness
            .Setup(b => b.DeleteTag(UserId, TagId, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.DeleteTag(
            OrgId,
            TagId);

        var result = Assert.IsType<OkObjectResult>(actionResult);

        // Assert
        _mockTagBusiness.Verify(
            b => b.DeleteTag(UserId, TagId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void DeleteTag_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.DeleteTag),
            "organizationId",
            "TagId");

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
        var expectedMessage = $"Archived Tag {TagId}";

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockTagBusiness
            .Setup(b => b.ArchiveTag(UserId, TagId, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.ArchiveTag(
            OrgId,
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
        var expectedMessage = $"Unarchived Tag {TagId}";

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(UserId, TagId, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.ArchiveTag(
            OrgId,
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

        _mockTagBusiness
            .Setup(b => b.ArchiveTag(UserId, TagId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _TagOrganizationController.ArchiveTag(
            OrgId,
            TagId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);

        Assert.Contains(
            $"An error occurred while archiving Tag {TagId}",
            message);
    }

    [Fact]
    public async Task ArchiveTag_Returns500_OnUnexpectedException_WhenUnarchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(UserId, TagId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _TagOrganizationController.ArchiveTag(
            OrgId,
            TagId,
            false);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);

        Assert.Contains(
            $"An error occurred while unarchiving Tag {TagId}",
            message);
    }

    [Fact]
    public async Task ArchiveTag_PassesToBusinessLayer_WhenArchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockTagBusiness
            .Setup(b => b.ArchiveTag(UserId, TagId, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.ArchiveTag(
            OrgId,
            TagId,
            true);

        var result = Assert.IsType<OkObjectResult>(actionResult);

        // Assert
        _mockTagBusiness.Verify(
            b => b.ArchiveTag(UserId, TagId, OrgId, null),
            Times.Once);

        _mockTagBusiness.Verify(
            b => b.UnarchiveTag(UserId, TagId, OrgId, null),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveTag_PassesToBusinessLayer_WhenUnarchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockTagBusiness
            .Setup(b => b.UnarchiveTag(UserId, TagId, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.ArchiveTag(
            OrgId,
            TagId,
            false);

        var result = Assert.IsType<OkObjectResult>(actionResult);

        // Assert
        _mockTagBusiness.Verify(
            b => b.UnarchiveTag(UserId, TagId, OrgId, null),
            Times.Once);

        _mockTagBusiness.Verify(
            b => b.ArchiveTag(UserId, TagId, OrgId, null),
            Times.Never);
    }

    [Fact]
    public void ArchiveTag_HasHttpPatch()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.ArchiveTag),
            "organizationId",
            "TagId",
            "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
    }

    #endregion

    // =========================================================================
    // GetPermissionsByTag Tests
    // =========================================================================

    #region GetPermissionsByTag Tests

    [Fact]
    public async Task GetPermissionsByTag_Returns200_WithPermission()
    {
        // Arrange
        List<PermissionResponseDto> expected = new List<PermissionResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetPermissionsByTag(TagId, OrgId, null))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _TagOrganizationController.GetPermissionsByTag(
            OrgId,
            TagId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetPermissionsByTag_Returns200_WithEmptyList()
    {
        // Arrange

        _mockTagBusiness
            .Setup(b => b.GetPermissionsByTag(TagId, OrgId, null))
            .ReturnsAsync((List<PermissionResponseDto>)null!);

        // Act
        var actionResult = await _TagOrganizationController.GetPermissionsByTag(
            OrgId,
            TagId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPermissionsByTag_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockTagBusiness
            .Setup(b => b.GetPermissionsByTag(TagId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _TagOrganizationController.GetPermissionsByTag(
            OrgId,
            TagId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetPermissionsByTag_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new List<PermissionResponseDto>();

        _mockTagBusiness
            .Setup(b => b.GetPermissionsByTag(TagId, OrgId, null))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _TagOrganizationController.GetPermissionsByTag(
            OrgId,
            TagId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockTagBusiness.Verify(
            b => b.GetPermissionsByTag(TagId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void GetPermissionsByTag_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.GetPermissionsByTag),
            "organizationId",
            "TagId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // AddPermissionToTag Tests
    // =========================================================================

    #region AddPermissionToTag Tests

    [Fact]
    public async Task AddPermissionToTag_Returns200_WithPermission()
    {
        // Arrange
        var expectedMessage = $"Added permission {PermissionId} to Tag {TagId}";

        _mockTagBusiness
            .Setup(b => b.AddPermissionToTag(TagId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.AddPermissionToTag(
            OrgId,
            TagId,
            PermissionId);

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
    public async Task AddPermissionToTag_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockTagBusiness
            .Setup(b => b.AddPermissionToTag(TagId, PermissionId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _TagOrganizationController.AddPermissionToTag(
            OrgId,
            TagId,
            PermissionId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task AddPermissionToTag_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new List<PermissionResponseDto>();

        _mockTagBusiness
            .Setup(b => b.AddPermissionToTag(TagId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.AddPermissionToTag(
            OrgId,
            TagId,
            PermissionId);

        // Assert
        _mockTagBusiness.Verify(
            b => b.AddPermissionToTag(TagId, PermissionId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void AddPermissionToTag_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.AddPermissionToTag),
            "organizationId",
            "TagId",
            "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // RemovePermissionFromTag Tests
    // =========================================================================

    #region RemovePermissionFromTag Tests

    [Fact]
    public async Task RemovePermissionFromTag_Returns200_WithPermission()
    {
        // Arrange
        var expectedMessage = $"Removed permission {PermissionId} from Tag {TagId}";

        _mockTagBusiness
            .Setup(b => b.RemovePermissionFromTag(TagId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.RemovePermissionFromTag(
            OrgId,
            TagId,
            PermissionId);

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
    public async Task RemovePermissionFromTag_Returns200_WithEmptyList()
    {
        // Arrange

        _mockTagBusiness
            .Setup(b => b.RemovePermissionFromTag(TagId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.RemovePermissionFromTag(
            OrgId,
            TagId,
            PermissionId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);

        var messageProperty = result.Value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var actualMessage = messageProperty.GetValue(result.Value) as string;
    }

    [Fact]
    public async Task RemovePermissionFromTag_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockTagBusiness
            .Setup(b => b.RemovePermissionFromTag(TagId, PermissionId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _TagOrganizationController.RemovePermissionFromTag(
            OrgId,
            TagId,
            PermissionId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task RemovePermissionFromTag_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new List<PermissionResponseDto>();

        _mockTagBusiness
            .Setup(b => b.RemovePermissionFromTag(TagId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.RemovePermissionFromTag(
            OrgId,
            TagId,
            PermissionId);

        // Assert
        _mockTagBusiness.Verify(
            b => b.RemovePermissionFromTag(TagId, PermissionId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void RemovePermissionFromTag_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.RemovePermissionFromTag),
            "organizationId",
            "TagId",
            "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // SetPermissionsForTag Tests
    // =========================================================================

    #region SetPermissionsForTag Tests

    [Fact]
    public async Task SetPermissionsForTag_Returns200_WithPermission()
    {
        // Arrange
        var expectedMessage = $"Set permissions for Tag {TagId}";

        _mockTagBusiness
            .Setup(b => b.SetPermissionsForTag(TagId, PermissionList, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.SetPermissionsForTag(
            OrgId,
            TagId,
            PermissionList);

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
    public async Task SetPermissionsForTag_Returns200_WithEmptyList()
    {
        // Arrange

        _mockTagBusiness
            .Setup(b => b.SetPermissionsForTag(TagId, PermissionList, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.SetPermissionsForTag(
            OrgId,
            TagId,
            PermissionList);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);

        var messageProperty = result.Value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var actualMessage = messageProperty.GetValue(result.Value) as string;
    }

    [Fact]
    public async Task SetPermissionsForTag_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockTagBusiness
            .Setup(b => b.SetPermissionsForTag(TagId, PermissionList, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _TagOrganizationController.SetPermissionsForTag(
            OrgId,
            TagId,
            PermissionList);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task SetPermissionsForTag_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new List<PermissionResponseDto>();

        _mockTagBusiness
            .Setup(b => b.SetPermissionsForTag(TagId, PermissionList, OrgId, null))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _TagOrganizationController.SetPermissionsForTag(
            OrgId,
            TagId,
            PermissionList);

        // Assert
        _mockTagBusiness.Verify(
            b => b.SetPermissionsForTag(TagId, PermissionList, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void SetPermissionsForTag_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(TagOrganizationController.SetPermissionsForTag),
            "organizationId",
            "TagId",
            "permissionIds");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
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
        return Assert.Single(typeof(TagOrganizationController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}