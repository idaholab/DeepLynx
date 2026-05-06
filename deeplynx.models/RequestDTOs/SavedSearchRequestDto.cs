using System.Text.Json.Serialization;

namespace deeplynx.models;

public class SavedSearchRequestDtos
{
    public class FilterSavedQueryRequestDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("textSearch")]
        public string? TextSearch { get; set; }

        [JsonPropertyName("lastUpdatedBefore")]
        public DateTime? LastUpdatedBefore { get; set; }

        [JsonPropertyName("lastUpdatedAfter")]
        public DateTime? LastUpdatedAfter { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 500;
        private const int MaxPageSize = 500;

        public int GetValidatedPageSize()
        {
            if (PageSize <= 0) return 25;
            return PageSize > MaxPageSize ? MaxPageSize : PageSize;
        }
    }

    public class SavedSearchRequestDto
    {
        [JsonPropertyName("filter")]
        public CustomQueryDtos.CustomQueryRequestDto[] Filter { get; set; }

        [JsonPropertyName("textSearch")]
        public string? TextSearch { get; set; }
    }
}