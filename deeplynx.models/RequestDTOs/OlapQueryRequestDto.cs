namespace deeplynx.models;

using System.ComponentModel.DataAnnotations;

public class OlapQueryRequestDto
{
    public const long DefaultLimit = 20;
    public const long DefaultRowStride = 1;
    public const long MaxLimit = 100000;
    public const int MaxColumnCount = 100;
    public const int MaxColumnNameLength = 128;
    public const string ColumnNamePattern = @"^[a-zA-Z_][a-zA-Z0-9_]*$";

    [MaxLength(10000)]
    public string? Query { get; set; }

    [Range(1, MaxLimit)]
    public long? Limit { get; set; }

    [Range(1, long.MaxValue)]
    public long? RowStride { get; set; }

    [Range(1, long.MaxValue)]
    public long? StartRow { get; set; }

    [Range(1, long.MaxValue)]
    public long? StopRow { get; set; }

    [MaxLength(MaxColumnCount)]
    public string[]? Columns { get; set; }
}
