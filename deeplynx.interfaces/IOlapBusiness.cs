using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.models;
using Microsoft.AspNetCore.Http;

namespace deeplynx.interfaces;

public interface IOlapBusiness
{
    Task AppendTabularBlob(
        long organizationId,
        long projectId,
        long recordId,
        long partNumber,
        IFormFile file);

    Task<PlotDataDto> QueryTabularFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        string userQuery,
        string viewName);

    Task<PlotDataDto> GetPlotData(long currentUserId, long organizationId, long projectId, long recordId, long limit,
        long rowNumber);

    Task<long> GetHighestPartNumber(
        long organizationId,
        long projectId,
        long recordId);

    Task<JsonArray?> ExtractTabularColumns(
        string objectStorageType,
        ObjectStorageConfigDto objectStorageConfig,
        string fileUri);
}