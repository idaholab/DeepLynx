using System.Text.Json.Serialization;

namespace deeplynx.models;

public class UpdateObjectStorageRequestDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = null!;

    [JsonPropertyName("default")] public bool Default { get; set; } = false;

    [JsonPropertyName("azureFilePath")]
    public string? AzureFilePath { get; set; } = null!;
}