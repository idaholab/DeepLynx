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
///     Unit tests for <see cref="RecordController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class RecordControllerTests : IDisposable
{
    private readonly Mock<IRecordBusiness> _mockBusiness;
    private readonly Mock<IGraphBusiness> _mockGraph;
    private readonly Mock<ILogger<RecordController>> _mockLogger;
    private readonly RecordController _controller;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long DataSourceId = 20L;
    private const long RecordIdConst = 7L;
    private const long ClassId = 30L;
    private const long TagId = 40L;
    private const long LabelId = 50L;
    private const long NotFoundId = 99L;

    public RecordControllerTests()
    {
        _mockBusiness = new Mock<IRecordBusiness>();
        _mockGraph = new Mock<IGraphBusiness>();
        _mockLogger = new Mock<ILogger<RecordController>>();

        _controller = new RecordController(
            _mockBusiness.Object,
            _mockGraph.Object,
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
    // GetAllRecords Tests
    // =========================================================================

    #region GetAllRecords Tests

    [Fact]
    public async Task GetAllRecords_Returns200_WithList()
    {
        var expected = new List<RecordResponseDto>
        {
            new() { Id = 1, Name = "Record 1" },
            new() { Id = 2, Name = "Record 2" }
        };
        _mockBusiness.Setup(b => b.GetAllRecords(
                         UserId, OrgId, ProjectId, null, true, null, false, false, false))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetAllRecords(OrgId, ProjectId, null, null, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllRecords_Returns200_WithEmptyList()
    {
        _mockBusiness.Setup(b => b.GetAllRecords(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<string?>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _controller.GetAllRecords(OrgId, ProjectId, null, null, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<RecordResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllRecords_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetAllRecords(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<string?>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.GetAllRecords(OrgId, ProjectId, null, null, true)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetAllRecords_PassesFiltersAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        _mockBusiness.Setup(b => b.GetAllRecords(
                         UserId, OrgId, ProjectId, DataSourceId, false, "pdf", true, false, false))
                     .ReturnsAsync([]);

        await _controller.GetAllRecords(OrgId, ProjectId, DataSourceId, "pdf", hideArchived: false);

        _mockBusiness.Verify(b => b.GetAllRecords(
            UserId, OrgId, ProjectId, DataSourceId, false, "pdf", true, false, false), Times.Once);
    }

    [Fact]
    public void GetAllRecords_HasHttpGetAndReadRecordAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.GetAllRecords),
            "organizationId",
            "projectId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record");
    }

    #endregion

    // =========================================================================
    // GetRecordsByTags Tests
    // =========================================================================

    #region GetRecordsByTags Tests

    [Fact]
    public async Task GetRecordsByTags_Returns200_WithList()
    {
        var expected = new List<RecordResponseDto> { new() { Id = 1, Name = "Tagged" } };
        _mockBusiness.Setup(b => b.GetRecordsByTags(
                         UserId, OrgId, ProjectId, It.Is<long[]>(t => t.SequenceEqual(new[] { TagId })),
                         true, false, false, false))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetRecordsByTags(OrgId, ProjectId, new[] { TagId }, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRecordsByTags_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetRecordsByTags(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.GetRecordsByTags(OrgId, ProjectId, new[] { TagId }, true)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void GetRecordsByTags_HasHttpGetAndReadRecordAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.GetRecordsByTags),
            "organizationId",
            "projectId",
            "tagIds");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record");
        AssertHasAuthAttribute(method, "read", "tag");
    }

    #endregion

    // =========================================================================
    // GetRecordsByOriginalId Tests
    // =========================================================================

    #region GetRecordsByOriginalId Tests

    [Fact]
    public async Task GetRecordsByOriginalId_Returns200_WithList()
    {
        var originalIds = new List<string> { "og-1", "og-2" };
        var expected = new List<RecordResponseDto> { new() { Id = 1, OriginalId = "og-1" } };
        _mockBusiness.Setup(b => b.GetRecordsByOriginalId(
                         UserId, OrgId, ProjectId, DataSourceId, originalIds, true, false, false, false))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetRecordsByOriginalId(
            OrgId, ProjectId, DataSourceId, originalIds, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_Returns400_OnArgumentException()
    {
        _mockBusiness.Setup(b => b.GetRecordsByOriginalId(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<List<string>>(), It.IsAny<bool>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new ArgumentException("invalid input"));

        var result = (await _controller.GetRecordsByOriginalId(
            OrgId, ProjectId, DataSourceId, new List<string> { "og-1" }, true)).Result as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_Returns404_OnKeyNotFoundException()
    {
        _mockBusiness.Setup(b => b.GetRecordsByOriginalId(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<List<string>>(), It.IsAny<bool>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new KeyNotFoundException($"DataSource {NotFoundId} not found"));

        var result = (await _controller.GetRecordsByOriginalId(
            OrgId, ProjectId, NotFoundId, new List<string> { "og-1" }, true)).Result as NotFoundObjectResult;

        Assert.NotNull(result);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetRecordsByOriginalId(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<List<string>>(), It.IsAny<bool>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.GetRecordsByOriginalId(
            OrgId, ProjectId, DataSourceId, new List<string> { "og-1" }, true)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void GetRecordsByOriginalId_HasHttpPostAndReadRecordAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.GetRecordsByOriginalId),
            "organizationId",
            "projectId",
            "dataSourceId",
            "originalIds");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "read", "record");
    }

    #endregion

    // =========================================================================
    // GetRecord Tests
    // =========================================================================

    #region GetRecord Tests

    [Fact]
    public async Task GetRecord_Returns200_WithRecord()
    {
        var expected = new RecordResponseDto { Id = RecordIdConst, Name = "Test Record" };
        _mockBusiness.Setup(b => b.GetRecord(UserId, OrgId, ProjectId, RecordIdConst, true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetRecord(OrgId, ProjectId, RecordIdConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRecord_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetRecord(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.GetRecord(OrgId, ProjectId, RecordIdConst, true)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void GetRecord_HasHttpGetAndReadRecordAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.GetRecord),
            "organizationId",
            "projectId",
            "recordId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record");
    }

    #endregion

    // =========================================================================
    // GetRecordsCountByDataSource Tests
    // =========================================================================

    #region GetRecordsCountByDataSource Tests

    [Fact]
    public async Task GetRecordsCountByDataSource_Returns200_WithCount()
    {
        _mockBusiness.Setup(b => b.GetRecordsCountByDataSource(OrgId, ProjectId, DataSourceId, true))
                     .ReturnsAsync(42);

        var result = (await _controller.GetRecordsCountByDataSource(OrgId, ProjectId, DataSourceId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task GetRecordsCountByDataSource_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetRecordsCountByDataSource(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.GetRecordsCountByDataSource(
            OrgId, ProjectId, DataSourceId, true)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void GetRecordsCountByDataSource_HasHttpGetAndReadRecordAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.GetRecordsCountByDataSource),
            "organizationId",
            "projectId",
            "dataSourceId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record");
    }

    #endregion

    // =========================================================================
    // CreateRecord Tests
    // =========================================================================

    #region CreateRecord Tests

    [Fact]
    public async Task CreateRecord_Returns200_WithRecord()
    {
        var dto = new CreateRecordRequestDto
        {
            Name = "New Record",
            Description = "Desc",
            OriginalId = "og-1",
            Properties = new System.Text.Json.Nodes.JsonObject(),
            ClassId = ClassId
        };
        var expected = new RecordResponseDto { Id = RecordIdConst, Name = "New Record" };
        _mockBusiness.Setup(b => b.CreateRecord(UserId, OrgId, ProjectId, DataSourceId, dto, null, false))
                     .ReturnsAsync(expected);

        var result = (await _controller.CreateRecord(
            OrgId, ProjectId, DataSourceId, sensitivityLabelIds: null, dto: dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateRecord_Returns500_OnUnexpectedException()
    {
        var dto = new CreateRecordRequestDto
        {
            Name = "n",
            Description = "d",
            OriginalId = "og-1",
            Properties = new System.Text.Json.Nodes.JsonObject()
        };
        _mockBusiness.Setup(b => b.CreateRecord(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<CreateRecordRequestDto>(), It.IsAny<List<long>?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.CreateRecord(
            OrgId, ProjectId, DataSourceId, sensitivityLabelIds: null, dto: dto)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateRecord_PassesCurrentUserIdToBusinessLayer()
    {
        UserContextStorage.UserId = 77L;
        var dto = new CreateRecordRequestDto
        {
            Name = "n",
            Description = "d",
            OriginalId = "og-1",
            Properties = new System.Text.Json.Nodes.JsonObject()
        };
        _mockBusiness.Setup(b => b.CreateRecord(77L, OrgId, ProjectId, DataSourceId, dto, null, false))
                     .ReturnsAsync(new RecordResponseDto { Id = 1 });

        await _controller.CreateRecord(
            OrgId, ProjectId, DataSourceId, sensitivityLabelIds: null, dto: dto);

        _mockBusiness.Verify(
            b => b.CreateRecord(77L, OrgId, ProjectId, DataSourceId, dto, null, false),
            Times.Once);
    }

    [Fact]
    public void CreateRecord_HasHttpPostAndWriteRecordAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.CreateRecord),
            "organizationId",
            "projectId",
            "dataSourceId",
            "sensitivityLabelIds",
            "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "record");
    }

    #endregion

    // =========================================================================
    // BulkCreateRecords Tests
    // =========================================================================

    #region BulkCreateRecords Tests

    [Fact]
    public async Task BulkCreateRecords_Returns200_WithRecords()
    {
        var dtos = new List<CreateRecordRequestDto>
        {
            new() { Name = "A", Description = "d", OriginalId = "og-a",
                    Properties = new System.Text.Json.Nodes.JsonObject() },
            new() { Name = "B", Description = "d", OriginalId = "og-b",
                    Properties = new System.Text.Json.Nodes.JsonObject() }
        };
        var expected = new List<RecordResponseDto>
        {
            new() { Id = 1, Name = "A" },
            new() { Id = 2, Name = "B" }
        };
        _mockBusiness.Setup(b => b.BulkCreateRecords(UserId, OrgId, ProjectId, DataSourceId, dtos, null))
                     .ReturnsAsync(expected);

        var result = (await _controller.BulkCreateRecords(
            OrgId, ProjectId, DataSourceId, records: dtos, null)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task BulkCreateRecords_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.BulkCreateRecords(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<List<CreateRecordRequestDto>>(), It.IsAny<List<long>?>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.BulkCreateRecords(
            OrgId, ProjectId, DataSourceId, records: new List<CreateRecordRequestDto>(), null)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void BulkCreateRecords_HasHttpPostAndWriteRecordAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.BulkCreateRecords),
            "organizationId",
            "projectId",
            "dataSourceId",
            "records",
            "sensitivityLabelIds");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "record");
    }

    #endregion

    // =========================================================================
    // UpdateRecord Tests
    // =========================================================================

    #region UpdateRecord Tests

    [Fact]
    public async Task UpdateRecord_Returns200_WithUpdatedRecord()
    {
        var dto = new UpdateRecordRequestDto { Name = "Updated" };
        var expected = new RecordResponseDto { Id = RecordIdConst, Name = "Updated" };
        _mockBusiness.Setup(b => b.UpdateRecord(UserId, OrgId, ProjectId, RecordIdConst, dto))
                     .ReturnsAsync(expected);

        var result = (await _controller.UpdateRecord(OrgId, ProjectId, RecordIdConst, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateRecord_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.UpdateRecord(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long>(), It.IsAny<UpdateRecordRequestDto>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _controller.UpdateRecord(
            OrgId, ProjectId, RecordIdConst, new UpdateRecordRequestDto())).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void UpdateRecord_HasHttpPutAndUpdateRecordAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.UpdateRecord),
            "organizationId",
            "projectId",
            "recordId",
            "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "record");
    }

    #endregion

    // =========================================================================
    // DeleteRecord Tests
    // =========================================================================

    #region DeleteRecord Tests

    [Fact]
    public async Task DeleteRecord_Returns200_OnSuccess()
    {
        _mockBusiness.Setup(b => b.DeleteRecord(UserId, OrgId, ProjectId, RecordIdConst))
                     .ReturnsAsync(true);

        var result = await _controller.DeleteRecord(OrgId, ProjectId, RecordIdConst) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task DeleteRecord_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.DeleteRecord(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.DeleteRecord(OrgId, ProjectId, RecordIdConst) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void DeleteRecord_HasHttpDeleteAndWriteRecordAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.DeleteRecord),
            "organizationId",
            "projectId",
            "recordId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "record");
    }

    #endregion

    // =========================================================================
    // ArchiveRecord Tests
    // =========================================================================

    #region ArchiveRecord Tests

    [Fact]
    public async Task ArchiveRecord_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.ArchiveRecord(UserId, OrgId, ProjectId, RecordIdConst))
                     .ReturnsAsync(true);

        var result = await _controller.ArchiveRecord(
            OrgId, ProjectId, RecordIdConst, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        _mockBusiness.Verify(b => b.ArchiveRecord(UserId, OrgId, ProjectId, RecordIdConst), Times.Once);
        _mockBusiness.Verify(b => b.UnarchiveRecord(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveRecord_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.UnarchiveRecord(UserId, OrgId, ProjectId, RecordIdConst))
                     .ReturnsAsync(true);

        var result = await _controller.ArchiveRecord(
            OrgId, ProjectId, RecordIdConst, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        _mockBusiness.Verify(b => b.UnarchiveRecord(UserId, OrgId, ProjectId, RecordIdConst), Times.Once);
        _mockBusiness.Verify(b => b.ArchiveRecord(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveRecord_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.ArchiveRecord(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.ArchiveRecord(
            OrgId, ProjectId, RecordIdConst, archive: true) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void ArchiveRecord_HasHttpPatchAndUpdateRecordAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.ArchiveRecord),
            "organizationId",
            "projectId",
            "recordId",
            "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "record");
    }

    #endregion

    // =========================================================================
    // AttachTag / UnattachTag Tests
    // =========================================================================

    #region AttachTag / UnattachTag Tests

    [Fact]
    public async Task AttachTag_Returns200_OnSuccess()
    {
        _mockBusiness.Setup(b => b.AttachTag(UserId, OrgId, ProjectId, RecordIdConst, TagId))
                     .ReturnsAsync(true);

        var result = await _controller.AttachTag(OrgId, ProjectId, RecordIdConst, TagId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task AttachTag_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.AttachTag(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.AttachTag(OrgId, ProjectId, RecordIdConst, TagId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UnattachTag_Returns200_OnSuccess()
    {
        _mockBusiness.Setup(b => b.UnattachTag(UserId, OrgId, ProjectId, RecordIdConst, TagId))
                     .ReturnsAsync(true);

        var result = await _controller.UnattachTag(OrgId, ProjectId, RecordIdConst, TagId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task UnattachTag_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.UnattachTag(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.UnattachTag(OrgId, ProjectId, RecordIdConst, TagId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void AttachTag_HasHttpPostAndUpdateRecordAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.AttachTag),
            "organizationId",
            "projectId",
            "recordId",
            "tagId");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "update", "record");
        AssertHasAuthAttribute(method, "read", "tag");
    }

    [Fact]
    public void UnattachTag_HasHttpDeleteAndUpdateRecordAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.UnattachTag),
            "organizationId",
            "projectId",
            "recordId",
            "tagId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "update", "record");
        AssertHasAuthAttribute(method, "read", "tag");
    }

    #endregion

    // =========================================================================
    // BulkAttachTagsToRecords / BulkUnattachTagsFromRecords Tests
    // =========================================================================

    #region BulkAttachTagsToRecords / BulkUnattachTagsFromRecords Tests

    [Fact]
    public async Task BulkAttachTagsToRecords_Returns200_OnSuccess()
    {
        var dtos = new List<RecordTagLinkDto>
        {
            new() { RecordId = RecordIdConst, TagId = TagId }
        };
        _mockBusiness.Setup(b => b.BulkAttachTags(UserId, OrgId, ProjectId, dtos))
                     .ReturnsAsync(true);

        var result = await _controller.BulkAttachTagsToRecords(OrgId, ProjectId, dtos) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task BulkAttachTagsToRecords_Returns400_OnArgumentException()
    {
        _mockBusiness.Setup(b => b.BulkAttachTags(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<List<RecordTagLinkDto>>()))
                     .ThrowsAsync(new ArgumentException("invalid input"));

        var result = await _controller.BulkAttachTagsToRecords(
            OrgId, ProjectId, new List<RecordTagLinkDto>()) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task BulkAttachTagsToRecords_Returns404_OnKeyNotFoundException()
    {
        _mockBusiness.Setup(b => b.BulkAttachTags(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<List<RecordTagLinkDto>>()))
                     .ThrowsAsync(new KeyNotFoundException("tag not found"));

        var result = await _controller.BulkAttachTagsToRecords(
            OrgId, ProjectId, new List<RecordTagLinkDto>()) as NotFoundObjectResult;

        Assert.NotNull(result);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task BulkAttachTagsToRecords_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.BulkAttachTags(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<List<RecordTagLinkDto>>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.BulkAttachTagsToRecords(
            OrgId, ProjectId, new List<RecordTagLinkDto>()) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task BulkUnattachTagsFromRecords_Returns200_OnSuccess()
    {
        var dtos = new List<RecordTagLinkDto>
        {
            new() { RecordId = RecordIdConst, TagId = TagId }
        };
        _mockBusiness.Setup(b => b.BulkUnattachTags(UserId, OrgId, ProjectId, dtos))
                     .ReturnsAsync(true);

        var result = await _controller.BulkUnattachTagsFromRecords(OrgId, ProjectId, dtos) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task BulkUnattachTagsFromRecords_Returns400_OnArgumentException()
    {
        _mockBusiness.Setup(b => b.BulkUnattachTags(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<List<RecordTagLinkDto>>()))
                     .ThrowsAsync(new ArgumentException("invalid input"));

        var result = await _controller.BulkUnattachTagsFromRecords(
            OrgId, ProjectId, new List<RecordTagLinkDto>()) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task BulkUnattachTagsFromRecords_Returns404_OnKeyNotFoundException()
    {
        _mockBusiness.Setup(b => b.BulkUnattachTags(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<List<RecordTagLinkDto>>()))
                     .ThrowsAsync(new KeyNotFoundException("tag not found"));

        var result = await _controller.BulkUnattachTagsFromRecords(
            OrgId, ProjectId, new List<RecordTagLinkDto>()) as NotFoundObjectResult;

        Assert.NotNull(result);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task BulkUnattachTagsFromRecords_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.BulkUnattachTags(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<List<RecordTagLinkDto>>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.BulkUnattachTagsFromRecords(
            OrgId, ProjectId, new List<RecordTagLinkDto>()) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void BulkAttachTagsToRecords_HasHttpPostAndUpdateRecordAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.BulkAttachTagsToRecords),
            "organizationId",
            "projectId",
            "dtos");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "update", "record");
        AssertHasAuthAttribute(method, "read", "tag");
    }

    [Fact]
    public void BulkUnattachTagsFromRecords_HasHttpPostAndUpdateRecordAndReadTagAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.BulkUnattachTagsFromRecords),
            "organizationId",
            "projectId",
            "dtos");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "update", "record");
        AssertHasAuthAttribute(method, "read", "tag");
    }

    #endregion

    // =========================================================================
    // AttachSensitivityLabel / UnattachSensitivityLabel Tests
    // =========================================================================

    #region AttachSensitivityLabel / UnattachSensitivityLabel Tests

    [Fact]
    public async Task AttachSensitivityLabel_Returns200_OnSuccess()
    {
        _mockBusiness.Setup(b => b.AttachLabel(UserId, OrgId, ProjectId, RecordIdConst, LabelId))
                     .ReturnsAsync(true);

        var result = await _controller.AttachSensitivityLabel(
            OrgId, ProjectId, RecordIdConst, LabelId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task AttachSensitivityLabel_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.AttachLabel(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.AttachSensitivityLabel(
            OrgId, ProjectId, RecordIdConst, LabelId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task BulkAttachSensitivityLabels_Returns200_OnSuccess()
    {
        var recordIds = new List<long> { RecordIdConst };
        var labelIds = new List<long> { LabelId };
        _mockBusiness.Setup(b => b.BulkAttachLabels(UserId, OrgId, ProjectId, recordIds, labelIds))
                     .ReturnsAsync(true);

        var result = await _controller.BulkAttachSensitivityLabels(
            OrgId, ProjectId, recordIds, labelIds) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task BulkAttachSensitivityLabels_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.BulkAttachLabels(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<List<long>>(), It.IsAny<List<long>>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.BulkAttachSensitivityLabels(
            OrgId, ProjectId, new List<long>(), new List<long>()) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UnattachSensitivityLabel_Returns200_OnSuccess()
    {
        _mockBusiness.Setup(b => b.UnattachLabel(UserId, OrgId, ProjectId, RecordIdConst, LabelId))
                     .ReturnsAsync(true);

        var result = await _controller.UnattachSensitivityLabel(
            OrgId, ProjectId, RecordIdConst, LabelId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task UnattachSensitivityLabel_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.UnattachLabel(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long>(), It.IsAny<long>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _controller.UnattachSensitivityLabel(
            OrgId, ProjectId, RecordIdConst, LabelId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void AttachSensitivityLabel_HasHttpPostAndUpdateRecordAndReadSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.AttachSensitivityLabel),
            "organizationId",
            "projectId",
            "recordId",
            "sensitivityLabelId");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "update", "record");
        AssertHasAuthAttribute(method, "read", "sensitivity_label");
    }

    [Fact]
    public void UnattachSensitivityLabel_HasHttpDeleteAndUpdateRecordAndReadSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.UnattachSensitivityLabel),
            "organizationId",
            "projectId",
            "recordId",
            "sensitivityLabelId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "update", "record");
        AssertHasAuthAttribute(method, "read", "sensitivity_label");
    }

    [Fact]
    public void BulkAttachSensitivityLabels_HasHttpPostAndUpdateRecordAndReadSensitivityLabelAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.BulkAttachSensitivityLabels),
            "organizationId",
            "projectId",
            "recordIds",
            "sensitivityLabelIds");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "update", "record");
        AssertHasAuthAttribute(method, "read", "sensitivity_label");
    }

    #endregion

    // =========================================================================
    // GetEdgesByRecord / GetGraphDataForRecord Tests (IGraphBusiness)
    // =========================================================================

    #region GetEdgesByRecord / GetGraphDataForRecord Tests

    [Fact]
    public async Task GetEdgesByRecord_Returns200_WithList()
    {
        var expected = new List<RelatedRecordsResponseDto> { new() };
        _mockGraph.Setup(g => g.GetEdgesByRecord(
                      UserId, OrgId, ProjectId, RecordIdConst, true, 1, 20))
                  .ReturnsAsync(expected);

        var result = (await _controller.GetEdgesByRecord(
            OrgId, ProjectId, RecordIdConst, isOrigin: true, page: 1)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetEdgesByRecord_Returns500_OnUnexpectedException()
    {
        _mockGraph.Setup(g => g.GetEdgesByRecord(
                      It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                      It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>()))
                  .ThrowsAsync(new Exception("graph error"));

        var result = (await _controller.GetEdgesByRecord(
            OrgId, ProjectId, RecordIdConst, isOrigin: true, page: 1)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetGraphDataForRecord_Returns200_WithGraph()
    {
        var expected = new GraphResponse();
        _mockGraph.Setup(g => g.GetGraphDataForRecord(OrgId, ProjectId, RecordIdConst, UserId, 2))
                  .ReturnsAsync(expected);

        var result = (await _controller.GetGraphDataForRecord(
            OrgId, ProjectId, RecordIdConst, depth: 2)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetGraphDataForRecord_Returns500_OnUnexpectedException()
    {
        _mockGraph.Setup(g => g.GetGraphDataForRecord(
                      It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                      It.IsAny<long>(), It.IsAny<int>()))
                  .ThrowsAsync(new Exception("graph error"));

        var result = (await _controller.GetGraphDataForRecord(
            OrgId, ProjectId, RecordIdConst, depth: 2)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void GetEdgesByRecord_HasHttpGetAndReadRecordAndReadEdgeAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.GetEdgesByRecord),
            "organizationId",
            "projectId",
            "recordId",
            "isOrigin",
            "page",
            "pageSize");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record");
        AssertHasAuthAttribute(method, "read", "edge");
    }

    [Fact]
    public void GetGraphDataForRecord_HasHttpGetAndReadRecordAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RecordController.GetGraphDataForRecord),
            "organizationId",
            "projectId",
            "recordId",
            "depth");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record");
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
        return Assert.Single(typeof(RecordController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }

}
