using System.Text.Json;
using System.Text.RegularExpressions;

namespace deeplynx.helpers.json;

/// <summary>
/// Extracts and deserializes JSON from raw LLM output that may contain surrounding prose or markdown fences.
/// </summary>
public static class LlmJsonParser
{
    private static readonly JsonSerializerOptions _options = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public static T Deserialize<T>(string rawBody)
    {
        var json = ExtractJson(rawBody);
        return JsonSerializer.Deserialize<T>(json, _options)
               ?? throw new JsonException("Deserialization returned null");
    }

    /// <summary>
    /// Extracts a JSON object from raw LLM output that may contain surrounding prose or markdown.
    /// Tries markdown code fences first, then falls back to finding the outermost { } pair.
    /// </summary>
    public static string ExtractJson(string rawBody)
    {
        var trimmed = rawBody.Trim();

        // Try markdown code fence first: ```json ... ``` or ``` ... ```
        var fenced = Regex.Match(trimmed, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.Singleline);
        if (fenced.Success)
            return fenced.Groups[1].Value.Trim();

        // Fall back to outermost { } pair — handles preamble text from the LLM
        var start = trimmed.IndexOf('{');
        if (start == -1)
            throw new JsonException($"No JSON object found in LLM output. Body was: {trimmed[..Math.Min(500, trimmed.Length)]}");

        var depth = 0;
        for (var i = start; i < trimmed.Length; i++)
        {
            if (trimmed[i] == '{') depth++;
            else if (trimmed[i] == '}') depth--;
            if (depth == 0)
                return trimmed[start..(i + 1)];
        }

        throw new JsonException("Unclosed JSON object in LLM output");
    }
}
