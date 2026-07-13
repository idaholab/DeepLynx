using System.Text.Json;
using deeplynx.models.Converters;

namespace deeplynx.tests.Converters;

public class NullableLongJsonConverterTests
{
    private readonly JsonSerializerOptions _options;

    public NullableLongJsonConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new NullableLongJsonConverter());
    }

    // Valid numeric JSON values should deserialize to nullable longs
    [Fact]
    public void Read_ReturnsLong_WhenValueIsNumber()
    {
        // Act
        var result = JsonSerializer.Deserialize<long?>("123", _options);

        // Assert
        Assert.Equal(123L, result);
    }

    // JSON null values should deserialize to null
    [Fact]
    public void Read_ReturnsNull_WhenValueIsNull()
    {
        // Act
        var result = JsonSerializer.Deserialize<long?>("null", _options);

        // Assert
        Assert.Null(result);
    }

    // Invalid string values should return the custom validation message
    [Fact]
    public void Read_ThrowsJsonException_WhenValueIsString()
    {
        // Act
        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<long?>("\"abc\"", _options));

        // Assert
        Assert.Equal("Label ID must be a valid number.", exception.Message);
    }

    // Nullable longs with values should serialize as JSON numbers
    [Fact]
    public void Write_WritesNumber_WhenValueHasValue()
    {
        // Act
        var result = JsonSerializer.Serialize<long?>(123L, _options);

        // Assert
        Assert.Equal("123", result);
    }

    // Null nullable longs should serialize as JSON null
    [Fact]
    public void Write_WritesNull_WhenValueIsNull()
    {
        // Act
        var result = JsonSerializer.Serialize<long?>(null, _options);

        // Assert
        Assert.Equal("null", result);
    }
}