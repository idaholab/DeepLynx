namespace deeplynx.models;
using System.ComponentModel.DataAnnotations;

public class OlapQueryRequestDto
{
    [Required]
    public string Query { get; set; }
}