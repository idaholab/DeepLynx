using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class UpdateFileContentHashRequestDto
{
    [Required]
    public string HashAlgorithm { get; set; } = "SHA-256";

    [Required]
    public string HashHex { get; set; } = null!;

    public long? ContentLength { get; set; }
}
