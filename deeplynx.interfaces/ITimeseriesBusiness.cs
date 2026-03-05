using deeplynx.datalayer.Models;
using deeplynx.models;
using Microsoft.AspNetCore.Http;

namespace deeplynx.interfaces;

public interface ITimeseriesBusiness
{
    Task AppendTimeseriesTable(long organizationId, long projectId, long dataSourceId, IFormFile file,
        string tableName);
    
    Task<PlotDataDto> QueryTabularFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        string userQuery,
        string viewName);

    Task<RecordResponseDto> ExportTimeseriesTable(long currentUserId, long organizationId, long projectId,
        long datasourceId,
        string tableName, string fileType);

    Task<PlotDataDto> GetPlotData(long currentUserId, long organizationId, long projectId,
        long dataSourceId, long recordId, long limit, long rowNumber);

    Task<List<string>?> ExtractTabularColumns(
        ObjectStorage objectStorage,
        ObjectStorageConfigDto objectStorageConfig,
        string fileUri);
}