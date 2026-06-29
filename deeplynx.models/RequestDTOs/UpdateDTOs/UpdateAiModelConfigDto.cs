using System.Text.Json.Serialization;

namespace deeplynx.models;

public class UpdateAiModelConfigDto
{
    [JsonPropertyName("modelName")] public string? ModelName { get; set; }

    [JsonPropertyName("modelType")] public string? ModelType { get; set; }

    [JsonPropertyName("serverUrl")] public string? ServerUrl { get; set; }

    [JsonPropertyName("requiresToken")] public bool? RequiresToken { get; set; }

    [JsonPropertyName("default")] public bool? Default { get; set; }

}