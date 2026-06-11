using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class PaginatedRequestDto
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;
    [Range(1, 500)]
    public int PageSize { get; set; } = 25;
}
