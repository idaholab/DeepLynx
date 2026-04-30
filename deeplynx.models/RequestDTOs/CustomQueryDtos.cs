using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CustomQueryDtos
{
        public class CustomQueryRequestDto
        {
                [JsonPropertyName("connector")]
                public string? Connector { get; set; }

                [JsonPropertyName("filter")]
                public string Filter { get; set; }

                [JsonPropertyName("operator")]
                public string Operator { get; set; }

                [JsonPropertyName("value")]
                public string? Value { get; set; }

                [JsonPropertyName("json")]
                public string? Json { get; set; }
        }

        public class CustomQueryResponseDto
        {
                [JsonPropertyName("textSearch")]
                public string? TextSearch { get; set; }

                [JsonPropertyName("filter")]
                public CustomQueryRequestDto[] Filter { get; set; }
        }
}