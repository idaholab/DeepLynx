using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CreateAiModelConfigDto
{
    [Required]
    [JsonPropertyName("serverUrl")] public string ServerUrl { get; set; }

    [Required]
    [JsonPropertyName("modelType")] public string ModelType { get; set; }

    [Required]
    [JsonPropertyName("modelProvider")] public string ModelProvider { get; set; }

    [Required]
    [JsonPropertyName("modelName")] public string ModelName { get; set; }

    [Required]
    [JsonPropertyName("requiresToken")] public bool RequiresToken { get; set; }

    [Required]
    [JsonPropertyName("default")] public bool Default { get; set; }
}