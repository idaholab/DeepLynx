using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CreateObjectStorageRequestDto
{
    [Required][JsonPropertyName("name")] public string Name { get; set; }

    [Required]
    [JsonPropertyName("config")]
    public ObjectStorageConfigDto Config { get; set; } = null!;

    [DefaultValue(false)]
    [JsonPropertyName("default")] public bool Default { get; set; }
}

public class ObjectStorageConfigDto
{
    public string? MountPath { get; set; }

    [JsonPropertyName("AzureObjectConfig")]
    [DefaultValue(null)]
    public AzureObjectConfigDto? AzureObjectConfig { get; set; }

    public string? AwsConnectionString { get; set; }
}

public class AzureObjectConfigDto
{
    public string AzureConnectionString { get; set; } = null!;
    public string? AzureContainerName { get; set; }
    public string? AzureFilePath { get; set; }
    public bool ExistingContainer { get; set; }
}