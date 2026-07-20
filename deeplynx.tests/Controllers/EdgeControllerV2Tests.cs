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
///     Unit tests for the V2 actions of <see cref="EdgeController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class EdgeControllerTestsV2 : IDisposable
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

    public EdgeControllerTestsV2()
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
    // GetAllEdgesV2 Tests
    // =========================================================================

    #region GetAllEdgesV2 Tests

    [Fact]
    public async Task GetAllEdgesV2_Returns200_WithList()
    {
        var expected = new List<EdgeResponseDto> { new(), new() };

        _mockEdgeBusiness.Setup(b => b.GetAllEdges(UserId, OrgId, ProjectId, DataSourceId, true))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.GetAllEdgesV2(OrgId, ProjectId, DataSourceId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllEdgesV2_Returns200_WithEmptyList()
    {
        _mockEdgeBusiness.Setup(b => b.GetAllEdges(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _edgeController.GetAllEdgesV2(OrgId, ProjectId, null, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<EdgeResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllEdgesV2_Returns500_OnUnexpectedException()
    {
        _mockEdgeBusiness.Setup(b => b.GetAllEdges(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.GetAllEdgesV2(OrgId, ProjectId, null, true));
    }

    [Fact]
    public async Task GetAllEdgesV2_PassesIdsDataSourceIdAndHideArchivedToBusinessLayer()
    {
        _mockEdgeBusiness.Setup(b => b.GetAllEdges(UserId, OrgId, ProjectId, DataSourceId, false))
                     .ReturnsAsync([]);

        await _edgeController.GetAllEdgesV2(OrgId, ProjectId, DataSourceId, hideArchived: false);

        _mockEdgeBusiness.Verify(b => b.GetAllEdges(UserId, OrgId, ProjectId, DataSourceId, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetEdgeByIdV2 Tests
    // =========================================================================

    #region GetEdgeByIdV2 Tests

    [Fact]
    public async Task GetEdgeByIdV2_Returns200_WithEdge()
    {
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.GetEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null, true))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.GetEdgeByIdV2(OrgId, ProjectId, EdgeIdConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetEdgeByIdV2_Returns500_OnUnexpectedException()
    {
        _mockEdgeBusiness.Setup(b => b.GetEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.GetEdgeByIdV2(OrgId, ProjectId, EdgeIdConst, true));
    }

    [Fact]
    public async Task GetEdgeByIdV2_PassesIdsToBusinessLayer()
    {
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.GetEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null, false))
                     .ReturnsAsync(expected);

        await _edgeController.GetEdgeByIdV2(OrgId, ProjectId, EdgeIdConst, hideArchived: false);

        _mockEdgeBusiness.Verify(b => b.GetEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetEdgeByRelationshipV2 Tests
    // =========================================================================

    #region GetEdgeByRelationshipV2 Tests

    [Fact]
    public async Task GetEdgeByRelationshipV2_Returns200_WithEdge()
    {
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.GetEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId, true))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.GetEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetEdgeByRelationshipV2_Returns500_OnUnexpectedException()
    {
        _mockEdgeBusiness.Setup(b => b.GetEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.GetEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId, true));
    }

    [Fact]
    public async Task GetEdgeByRelationshipV2_PassesOriginAndDestinationIdsToBusinessLayer()
    {
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.GetEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId, false))
                     .ReturnsAsync(expected);

        await _edgeController.GetEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId, hideArchived: false);

        _mockEdgeBusiness.Verify(b => b.GetEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // CreateEdgeV2 Tests
    // =========================================================================

    #region CreateEdgeV2 Tests

    [Fact]
    public async Task CreateEdgeV2_Returns200_WithCreatedEdge()
    {
        var request = new CreateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.CreateEdge(UserId, OrgId, ProjectId, DataSourceId, request))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.CreateEdgeV2(OrgId, ProjectId, DataSourceId, request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateEdgeV2_Returns500_OnUnexpectedException()
    {
        var request = new CreateEdgeRequestDto();

        _mockEdgeBusiness.Setup(b => b.CreateEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<CreateEdgeRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.CreateEdgeV2(OrgId, ProjectId, DataSourceId, request));
    }

    [Fact]
    public async Task CreateEdgeV2_PassesCurrentUserIdIdsAndRequestToBusinessLayer()
    {
        var request = new CreateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.CreateEdge(UserId, OrgId, ProjectId, DataSourceId, request))
                     .ReturnsAsync(expected);

        await _edgeController.CreateEdgeV2(OrgId, ProjectId, DataSourceId, request);

        _mockEdgeBusiness.Verify(b => b.CreateEdge(UserId, OrgId, ProjectId, DataSourceId, request), Times.Once);
    }

    #endregion

    // =========================================================================
    // BulkCreateEdgesV2 Tests
    // =========================================================================

    #region BulkCreateEdgesV2 Tests

    [Fact]
    public async Task BulkCreateEdgesV2_Returns200_WithCreatedEdges()
    {
        var request = new List<CreateEdgeRequestDto> { new(), new() };
        var expected = new List<EdgeResponseDto> { new(), new() };

        _mockEdgeBusiness.Setup(b => b.BulkCreateEdges(UserId, OrgId, ProjectId, DataSourceId, request))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.BulkCreateEdgesV2(OrgId, ProjectId, DataSourceId, request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateEdgesV2_Returns200_WithEmptyList()
    {
        _mockEdgeBusiness.Setup(b => b.BulkCreateEdges(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<List<CreateEdgeRequestDto>>()))
                     .ReturnsAsync([]);

        var result = (await _edgeController.BulkCreateEdgesV2(OrgId, ProjectId, DataSourceId, [])).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<List<EdgeResponseDto>>(result.Value);
    }

    [Fact]
    public async Task BulkCreateEdgesV2_Returns500_OnUnexpectedException()
    {
        _mockEdgeBusiness.Setup(b => b.BulkCreateEdges(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<List<CreateEdgeRequestDto>>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.BulkCreateEdgesV2(OrgId, ProjectId, DataSourceId, []));
    }

    [Fact]
    public async Task BulkCreateEdgesV2_PassesIdsAndRequestToBusinessLayer()
    {
        var request = new List<CreateEdgeRequestDto> { new() };
        var expected = new List<EdgeResponseDto> { new() };

        _mockEdgeBusiness.Setup(b => b.BulkCreateEdges(UserId, OrgId, ProjectId, DataSourceId, request))
                     .ReturnsAsync(expected);

        await _edgeController.BulkCreateEdgesV2(OrgId, ProjectId, DataSourceId, request);

        _mockEdgeBusiness.Verify(b => b.BulkCreateEdges(UserId, OrgId, ProjectId, DataSourceId, request), Times.Once);
    }

    #endregion

    // =========================================================================
    // UpdateEdgeByIdV2 Tests
    // =========================================================================

    #region UpdateEdgeByIdV2 Tests

    [Fact]
    public async Task UpdateEdgeByIdV2_Returns200_WithUpdatedEdge()
    {
        var dto = new UpdateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, EdgeIdConst, null, null))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.UpdateEdgeByIdV2(OrgId, ProjectId, EdgeIdConst, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateEdgeByIdV2_Returns500_OnUnexpectedException()
    {
        var dto = new UpdateEdgeRequestDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<UpdateEdgeRequestDto>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.UpdateEdgeByIdV2(OrgId, ProjectId, EdgeIdConst, dto));
    }

    [Fact]
    public async Task UpdateEdgeByIdV2_PassesIdsAndDtoToBusinessLayer()
    {
        var dto = new UpdateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, EdgeIdConst, null, null))
                     .ReturnsAsync(expected);

        await _edgeController.UpdateEdgeByIdV2(OrgId, ProjectId, EdgeIdConst, dto);

        _mockEdgeBusiness.Verify(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, EdgeIdConst, null, null), Times.Once);
    }

    #endregion

    // =========================================================================
    // UpdateEdgeByRelationshipV2 Tests
    // =========================================================================

    #region UpdateEdgeByRelationshipV2 Tests

    [Fact]
    public async Task UpdateEdgeByRelationshipV2_Returns200_WithUpdatedEdge()
    {
        var dto = new UpdateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, null, OriginId, DestinationId))
                     .ReturnsAsync(expected);

        var result = (await _edgeController.UpdateEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateEdgeByRelationshipV2_Returns500_OnUnexpectedException()
    {
        var dto = new UpdateEdgeRequestDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<UpdateEdgeRequestDto>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.UpdateEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId, dto));
    }

    [Fact]
    public async Task UpdateEdgeByRelationshipV2_PassesOriginDestinationAndDtoToBusinessLayer()
    {
        var dto = new UpdateEdgeRequestDto();
        var expected = new EdgeResponseDto();

        _mockEdgeBusiness.Setup(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, null, OriginId, DestinationId))
                     .ReturnsAsync(expected);

        await _edgeController.UpdateEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId, dto);

        _mockEdgeBusiness.Verify(b => b.UpdateEdge(UserId, OrgId, ProjectId, dto, null, OriginId, DestinationId), Times.Once);
    }

    #endregion

    // =========================================================================
    // DeleteEdgeByIdV2 Tests
    // =========================================================================

    #region DeleteEdgeByIdV2 Tests

    [Fact]
    public async Task DeleteEdgeByIdV2_Returns200_OnSuccess()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.DeleteEdgeByIdV2(OrgId, ProjectId, EdgeIdConst) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task DeleteEdgeByIdV2_Returns500_OnUnexpectedException()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.DeleteEdgeByIdV2(OrgId, ProjectId, EdgeIdConst));
    }

    [Fact]
    public async Task DeleteEdgeByIdV2_PassesCurrentUserIdAndIdsToBusinessLayer()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null))
                     .ReturnsAsync(EdgeIdConst);

        await _edgeController.DeleteEdgeByIdV2(OrgId, ProjectId, EdgeIdConst);

        _mockEdgeBusiness.Verify(b => b.DeleteEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null), Times.Once);
    }

    #endregion

    // =========================================================================
    // DeleteEdgeByRelationshipV2 Tests
    // =========================================================================

    #region DeleteEdgeByRelationshipV2 Tests

    [Fact]
    public async Task DeleteEdgeByRelationshipV2_Returns200_OnSuccess()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.DeleteEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task DeleteEdgeByRelationshipV2_Returns500_OnUnexpectedException()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.DeleteEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId));
    }

    [Fact]
    public async Task DeleteEdgeByRelationshipV2_PassesOriginAndDestinationIdsToBusinessLayer()
    {
        _mockEdgeBusiness.Setup(b => b.DeleteEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId))
                     .ReturnsAsync(EdgeIdConst);

        await _edgeController.DeleteEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId);

        _mockEdgeBusiness.Verify(b => b.DeleteEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId), Times.Once);
    }

    #endregion

    // =========================================================================
    // ArchiveEdgeByIdV2 Tests
    // =========================================================================

    #region ArchiveEdgeByIdV2 Tests

    [Fact]
    public async Task ArchiveEdgeByIdV2_Returns200_OnArchive()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.ArchiveEdgeByIdV2(OrgId, ProjectId, EdgeIdConst, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task ArchiveEdgeByIdV2_Returns200_OnUnarchive()
    {
        _mockEdgeBusiness.Setup(b => b.UnarchiveEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.ArchiveEdgeByIdV2(OrgId, ProjectId, EdgeIdConst, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task ArchiveEdgeByIdV2_Returns500_OnUnexpectedException()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.ArchiveEdgeByIdV2(OrgId, ProjectId, EdgeIdConst, archive: true));
    }

    [Fact]
    public async Task ArchiveEdgeByIdV2_PassesIdsAndArchiveFlagToBusinessLayer()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null))
                     .ReturnsAsync(EdgeIdConst);

        await _edgeController.ArchiveEdgeByIdV2(OrgId, ProjectId, EdgeIdConst, archive: true);

        _mockEdgeBusiness.Verify(b => b.ArchiveEdge(UserId, OrgId, ProjectId, EdgeIdConst, null, null), Times.Once);
        _mockEdgeBusiness.Verify(b => b.UnarchiveEdge(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
            It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()), Times.Never);
    }

    #endregion

    // =========================================================================
    // ArchiveEdgeByRelationshipV2 Tests
    // =========================================================================

    #region ArchiveEdgeByRelationshipV2 Tests

    [Fact]
    public async Task ArchiveEdgeByRelationshipV2_Returns200_OnArchive()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.ArchiveEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task ArchiveEdgeByRelationshipV2_Returns200_OnUnarchive()
    {
        _mockEdgeBusiness.Setup(b => b.UnarchiveEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId))
                     .ReturnsAsync(EdgeIdConst);

        var result = await _edgeController.ArchiveEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(EdgeIdConst, result.Value);
    }

    [Fact]
    public async Task ArchiveEdgeByRelationshipV2_Returns500_OnUnexpectedException()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _edgeController.ArchiveEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId, archive: true));
    }

    [Fact]
    public async Task ArchiveEdgeByRelationshipV2_PassesOriginDestinationAndArchiveFlagToBusinessLayer()
    {
        _mockEdgeBusiness.Setup(b => b.ArchiveEdge(UserId, OrgId, ProjectId, null, OriginId, DestinationId))
                     .ReturnsAsync(EdgeIdConst);

        await _edgeController.ArchiveEdgeByRelationshipV2(OrgId, ProjectId, OriginId, DestinationId, archive: true);

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
    public void GetAllEdgesV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.GetAllEdgesV2),
            "organizationId", "projectId", "dataSourceId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "edge");
        AssertHasAuthAttribute(method, "read", "record");
    }

    [Fact]
    public void GetEdgeByIdV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.GetEdgeByIdV2),
            "organizationId", "projectId", "edgeId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "edge");
        AssertHasAuthAttribute(method, "read", "record");
    }

    [Fact]
    public void GetEdgeByRelationshipV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.GetEdgeByRelationshipV2),
            "organizationId", "projectId", "originId", "destinationId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "edge");
        AssertHasAuthAttribute(method, "read", "record");
    }

    [Fact]
    public void CreateEdgeV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.CreateEdgeV2),
            "organizationId", "projectId", "dataSourceId", "edge");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void BulkCreateEdgesV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.BulkCreateEdgesV2),
            "organizationId", "projectId", "dataSourceId", "edges");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void UpdateEdgeByIdV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.UpdateEdgeByIdV2),
            "organizationId", "projectId", "edgeId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void UpdateEdgeByRelationshipV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.UpdateEdgeByRelationshipV2),
            "organizationId", "projectId", "originId", "destinationId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void DeleteEdgeByIdV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.DeleteEdgeByIdV2),
            "organizationId", "projectId", "edgeId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void DeleteEdgeByRelationshipV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.DeleteEdgeByRelationshipV2),
            "organizationId", "projectId", "originId", "destinationId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void ArchiveEdgeByIdV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.ArchiveEdgeByIdV2),
            "organizationId", "projectId", "edgeId", "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "edge");
        AssertHasAuthAttribute(method, "update", "record");
    }

    [Fact]
    public void ArchiveEdgeByRelationshipV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(EdgeController.ArchiveEdgeByRelationshipV2),
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