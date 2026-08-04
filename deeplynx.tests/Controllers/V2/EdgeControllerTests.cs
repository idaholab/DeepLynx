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
///     Unit tests for the V2 actions of <see cref="EdgeController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class EdgeControllerTests : IDisposable
{
    private readonly Mock<IEdgeBusiness> _mockEdgeBusiness;
    private readonly Mock<ILogger<EdgeController>> _mockLogger;
    private readonly EdgeController _edgeController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long EdgeIdConst = 30L;
    private const long DataSourceId = 5L;
    private const long OriginId = 40L;
    private const long DestinationId = 41L;

    public EdgeControllerTests()
    {
        _mockEdgeBusiness = new Mock<IEdgeBusiness>();
        _mockLogger = new Mock<ILogger<EdgeController>>();

        _edgeController = new EdgeController(
            _mockEdgeBusiness.Object,
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
    // GetAllEdges Tests
    // =========================================================================

    #region GetAllEdges Tests

    [Fact]
    public async Task GetAllEdges_Returns200_WithList()
    {
        var expected = new List<EdgeResponseDto> { new(), new() };

        _mockEdgeBusiness.Setup(b => b.GetAllEdges(UserId, OrgId, ProjectId, DataSourceId, true))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.GetAllEdges(OrgId, ProjectId, DataSourceId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllEdges_Returns200_WithEmptyList()
    {
        _mockEdgeBusiness.Setup(b => b.GetAllEdges(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _edgeController.GetAllEdges(OrgId, ProjectId, null, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<EdgeResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllEdges_ThrowsException_WhenEdgeBusinessThrows()
    {
        _mockEdgeBusiness.Setup(b => b.GetAllEdges(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.GetAllEdges(OrgId, ProjectId, null, true));
    }

    [Fact]
    public async Task GetAllEdges_PassesIdsDataSourceIdAndHideArchivedToBusinessLayer()
    {
        _mockEdgeBusiness.Setup(b => b.GetAllEdges(UserId, OrgId, ProjectId, DataSourceId, false))
                     .ReturnsAsync([]);

        await _edgeController.GetAllEdges(OrgId, ProjectId, DataSourceId, hideArchived: false);

        _mockEdgeBusiness.Verify(b => b.GetAllEdges(UserId, OrgId, ProjectId, DataSourceId, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetEdgeById Tests
    // =========================================================================

    #region GetEdgeById Tests

    [Fact]
    public async Task GetEdgeById_Returns200_WithEdge()
    {
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.GetEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null, true))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.GetEdgeById(OrgId, ProjectId, EdgeIdConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetEdgeById_ThrowsException_WhenEdgeBusinessThrows()
    {
        _mockEdgeBusiness.Setup(b => b.GetEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.GetEdgeById(OrgId, ProjectId, EdgeIdConst, true));
    }

    [Fact]
    public async Task GetEdgeById_PassesIdsToBusinessLayer()
    {
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.GetEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null, false))
                     .ReturnsAsync(expected);

        await _edgeController.GetEdgeById(OrgId, ProjectId, EdgeIdConst, hideArchived: false);

        _mockEdgeBusiness.Verify(b => b.GetEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetEdgeByRelationship Tests
    // =========================================================================

    #region GetEdgeByRelationship Tests

    [Fact]
    public async Task GetEdgeByRelationship_Returns200_WithEdge()
    {
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.GetEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId, true))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.GetEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetEdgeByRelationship_ThrowsException_WhenEdgeBusinessThrows()
    {
        _mockEdgeBusiness.Setup(b => b.GetEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.GetEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId, true));
    }

    [Fact]
    public async Task GetEdgeByRelationship_PassesOriginAndDestinationIdsToBusinessLayer()
    {
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.GetEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId, false))
                     .ReturnsAsync(expected);

        await _edgeController.GetEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId, hideArchived: false);

        _mockEdgeBusiness.Verify(b => b.GetEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // CreateEdge Tests
    // =========================================================================

    #region CreateEdge Tests

    [Fact]
    public async Task CreateEdge_Returns200_WithCreatedEdge()
    {
        var request = new CreateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.CreateEdge(UserId, OrgId, ProjectId, DataSourceId, request))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.CreateEdge(OrgId, ProjectId, DataSourceId, request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateEdge_ThrowsException_WhenEdgeBusinessThrows()
    {
        var request = new CreateEdgeRequestDto();

        _mockEdgeBusiness.Setup(b => b.CreateEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<CreateEdgeRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.CreateEdge(OrgId, ProjectId, DataSourceId, request));
    }

    [Fact]
    public async Task CreateEdge_PassesCurrentUserIdIdsAndRequestToBusinessLayer()
    {
        var request = new CreateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.CreateEdge(UserId, OrgId, ProjectId, DataSourceId, request))
                     .ReturnsAsync(expected);

        await _edgeController.CreateEdge(OrgId, ProjectId, DataSourceId, request);

        _mockEdgeBusiness.Verify(b => b.CreateEdge(UserId, OrgId, ProjectId, DataSourceId, request), Times.Once);
    }

    #endregion

    // =========================================================================
    // BulkCreateEdges Tests
    // =========================================================================

    #region BulkCreateEdges Tests

    [Fact]
    public async Task BulkCreateEdges_Returns200_WithCreatedEdges()
    {
        var request = new List<CreateEdgeRequestDto> { new(), new() };
        var expected = new List<EdgeResponseDto> { new(), new() };

        _mockEdgeBusiness.Setup(b => b.BulkCreateEdges(UserId, OrgId, ProjectId, DataSourceId, request))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.BulkCreateEdges(OrgId, ProjectId, DataSourceId, request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateEdges_Returns200_WithEmptyList()
    {
        _mockEdgeBusiness.Setup(b => b.BulkCreateEdges(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<List<CreateEdgeRequestDto>>()))
                     .ReturnsAsync([]);

        var result = (await _edgeController.BulkCreateEdges(OrgId, ProjectId, DataSourceId, [])).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<List<EdgeResponseDto>>(result.Value);
    }

    [Fact]
    public async Task BulkCreateEdges_ThrowsException_WhenEdgeBusinessThrows()
    {
        _mockEdgeBusiness.Setup(b => b.BulkCreateEdges(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<List<CreateEdgeRequestDto>>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.BulkCreateEdges(OrgId, ProjectId, DataSourceId, []));
    }

    [Fact]
    public async Task BulkCreateEdges_PassesIdsAndRequestToBusinessLayer()
    {
        var request = new List<CreateEdgeRequestDto> { new() };
        var expected = new List<EdgeResponseDto> { new() };

        _mockEdgeBusiness.Setup(b => b.BulkCreateEdges(UserId, OrgId, ProjectId, DataSourceId, request))
                     .ReturnsAsync(expected);

        await _edgeController.BulkCreateEdges(OrgId, ProjectId, DataSourceId, request);

        _mockEdgeBusiness.Verify(b => b.BulkCreateEdges(UserId, OrgId, ProjectId, DataSourceId, request), Times.Once);
    }

    #endregion

    // =========================================================================
    // UpdateEdgeById Tests
    // =========================================================================

    #region UpdateEdgeById Tests

    [Fact]
    public async Task UpdateEdgeById_Returns200_WithUpdatedEdge()
    {
        var dto = new UpdateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, EdgeIdConst, null, null))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.UpdateEdgeById(OrgId, ProjectId, EdgeIdConst, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateEdgeById_ThrowsException_WhenEdgeBusinessThrows()
    {
        var dto = new UpdateEdgeRequestDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<UpdateEdgeRequestDto>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.UpdateEdgeById(OrgId, ProjectId, EdgeIdConst, dto));
    }

    [Fact]
    public async Task UpdateEdgeById_PassesIdsAndDtoToBusinessLayer()
    {
        var dto = new UpdateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, EdgeIdConst, null, null))
                     .ReturnsAsync(expected);

        await _edgeController.UpdateEdgeById(OrgId, ProjectId, EdgeIdConst, dto);

        _mockEdgeBusiness.Verify(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, EdgeIdConst, null, null), Times.Once);
    }

    #endregion

    // =========================================================================
    // UpdateEdgeByRelationship Tests
    // =========================================================================

    #region UpdateEdgeByRelationship Tests

    [Fact]
    public async Task UpdateEdgeByRelationship_Returns200_WithUpdatedEdge()
    {
        var dto = new UpdateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, null, OriginId, DestinationId))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.UpdateEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateEdgeByRelationship_ThrowsException_WhenEdgeBusinessThrows()
    {
        var dto = new UpdateEdgeRequestDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<UpdateEdgeRequestDto>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.UpdateEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId, dto));
    }

    [Fact]
    public async Task UpdateEdgeByRelationship_PassesOriginDestinationAndDtoToBusinessLayer()
    {
        var dto = new UpdateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, null, OriginId, DestinationId))
                     .ReturnsAsync(expected);

        await _edgeController.UpdateEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId, dto);

        _mockEdgeBusiness.Verify(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, null, OriginId, DestinationId), Times.Once);
    }

    #endregion

    // =========================================================================
    // DeleteEdgeById Tests
    // =========================================================================

    #region DeleteEdgeById Tests

    [Fact]
    public async Task DeleteEdgeById_Returns200_OnSuccess()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.DeleteEdgeById(OrgId, ProjectId, EdgeIdConst) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task DeleteEdgeById_ThrowsException_WhenEdgeBusinessThrows()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.DeleteEdgeById(OrgId, ProjectId, EdgeIdConst));
    }

    [Fact]
    public async Task DeleteEdgeById_PassesCurrentUserIdAndIdsToBusinessLayer()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null))
                     .ReturnsAsync(EdgeIdConst);

        await _edgeController.DeleteEdgeById(OrgId, ProjectId, EdgeIdConst);

        _mockEdgeBusiness.Verify(b => b.DeleteEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null), Times.Once);
    }

    #endregion

    // =========================================================================
    // DeleteEdgeByRelationship Tests
    // =========================================================================

    #region DeleteEdgeByRelationship Tests

    [Fact]
    public async Task DeleteEdgeByRelationship_Returns200_OnSuccess()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.DeleteEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task DeleteEdgeByRelationship_ThrowsException_WhenEdgeBusinessThrows()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.DeleteEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId));
    }

    [Fact]
    public async Task DeleteEdgeByRelationship_PassesOriginAndDestinationIdsToBusinessLayer()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId))
                     .ReturnsAsync(EdgeIdConst);

        await _edgeController.DeleteEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId);

        _mockEdgeBusiness.Verify(b => b.DeleteEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId), Times.Once);
    }

    #endregion

    // =========================================================================
    // ArchiveEdgeById Tests
    // =========================================================================

    #region ArchiveEdgeById Tests

    [Fact]
    public async Task ArchiveEdgeById_Returns200_OnArchive()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.ArchiveEdgeById(OrgId, ProjectId, EdgeIdConst, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task ArchiveEdgeById_Returns200_OnUnarchive()
    {
        _mockEdgeBusiness.Setup(b => b.UnarchiveEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.ArchiveEdgeById(OrgId, ProjectId, EdgeIdConst, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task ArchiveEdgeById_ThrowsException_WhenEdgeBusinessThrows()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.ArchiveEdgeById(OrgId, ProjectId, EdgeIdConst, archive: true));
    }

    [Fact]
    public async Task ArchiveEdgeById_PassesIdsAndArchiveFlagToBusinessLayer()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null))
                     .ReturnsAsync(EdgeIdConst);

        await _edgeController.ArchiveEdgeById(OrgId, ProjectId, EdgeIdConst, archive: true);

        _mockEdgeBusiness.Verify(b => b.ArchiveEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null), Times.Once);
        _mockEdgeBusiness.Verify(b => b.UnarchiveEdge(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
            It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()), Times.Never);
    }

    #endregion

    // =========================================================================
    // ArchiveEdgeByRelationship Tests
    // =========================================================================

    #region ArchiveEdgeByRelationship Tests

    [Fact]
    public async Task ArchiveEdgeByRelationship_Returns200_OnArchive()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.ArchiveEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task ArchiveEdgeByRelationship_Returns200_OnUnarchive()
    {
        _mockEdgeBusiness.Setup(b => b.UnarchiveEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.ArchiveEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task ArchiveEdgeByRelationship_ThrowsException_WhenEdgeBusinessThrows()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.ArchiveEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId, archive: true));
    }

    [Fact]
    public async Task ArchiveEdgeByRelationship_PassesOriginDestinationAndArchiveFlagToBusinessLayer()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId))
                     .ReturnsAsync(EdgeIdConst);

        await _edgeController.ArchiveEdgeByRelationship(OrgId, ProjectId, OriginId, DestinationId, archive: true);

        _mockEdgeBusiness.Verify(b => b.ArchiveEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId), Times.Once);
        _mockEdgeBusiness.Verify(b => b.UnarchiveEdge(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
            It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()), Times.Never);
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void EdgeController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(EdgeController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void GetAllEdges_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.GetAllEdges),
            "organizationId", "projectId", "dataSourceId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "edge");
        AssertHasAuthAttribute(method, "read", "record");
    }

    [Fact]
    public void GetEdgeById_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.GetEdgeById),
            "organizationId", "projectId", "edgeId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "edge");
        AssertHasAuthAttribute(method, "read", "record");
    }

    [Fact]
    public void GetEdgeByRelationship_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.GetEdgeByRelationship),
            "organizationId", "projectId", "originId", "destinationId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "edge");
        AssertHasAuthAttribute(method, "read", "record");
    }

    [Fact]
    public void CreateEdge_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.CreateEdge),
            "organizationId", "projectId", "dataSourceId", "edge");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void BulkCreateEdges_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.BulkCreateEdges),
            "organizationId", "projectId", "dataSourceId", "edges");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void UpdateEdgeById_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.UpdateEdgeById),
            "organizationId", "projectId", "edgeId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void UpdateEdgeByRelationship_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.UpdateEdgeByRelationship),
            "organizationId", "projectId", "originId", "destinationId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void DeleteEdgeById_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.DeleteEdgeById),
            "organizationId", "projectId", "edgeId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void DeleteEdgeByRelationship_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.DeleteEdgeByRelationship),
            "organizationId", "projectId", "originId", "destinationId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void ArchiveEdgeById_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.ArchiveEdgeById),
            "organizationId", "projectId", "edgeId", "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void ArchiveEdgeByRelationship_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.ArchiveEdgeByRelationship),
            "organizationId", "projectId", "originId", "destinationId", "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    #endregion

    // =========================================================================
    // Helpers for Auth / Middleware Metadata Tests
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(EdgeController).GetMethods()
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
}