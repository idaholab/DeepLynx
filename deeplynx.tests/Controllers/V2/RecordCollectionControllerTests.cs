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
///     Unit tests for <see cref="RecordCollectionController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class RecordCollectionControllerTests : IDisposable
{
    private readonly Mock<IRecordCollectionBusiness> _mockRecordCollectionBusiness;
    private readonly Mock<ILogger<RecordCollectionController>> _mockLogger;
    private readonly RecordCollectionController _recordCollectionController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private static readonly long[] ProjectList = { 13L, 14L };
    private const long UserId = 10L;
    private const long CollectionId = 7L;
    private const long RecordCollectionId = 8L;
    private const long RecordIdConst = 20L;
    private const long TagId = 30L;

    public RecordCollectionControllerTests()
    {
        _mockRecordCollectionBusiness = new Mock<IRecordCollectionBusiness>();
        _mockLogger = new Mock<ILogger<RecordCollectionController>>();

        _recordCollectionController = new RecordCollectionController(
            _mockRecordCollectionBusiness.Object,
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
    // GetAllRecordCollections Tests
    // =========================================================================

    #region GetAllRecordCollections Tests

    [Fact]
    public async Task GetAllRecordCollections_Returns200_WithList()
    {
        var paginatedRequestDto = new PaginatedRequestDto();
        var expected = new PaginatedResponse<RecordCollectionResponseDto>
        {
            Items = new List<RecordCollectionResponseDto>
            {
                new(),
                new()
            },
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 2
        };

        _mockRecordCollectionBusiness.Setup(b => b.GetAllRecordCollectionsPaginated(
                         UserId, OrgId, ProjectId, null, null, null, null, paginatedRequestDto, true, false, false, false))
                     .ReturnsAsync(expected);

        var result = (await _recordCollectionController.GetAllRecordCollections(
            OrgId,
            ProjectId, null, null, null, null, true, paginatedRequestDto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var actual = Assert.IsType<PaginatedResponse<RecordCollectionResponseDtoV2>>(result.Value);
        Assert.Equal(2, actual.Items.Count);
    }

    [Fact]
    public async Task GetAllRecordCollections_Returns200_WithEmptyList()
    {
        var expected = new PaginatedResponse<RecordCollectionResponseDto>
        {
            Items = [],
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 0
        };

        _mockRecordCollectionBusiness.Setup(b => b.GetAllRecordCollectionsPaginated(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<string?>(), It.IsAny<long[]?>(), It.IsAny<long[]?>(), It.IsAny<string?>(),
                         It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ReturnsAsync(expected);

        var result = (await _recordCollectionController.GetAllRecordCollections(
            OrgId,
            ProjectId, null, null, null, null, true, null)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<PaginatedResponse<RecordCollectionResponseDtoV2>>(result.Value);
    }

    [Fact]
    public async Task GetAllRecordCollections_Returns500_OnUnexpectedException()
    {
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = false;
        UserContextStorage.IsOrgAdmin = false;
        UserContextStorage.IsProjectAdmin = false;

        _mockRecordCollectionBusiness
            .Setup(b => b.GetAllRecordCollectionsPaginated(
                UserId,
                OrgId,
                ProjectId,
                null, null, null, null,
                It.IsAny<PaginatedRequestDto>(),
                true,
                false,
                false,
                false))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _recordCollectionController.GetAllRecordCollections(
            OrgId,
            ProjectId,
            null, null, null, null,
            true,
            null));
    }

    [Fact]
    public async Task GetAllRecordCollections_PassesIdsHideArchivedAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;
        var paginatedRequestDto = new PaginatedRequestDto
        {
            PageNumber = 2,
            PageSize = 10
        };
        var expected = new PaginatedResponse<RecordCollectionResponseDto>
        {
            Items = [],
            PageNumber = 2,
            PageSize = 10,
            TotalCount = 0
        };

        _mockRecordCollectionBusiness.Setup(b => b.GetAllRecordCollectionsPaginated(
                         UserId, OrgId, ProjectId, "collection", null, null, null, paginatedRequestDto, false, true, true, true))
                     .ReturnsAsync(expected);

        await _recordCollectionController.GetAllRecordCollections(
            OrgId,
            ProjectId,
            "collection", null, null, null,
            hideArchived: false,
            paginatedRequestDto: paginatedRequestDto);

        _mockRecordCollectionBusiness.Verify(b => b.GetAllRecordCollectionsPaginated(
            UserId, OrgId, ProjectId, "collection", null, null, null, paginatedRequestDto, false, true, true, true), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetRecordsInRecordCollection Tests
    // =========================================================================

    #region GetRecordsInRecordCollection Tests

    [Fact]
    public async Task GetRecordsInRecordCollection_Returns200_WithList()
    {
        var expected = new PaginatedResponse<RecordResponseDto>
        {
            Items = new List<RecordResponseDto> { new(), new() },
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 2
        };

        _mockRecordCollectionBusiness.Setup(b => b.GetRecordsInRecordCollectionPaginated(
            UserId, OrgId, ProjectId, CollectionId, true, It.IsAny<PaginatedRequestDto>(), false, false, false))
                    .ReturnsAsync(expected);

        var result = (await _recordCollectionController.GetRecordsInRecordCollection(
            OrgId,
            ProjectId,
            CollectionId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var actual = Assert.IsType<PaginatedResponse<RecordResponseDtoV2>>(result.Value);
        Assert.Equal(2, actual.Items.Count);
    }

    [Fact]
    public async Task GetRecordsInRecordCollection_Returns200_WithEmptyList()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetRecordsInRecordCollectionPaginated(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<bool>(), It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ReturnsAsync(new PaginatedResponse<RecordResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        var result = (await _recordCollectionController.GetRecordsInRecordCollection(
            OrgId,
            ProjectId,
            CollectionId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<PaginatedResponse<RecordResponseDtoV2>>(result.Value);
    }

    [Fact]
    public async Task GetRecordsInRecordCollection_Returns404_OnKeyNotFoundException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetRecordsInRecordCollectionPaginated(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<bool>(), It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new KeyNotFoundException("record collection not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _recordCollectionController.GetRecordsInRecordCollection(
            OrgId,
            ProjectId,
            CollectionId, true));
    }

    [Fact]
    public async Task GetRecordsInRecordCollection_Returns500_OnUnexpectedException()
    {
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = false;
        UserContextStorage.IsOrgAdmin = false;
        UserContextStorage.IsProjectAdmin = false;

        _mockRecordCollectionBusiness
            .Setup(b => b.GetRecordsInRecordCollectionPaginated(
                UserId,
                OrgId,
                ProjectId,
                CollectionId,
                true,
                It.IsAny<PaginatedRequestDto>(),
                false,
                false,
                false))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _recordCollectionController.GetRecordsInRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            true));
    }

    [Fact]
    public async Task GetRecordsInRecordCollection_PassesIdsHideArchivedAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        _mockRecordCollectionBusiness.Setup(b => b.GetRecordsInRecordCollectionPaginated(
                         UserId, OrgId, ProjectId, CollectionId, false, It.IsAny<PaginatedRequestDto>(), true, true, true))
                     .ReturnsAsync(new PaginatedResponse<RecordResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        await _recordCollectionController.GetRecordsInRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            hideArchived: false);

        _mockRecordCollectionBusiness.Verify(b => b.GetRecordsInRecordCollectionPaginated(
            UserId, OrgId, ProjectId, CollectionId, false, It.IsAny<PaginatedRequestDto>(), true, true, true), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetRecordCollectionsForARecord Tests
    // =========================================================================

    #region GetRecordCollectionsForARecord Tests

    [Fact]
    public async Task GetRecordCollectionsForARecord_Returns200_WithList()
    {
        var expected = new PaginatedResponse<RecordCollectionResponseDto>
        {
            Items = new List<RecordCollectionResponseDto> { new(), new() },
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 2
        };

        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsForRecordPaginated(
                         UserId, OrgId, ProjectId, RecordIdConst, true, It.IsAny<PaginatedRequestDto>(), false, false, false))
                     .ReturnsAsync(expected);

        var result = (await _recordCollectionController.GetRecordCollectionsForARecord(
            OrgId, ProjectId, RecordIdConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var actual = Assert.IsType<PaginatedResponse<RecordCollectionResponseDtoV2>>(result.Value);
        Assert.Equal(2, actual.Items.Count);
    }

    [Fact]
    public async Task GetRecordCollectionsForARecord_Returns200_WithEmptyList()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsForRecordPaginated(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<bool>(), It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ReturnsAsync(new PaginatedResponse<RecordCollectionResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        var result = (await _recordCollectionController.GetRecordCollectionsForARecord(
            OrgId, ProjectId, RecordIdConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<PaginatedResponse<RecordCollectionResponseDtoV2>>(result.Value);
    }

    [Fact]
    public async Task GetRecordCollectionsForARecord_ThrowsException_WhenBusinessThrows()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsForRecordPaginated(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<bool>(), It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _recordCollectionController.GetRecordCollectionsForARecord(
            OrgId, ProjectId, RecordIdConst, true));
    }

    [Fact]
    public async Task GetRecordCollectionsForARecord_PassesIdsAndHideArchivedToBusinessLayer()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsForRecordPaginated(
                         UserId, OrgId, ProjectId, RecordIdConst, false, It.IsAny<PaginatedRequestDto>(), false, false, false))
                     .ReturnsAsync(new PaginatedResponse<RecordCollectionResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        await _recordCollectionController.GetRecordCollectionsForARecord(
            OrgId, ProjectId, RecordIdConst, hideArchived: false);

        _mockRecordCollectionBusiness.Verify(b => b.GetRecordCollectionsForRecordPaginated(
            UserId, OrgId, ProjectId, RecordIdConst, false, It.IsAny<PaginatedRequestDto>(), false, false, false), Times.Once);
    }

    [Fact]
    public async Task GetRecordCollectionsForARecord_DefaultsPaginatedRequestDto_WhenNotProvided()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsForRecordPaginated(
                         UserId, OrgId, ProjectId, RecordIdConst, true,
                         It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25),
                         false, false, false))
                     .ReturnsAsync(new PaginatedResponse<RecordCollectionResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        await _recordCollectionController.GetRecordCollectionsForARecord(OrgId, ProjectId, RecordIdConst, true);

        _mockRecordCollectionBusiness.Verify(b => b.GetRecordCollectionsForRecordPaginated(
            UserId, OrgId, ProjectId, RecordIdConst, true,
            It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25),
            false, false, false), Times.Once);
    }

    [Fact]
    public async Task GetRecordCollectionsForARecord_PassesProvidedPaginatedRequestDto_WhenGiven()
    {
        var pagination = new PaginatedRequestDto { PageNumber = 4, PageSize = 50 };

        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsForRecordPaginated(
                         UserId, OrgId, ProjectId, RecordIdConst, true,
                         It.Is<PaginatedRequestDto>(p => p.PageNumber == 4 && p.PageSize == 50),
                         false, false, false))
                     .ReturnsAsync(new PaginatedResponse<RecordCollectionResponseDto>
                     {
                         Items = [],
                         PageNumber = 4,
                         PageSize = 50,
                         TotalCount = 0
                     });

        await _recordCollectionController.GetRecordCollectionsForARecord(OrgId, ProjectId, RecordIdConst, true, pagination);

        _mockRecordCollectionBusiness.Verify(b => b.GetRecordCollectionsForRecordPaginated(
            UserId, OrgId, ProjectId, RecordIdConst, true,
            It.Is<PaginatedRequestDto>(p => p.PageNumber == 4 && p.PageSize == 50),
            false, false, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetRecordCollectionsByTags Tests
    // =========================================================================

    #region GetRecordCollectionsByTags Tests

    [Fact]
    public async Task GetRecordCollectionsByTags_Returns200_WithList()
    {
        var expected = new PaginatedResponse<RecordCollectionResponseDto>
        {
            Items = new List<RecordCollectionResponseDto> { new(), new() },
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 2
        };

        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsByTagsPaginated(
                         UserId, OrgId, ProjectId, It.Is<long[]>(t => t.SequenceEqual(new[] { TagId })),
                         It.IsAny<PaginatedRequestDto>(), true, false, false, false))
                     .ReturnsAsync(expected);

        var result = (await _recordCollectionController.GetRecordCollectionsByTags(
            OrgId, ProjectId, new[] { TagId }, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var actual = Assert.IsType<PaginatedResponse<RecordCollectionResponseDtoV2>>(result.Value);
        Assert.Equal(2, actual.Items.Count);
    }

    [Fact]
    public async Task GetRecordCollectionsByTags_Returns200_WithEmptyList()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsByTagsPaginated(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]>(),
                         It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ReturnsAsync(new PaginatedResponse<RecordCollectionResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        var result = (await _recordCollectionController.GetRecordCollectionsByTags(
            OrgId, ProjectId, new[] { TagId }, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<PaginatedResponse<RecordCollectionResponseDtoV2>>(result.Value);
    }

    [Fact]
    public async Task GetRecordCollectionsByTags_Returns500_OnUnexpectedException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsByTagsPaginated(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]>(),
                         It.IsAny<PaginatedRequestDto>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _recordCollectionController.GetRecordCollectionsByTags(
            OrgId, ProjectId, new[] { TagId }, true));
    }

    [Fact]
    public async Task GetRecordCollectionsByTags_PassesIdsAndHideArchivedToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsByTagsPaginated(
                         UserId, OrgId, ProjectId, It.Is<long[]>(t => t.SequenceEqual(new[] { TagId })),
                         It.IsAny<PaginatedRequestDto>(), false, true, true, true))
                     .ReturnsAsync(new PaginatedResponse<RecordCollectionResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        await _recordCollectionController.GetRecordCollectionsByTags(
            OrgId, ProjectId, new[] { TagId }, hideArchived: false);

        _mockRecordCollectionBusiness.Verify(b => b.GetRecordCollectionsByTagsPaginated(
            UserId, OrgId, ProjectId, It.Is<long[]>(t => t.SequenceEqual(new[] { TagId })),
            It.IsAny<PaginatedRequestDto>(), false, true, true, true), Times.Once);
    }

    [Fact]
    public async Task GetRecordCollectionsByTags_DefaultsPaginatedRequestDto_WhenNotProvided()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsByTagsPaginated(
                         UserId, OrgId, ProjectId, It.Is<long[]>(t => t.SequenceEqual(new[] { TagId })),
                         It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25),
                         true, false, false, false))
                     .ReturnsAsync(new PaginatedResponse<RecordCollectionResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        await _recordCollectionController.GetRecordCollectionsByTags(OrgId, ProjectId, new[] { TagId }, true);

        _mockRecordCollectionBusiness.Verify(b => b.GetRecordCollectionsByTagsPaginated(
            UserId, OrgId, ProjectId, It.Is<long[]>(t => t.SequenceEqual(new[] { TagId })),
            It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25),
            true, false, false, false), Times.Once);
    }

    [Fact]
    public async Task GetRecordCollectionsByTags_PassesProvidedPaginatedRequestDto_WhenGiven()
    {
        var pagination = new PaginatedRequestDto { PageNumber = 4, PageSize = 50 };

        _mockRecordCollectionBusiness.Setup(b => b.GetRecordCollectionsByTagsPaginated(
                         UserId, OrgId, ProjectId, It.Is<long[]>(t => t.SequenceEqual(new[] { TagId })),
                         It.Is<PaginatedRequestDto>(p => p.PageNumber == 4 && p.PageSize == 50),
                         true, false, false, false))
                     .ReturnsAsync(new PaginatedResponse<RecordCollectionResponseDto>
                     {
                         Items = [],
                         PageNumber = 4,
                         PageSize = 50,
                         TotalCount = 0
                     });

        await _recordCollectionController.GetRecordCollectionsByTags(
            OrgId, ProjectId, new[] { TagId }, true, pagination);

        _mockRecordCollectionBusiness.Verify(b => b.GetRecordCollectionsByTagsPaginated(
            UserId, OrgId, ProjectId, It.Is<long[]>(t => t.SequenceEqual(new[] { TagId })),
            It.Is<PaginatedRequestDto>(p => p.PageNumber == 4 && p.PageSize == 50),
            true, false, false, false), Times.Once);
    }

    [Fact]
    public void GetRecordCollectionsByTags_HasHttpGetAndReadRecordCollectionAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.GetRecordCollectionsByTags),
            "organizationId",
            "projectId",
            "tagIds",
            "hideArchived",
            "paginatedRequestDto");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record_collection");
        AssertHasAuthAttribute(method, "read", "tag");
        AssertHasSensitivityAttribute(method, "read record");
    }

    #endregion

    // =========================================================================
    // GetSensitivityLabelsForRecordCollection Tests
    // =========================================================================

    #region GetSensitivityLabelsForRecordCollection Tests

    [Fact]
    public async Task GetSensitivityLabelsForRecordCollection_Returns200_WithList()
    {
        var expected = new PaginatedResponse<SensitivityLabelResponseDto>
        {
            Items = new List<SensitivityLabelResponseDto> { new(), new() },
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 2
        };

        _mockRecordCollectionBusiness.Setup(b => b.GetSensitivityLabelsForRecordCollectionPaginated(
            OrgId, ProjectId, CollectionId, It.IsAny<PaginatedRequestDto>()))
                    .ReturnsAsync(expected);

        var result = (await _recordCollectionController.GetSensitivityLabelsForRecordCollection(
            OrgId, ProjectId, CollectionId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetSensitivityLabelsForRecordCollection_Returns200_WithEmptyList()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetSensitivityLabelsForRecordCollectionPaginated(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<PaginatedRequestDto>()))
                     .ReturnsAsync(new PaginatedResponse<SensitivityLabelResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        var result = (await _recordCollectionController.GetSensitivityLabelsForRecordCollection(
            OrgId, ProjectId, CollectionId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<PaginatedResponse<SensitivityLabelResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetSensitivityLabelsForRecordCollection_Returns404_OnKeyNotFoundException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetSensitivityLabelsForRecordCollectionPaginated(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<PaginatedRequestDto>()))
                     .ThrowsAsync(new KeyNotFoundException("record collection not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _recordCollectionController.GetSensitivityLabelsForRecordCollection(
            OrgId, ProjectId, CollectionId));
    }

    [Fact]
    public async Task GetSensitivityLabelsForRecordCollection_PassesIdsAndPaginationToBusinessLayer()
    {
        _mockRecordCollectionBusiness.Setup(b => b.GetSensitivityLabelsForRecordCollectionPaginated(
                         OrgId, ProjectId, CollectionId, It.IsAny<PaginatedRequestDto>()))
                     .ReturnsAsync(new PaginatedResponse<SensitivityLabelResponseDto>
                     {
                         Items = [],
                         PageNumber = 1,
                         PageSize = 25,
                         TotalCount = 0
                     });

        await _recordCollectionController.GetSensitivityLabelsForRecordCollection(
            OrgId, ProjectId, CollectionId);

        _mockRecordCollectionBusiness.Verify(b => b.GetSensitivityLabelsForRecordCollectionPaginated(
            OrgId, ProjectId, CollectionId, It.IsAny<PaginatedRequestDto>()), Times.Once);
    }

    [Fact]
    public void GetSensitivityLabelsForRecordCollection_HasHttpGetAndReadRecordCollectionAndReadSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.GetSensitivityLabelsForRecordCollection),
            "organizationId",
            "projectId",
            "recordCollectionId",
            "paginatedRequestDto");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record_collection");
        AssertHasAuthAttribute(method, "read", "sensitivity_label");
        AssertHasSensitivityAttribute(method, "read record");
    }

    #endregion

    // =========================================================================
    // AddRecordsToRecordCollection Tests
    // =========================================================================

    #region AddRecordsToRecordCollection Tests

    [Fact]
    public async Task AddRecordsToRecordCollection_Returns200_OnSuccess()
    {
        _mockRecordCollectionBusiness.Setup(b => b.AddRecordsToRecordCollection(
                         UserId,
                         OrgId,
                         ProjectId,
                         CollectionId,
                         ProjectList,
                         false,
                         false,
                         false))
                     .ReturnsAsync(true);

        var result = await _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_Returns400_OnArgumentException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.AddRecordsToRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new ArgumentException("invalid request"));

        await Assert.ThrowsAsync<ArgumentException>(() => _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList));
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_Returns404_OnKeyNotFoundException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.AddRecordsToRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new KeyNotFoundException("record not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList));
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_Returns403_OnUnauthorizedAccessException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.AddRecordsToRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new UnauthorizedAccessException("not allowed"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList));
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_Returns500_OnUnexpectedException()
    {
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = false;
        UserContextStorage.IsOrgAdmin = false;
        UserContextStorage.IsProjectAdmin = false;

        var recordIds = ProjectList;

        _mockRecordCollectionBusiness
            .Setup(b => b.AddRecordsToRecordCollection(
                UserId,
                OrgId,
                ProjectId,
                CollectionId,
                It.Is<long[]>(ids => ids.SequenceEqual(recordIds)),
                false,
                false,
                false))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            recordIds));
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_PassesIdsRecordIdsAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        _mockRecordCollectionBusiness.Setup(b => b.AddRecordsToRecordCollection(
                         UserId,
                         OrgId,
                         ProjectId,
                         CollectionId,
                         ProjectList,
                         true,
                         true,
                         true))
                     .ReturnsAsync(true);

        await _recordCollectionController.AddRecordsToRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList);

        _mockRecordCollectionBusiness.Verify(b => b.AddRecordsToRecordCollection(
            UserId,
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList,
            true,
            true,
            true), Times.Once);
    }

    #endregion

    // =========================================================================
    // RemoveRecordsFromRecordCollection Tests
    // =========================================================================

    #region RemoveRecordsFromRecordCollection Tests

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_Returns200_OnSuccess()
    {
        _mockRecordCollectionBusiness.Setup(b => b.RemoveRecordsFromRecordCollection(
                         UserId,
                         OrgId,
                         ProjectId,
                         CollectionId,
                         ProjectList,
                         false,
                         false,
                         false))
                     .ReturnsAsync(true);

        var result = await _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_Returns400_OnArgumentException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.RemoveRecordsFromRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new ArgumentException("invalid request"));

        await Assert.ThrowsAsync<ArgumentException>(() => _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList));
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_Returns404_OnKeyNotFoundException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.RemoveRecordsFromRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new KeyNotFoundException("record not found"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList));
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_Returns403_OnUnauthorizedAccessException()
    {
        _mockRecordCollectionBusiness.Setup(b => b.RemoveRecordsFromRecordCollection(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new UnauthorizedAccessException("not allowed"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList));
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_Returns500_OnUnexpectedException()
    {
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = false;
        UserContextStorage.IsOrgAdmin = false;
        UserContextStorage.IsProjectAdmin = false;

        var recordIds = ProjectList;

        _mockRecordCollectionBusiness
            .Setup(b => b.RemoveRecordsFromRecordCollection(
                UserId,
                OrgId,
                ProjectId,
                CollectionId,
                It.Is<long[]>(ids => ids.SequenceEqual(recordIds)),
                false,
                false,
                false))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            recordIds));
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_PassesIdsRecordIdsAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        var recordIds = new List<long> { RecordIdConst };
        var request = new UpdateRecordCollectionRequestDto
        {
            RecordIds = recordIds
        };

        _mockRecordCollectionBusiness.Setup(b => b.RemoveRecordsFromRecordCollection(
                         UserId,
                         OrgId,
                         ProjectId,
                         CollectionId,
                         ProjectList,
                         true,
                         true,
                         true))
                     .ReturnsAsync(true);

        await _recordCollectionController.RemoveRecordsFromRecordCollection(
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList);

        _mockRecordCollectionBusiness.Verify(b => b.RemoveRecordsFromRecordCollection(
            UserId,
            OrgId,
            ProjectId,
            CollectionId,
            ProjectList,
            true,
            true,
            true), Times.Once);
    }

    #endregion

    // =========================================================================
    // CreateRecordCollection Tests
    // =========================================================================

    #region CreateRecordCollection Tests

    [Fact]
    public async Task CreateRecordCollection_Returns200_WithRecordCollection()
    {
        var request = new CreateRecordCollectionRequestDto();
        var expected = new RecordCollectionResponseDto();

        _mockRecordCollectionBusiness.Setup(b => b.CreateRecordCollection(
                         UserId, OrgId, ProjectId, null, request))
                     .ReturnsAsync(expected);

        var result = (await _recordCollectionController.CreateRecordCollection(
            OrgId,
            ProjectId,
            null,
            request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsType<RecordCollectionResponseDtoV2>(result.Value);
    }

    [Fact]
    public async Task CreateRecordCollection_Returns500_OnUnexpectedException()
    {
        UserContextStorage.UserId = UserId;

        var sensitivityLabelIds = new List<long>();
        var request = new CreateRecordCollectionRequestDto();

        _mockRecordCollectionBusiness
            .Setup(b => b.CreateRecordCollection(
                UserId,
                OrgId,
                ProjectId,
                It.Is<List<long>>(ids => ReferenceEquals(ids, sensitivityLabelIds)),
                It.Is<CreateRecordCollectionRequestDto>(dto => ReferenceEquals(dto, request))))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _recordCollectionController.CreateRecordCollection(
            OrgId,
            ProjectId,
            sensitivityLabelIds,
            request));
    }

    [Fact]
    public async Task CreateRecordCollection_PassesCurrentUserIdIdsAndRequestToBusinessLayer()
    {
        var request = new CreateRecordCollectionRequestDto();
        var expected = new RecordCollectionResponseDto();

        _mockRecordCollectionBusiness.Setup(b => b.CreateRecordCollection(
                         UserId, OrgId, ProjectId, null, request))
                     .ReturnsAsync(expected);

        await _recordCollectionController.CreateRecordCollection(
            OrgId,
            ProjectId,
            null,
            request);

        _mockRecordCollectionBusiness.Verify(b => b.CreateRecordCollection(
            UserId, OrgId, ProjectId, null, request), Times.Once);
    }

    #endregion

    // =========================================================================
    // DeleteRecordCollection Tests
    // =========================================================================

    #region DeleteRecordCollection Tests

    [Fact]
    public async Task DeleteRecordCollection_Returns200_WithMessage()
    {
        _mockRecordCollectionBusiness.Setup(b => b.DeleteRecordCollection(
                         UserId, OrgId, ProjectId, RecordCollectionId))
                     .Returns(Task.FromResult(true));

        var result = await _recordCollectionController.DeleteRecordCollection(
            OrgId,
            ProjectId,
            RecordCollectionId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task DeleteRecordCollection_Returns500_OnUnexpectedException()
    {
        UserContextStorage.UserId = UserId;

        _mockRecordCollectionBusiness
            .Setup(b => b.DeleteRecordCollection(
                UserId,
                OrgId,
                ProjectId,
                RecordCollectionId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _recordCollectionController.DeleteRecordCollection(
            OrgId,
            ProjectId,
            RecordCollectionId));
    }

    [Fact]
    public async Task DeleteRecordCollection_PassesCurrentUserIdAndIdsToBusinessLayer()
    {
        _mockRecordCollectionBusiness.Setup(b => b.DeleteRecordCollection(
                         UserId, OrgId, ProjectId, RecordCollectionId))
                     .Returns(Task.FromResult(true));

        await _recordCollectionController.DeleteRecordCollection(
            OrgId,
            ProjectId,
            RecordCollectionId);

        _mockRecordCollectionBusiness.Verify(b => b.DeleteRecordCollection(
            UserId, OrgId, ProjectId, RecordCollectionId), Times.Once);
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void RecordCollectionController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(RecordCollectionController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void GetAllRecordCollections_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.GetAllRecordCollections),
            "organizationId",
            "projectId",
            "search",
            "sensitivityLabelIds",
            "tagIds",
            "sort",
            "hideArchived",
            "paginatedRequestDto");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    [Fact]
    public void GetRecordsInRecordCollection_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.GetRecordsInRecordCollection),
            "organizationId",
            "projectId",
            "recordCollectionId",
            "hideArchived",
            "paginatedDto");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    [Fact]
    public void AddRecordsToRecordCollection_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.AddRecordsToRecordCollection),
            "organizationId",
            "projectId",
            "recordCollectionId",
            "recordIds");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "update", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    [Fact]
    public void RemoveRecordsFromRecordCollection_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.RemoveRecordsFromRecordCollection),
            "organizationId",
            "projectId",
            "recordCollectionId",
            "recordIds");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    [Fact]
    public void CreateRecordCollection_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.CreateRecordCollection),
            "organizationId",
            "projectId",
            "sensitivityLabelIds",
            "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    [Fact]
    public void DeleteRecordCollection_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(RecordCollectionController.DeleteRecordCollection),
            "organizationId",
            "projectId",
            "recordCollectionId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "record_collection");
        AssertHasSensitivityAttribute(method, "read record");
    }

    #endregion

    // =========================================================================
    // Helpers for Auth / Middleware Metadata Tests
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(RecordCollectionController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => method.GetParameters()
                .Select(parameter => parameter.Name ?? string.Empty)
                .SequenceEqual(parameterNames)));
    }

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

    private static void AssertHasSensitivityAttribute(
        System.Reflection.MethodInfo method,
        string expectedSensitivity)
    {
        var sensitivityAttributes = method.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "SensitivityAttribute")
            .ToList();

        Assert.Contains(sensitivityAttributes, attribute =>
            attribute.ConstructorArguments.Any(argument =>
                argument.Value?.ToString() == expectedSensitivity));
    }
}