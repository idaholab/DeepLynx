using System.Text.Json;
using deeplynx.helpers.Json;
using deeplynx.models;

namespace deeplynx.tests;

public class UtcDateTimeJsonConverterTests
{
    private readonly JsonSerializerOptions _options = new()
    {
        Converters = { new UtcDateTimeJsonConverter() }
    };

    [Fact]
    public void Serialize_UnspecifiedDateTime_WritesUtcOffset()
    {
        var value = new DateTime(2026, 6, 8, 12, 30, 45, DateTimeKind.Unspecified);

        var json = JsonSerializer.Serialize(new { lastUpdatedAt = value }, _options);

        Assert.Contains("\"lastUpdatedAt\":\"2026-06-08T12:30:45Z\"", json);
    }

    [Fact]
    public void Serialize_UtcDateTime_WritesUtcOffset()
    {
        var value = new DateTime(2026, 6, 8, 12, 30, 45, DateTimeKind.Utc);

        var json = JsonSerializer.Serialize(new { lastUpdatedAt = value }, _options);

        Assert.Contains("\"lastUpdatedAt\":\"2026-06-08T12:30:45Z\"", json);
    }

    [Fact]
    public void Serialize_NullableDateTime_WritesUtcOffsetWhenPresent()
    {
        DateTime? value = new DateTime(2026, 6, 8, 12, 30, 45, DateTimeKind.Unspecified);

        var json = JsonSerializer.Serialize(new { lastUpdatedAt = value }, _options);

        Assert.Contains("\"lastUpdatedAt\":\"2026-06-08T12:30:45Z\"", json);
    }

    [Fact]
    public void Serialize_ResponseDtoLastUpdatedAt_WritesUtcOffset()
    {
        var dto = new OrganizationResponseDto
        {
            Id = 1,
            Name = "Contract Test",
            LastUpdatedAt = new DateTime(2026, 6, 8, 12, 30, 45, DateTimeKind.Unspecified),
            LastUpdatedBy = 2
        };

        var json = JsonSerializer.Serialize(dto, _options);

        Assert.Contains("\"LastUpdatedAt\":\"2026-06-08T12:30:45Z\"", json);
    }

    [Fact]
    public void Deserialize_DateTime_UsesSystemTextJsonParsing()
    {
        const string json = "\"2026-06-08T12:30:45Z\"";

        var value = JsonSerializer.Deserialize<DateTime>(json, _options);

        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(new DateTime(2026, 6, 8, 12, 30, 45, DateTimeKind.Utc), value);
    }
}
