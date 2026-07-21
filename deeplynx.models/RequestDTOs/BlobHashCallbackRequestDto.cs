using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class BlobHashCallbackRequestDto
{
    [Required]
    public string ObjectStorageType { get; set; } = null!;

    [Required]
    public string ContainerName { get; set; } = null!;

    [Required]
    public string BlobName { get; set; } = null!;

    [Required]
    public string HashAlgorithm { get; set; } = null!;

    [Required]
    public string HashHex { get; set; } = null!;

    public long? ContentLength { get; set; }
}
