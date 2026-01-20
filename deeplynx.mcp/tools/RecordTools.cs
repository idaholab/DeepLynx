using System.ComponentModel;
using System.Text.Json;
using System.Web;
using deeplynx.mcp.helpers;
using ModelContextProtocol.Server;
using System.Net.Http.Json;

namespace deeplynx.mcp.tools;

[McpServerToolType]

public class RecordTools
{
    public class RecordTag
    {
        [Description("The database ID of the tag")]
        public long Id { get; set; }

        [Description("The name of the tag")]
        public string Name { get; set; } = null!;
    }

    public class Record
    {
        [Description("The database ID of the record")]
        public long Id { get; set; }

        [Description("The name of the record")]
        public string Name { get; set; } = null!;

        [Description("The description of the record")]
        public string? Description { get; set; }

        [Description("The URI of the record")]
        public string? Uri { get; set; }

        [Description("JSON string containing record properties")]
        public string Properties { get; set; } = null!;

        [Description("The ID of the object in storage, if applicable")]
        public long? ObjectStorageId { get; set; }

        [Description("The original ID from the source system")]
        public string? OriginalId { get; set; }

        [Description("The class ID of the record")]
        public long? ClassId { get; set; }

        [Description("The ID of the data source this record belongs to")]
        public long DataSourceId { get; set; }

        [Description("The ID of the project this record belongs to")]
        public long ProjectId { get; set; }

        [Description("The ID of the organization this record belongs to")]
        public long OrganizationId { get; set; }

        [Description("The timestamp when the record was last updated")]
        public DateTime LastUpdatedAt { get; set; }

        [Description("The ID of the user who last updated the record")]
        public long? LastUpdatedBy { get; set; }

        [Description("Whether the record is archived")]
        public bool IsArchived { get; set; }

        [Description("The file type/extension if this record represents a file")]
        public string? FileType { get; set; }

        [Description("Tags associated with this record")]
        public ICollection<RecordTag>? Tags { get; set; }
    }

    public class RelatedRecord
    {
        [Description("The name of the related record")]
        public string RelatedRecordName { get; set; } = null!;

        [Description("The database ID of the related record")]
        public long RelatedRecordId { get; set; }

        [Description("The project ID of the related record")]
        public long RelatedRecordProjectId { get; set; }

        [Description("The name of the relationship between records")]
        public string? RelationshipName { get; set; }
    }

    private readonly IAuthenticatedHttpClientFactory _httpClientFactory;

    public RecordTools(IAuthenticatedHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
    
    [McpServerTool, Description("Gets all of the records that are related to the record with the ID that is provided. " +
                                "Returns related records in a stringified JSON array. ")]
    public async Task<string> GetRelatedRecords(
        [Description("The ID of the organization to which the project belongs")] long organizationId,
        [Description("The ID of the project to which the record belongs")] long projectId,
        [Description("The ID of the record we want to get related records for.")] long recordId,
        [Description("Indicates whether to find relationships where the recordId is the origin of the relationship or not")] string isOrigin,
        [Description("The results are paginated. This is the page number that the user wants")] int page,
        [Description("The page size. Default is 20.")] int pageSize = 20)
    {
        try
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["isOrigin"] = bool.Parse(isOrigin).ToString().ToLower();
            query["page"] = page.ToString();
            query["pageSize"] = pageSize.ToString();
            
            using var httpClient = await _httpClientFactory.CreateClientAsync();
            var url = $"organizations/{organizationId}/projects/{projectId}/records/{recordId}/edges?{query}";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API error {response.StatusCode}: {error}");
            }

            var content = await response.Content.ReadFromJsonAsync<List<RelatedRecord>>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            // Return exception as JSON object
            var errorResponse = new
            {
                success = false,
                error = "Exception",
                message = ex.Message,
                stackTrace = ex.StackTrace
            };
            
            return new Exception(errorResponse.ToString()).ToString();
        }
    }
    
    [McpServerTool, Description("Gets all records for a specific project, optionally filtered by datasource and file type. " +
                            "Returns records in a stringified JSON array.")]
    public async Task<string> GetAllRecords(
        [Description("The ID of the organization to which the project belongs")] long organizationId,
        [Description("The ID of the project whose records are to be retrieved")] long projectId,
        [Description("The optional ID of the datasource to filter records by")] long? dataSourceId = null,
        [Description("Indicates if user wants to hide archived records. Default is true")] string hideArchived = "true",
        [Description("Optional file extension to filter by (e.g., pdf, png, jpg)")] string? fileType = null)
    {
        try
        {
            var hideArchivedBool = string.IsNullOrEmpty(hideArchived) || bool.Parse(hideArchived);
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["hideArchived"] = hideArchivedBool.ToString().ToLower();

            if (dataSourceId.HasValue)
                query["dataSourceId"] = dataSourceId.Value.ToString();

            if (!string.IsNullOrWhiteSpace(fileType))
                query["fileType"] = fileType;

            using var httpClient = await _httpClientFactory.CreateClientAsync();
            var url = $"organizations/{organizationId}/projects/{projectId}/records?{query}";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API error {response.StatusCode}: {error}");
            }

            var content = await response.Content.ReadFromJsonAsync<List<Record>>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            // Return exception as JSON object
            var errorResponse = new
            {
                success = false,
                error = "Exception",
                message = ex.Message,
                stackTrace = ex.StackTrace
            };
            
            return JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }
    }
}