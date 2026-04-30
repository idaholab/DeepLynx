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
    }

    public class SavedSearchRequestDto
    {
        [JsonPropertyName("filter")]
        public CustomQueryDtos.CustomQueryRequestDto[] Filter { get; set; }

        [JsonPropertyName("textSearch")]
        public string? TextSearch { get; set; }
    }
}