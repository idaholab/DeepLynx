using deeplynx.models;
using Microsoft.AspNetCore.Http;

namespace deeplynx.interfaces;

public interface ITimeseriesBusiness
{
    Task<RecordResponseDto> UploadFile(long currentUserId, long organizationId, long projectId, long datasourceId,
        IFormFile file);

    Task<string> StartUpload(long organizationId, long projectId, long datasourceId, string fileName);

    Task<string> UploadChunk(long organizationId, long projectId, long datasourceId, IFormFile chunk,
        string uploadId, int chunkNumber);

    Task<RecordResponseDto> CompleteUpload(long currentUserId, long organizationId, long projectId, long datasourceId,
        TimeseriesUploadCompleteRequestDto request);

    Task CreateTimeseriesTable(long organizationId, long projectId, long dataSourceId, string tableName,
        string fileType);

    Task AppendTimeseriesTable(long organizationId, long projectId, long dataSourceId, IFormFile file,
        string tableName);

    Task<RecordResponseDto> QueryTimeseries(long currentUserId, TimeseriesQueryRequestDto request, long projectId,
        long organizationId,
        long datasourceId, string fileType);

    Task<RecordResponseDto> InterpolateRows(long currentUserId, long organizationId, long projectId, long datasourceId,
        string rowNumber,
        string tableName, string fileType);

    Task<RecordResponseDto> ExportTimeseriesTable(long currentUserId, long organizationId, long projectId,
        long datasourceId,
        string tableName, string fileType);

    Task<PlotDataDto> GetPlotData(long currentUserId, long organizationId, long projectId,
        long dataSourceId, long recordId, long limit, long rowNumber);

    Task<Dictionary<string, object?>> GetLatestRow(long currentUserId, long organizationId, long projectId,
        long dataSourceId, long recordId);
}