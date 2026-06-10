using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace deeplynx.helpers.ExceptionHandlers;

public static class BadRequestProblemDetailsFactory
{
    public const string ProblemType = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
    public const string ProblemTitle = "Bad Request";

    public static ValidationProblemDetails Create(string detail) =>
        Apply(new ValidationProblemDetails { Detail = detail });

    public static ValidationProblemDetails CreateForModelState(ModelStateDictionary modelState) =>
        Apply(new ValidationProblemDetails(BuildCleanedErrors(modelState))
        {
            Detail = "One or more validation errors occurred."
        });

    // System.Text.Json keys body-binding errors by JSON path ("$.labelId"); strip the JSON-path
    // syntax so clients see the plain field name. A path that cleans to empty (e.g. malformed JSON
    // reported against the document root "$") falls back to "request".
    private static Dictionary<string, string[]> BuildCleanedErrors(ModelStateDictionary modelState)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (key, entry) in modelState)
        {
            Console.WriteLine(key);
            if (entry.Errors.Count == 0)
                continue;

            var field = CleanFieldName(key);

            var messages = entry.Errors
                .Select(e => NormalizeMessage(e.ErrorMessage))
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToArray();

            if (messages.Length == 0)
                continue;

            // Merge if two raw keys clean to the same field name.
            errors[field] = errors.TryGetValue(field, out var existing)
                ? existing.Concat(messages).ToArray()
                : messages;
        }

        return errors;
    }

    private static string CleanFieldName(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "request";

        var cleaned = key
            .Replace("$.", string.Empty, StringComparison.Ordinal)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .TrimStart('.');

        return string.IsNullOrEmpty(cleaned) ? "request" : cleaned;
    }

    // STJ binding/parse failures append a diagnostic tail ("… Path: $.x | LineNumber: 0 |
    // BytePositionInString: 25.") that leaks byte offsets and internal type names. Drop it, keeping
    // the human-readable leading sentence. Converter-thrown messages (no tail) pass through unchanged.
    private static string NormalizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var pathIndex = message.IndexOf(" Path:", StringComparison.Ordinal);
        return pathIndex >= 0 ? message[..pathIndex].TrimEnd() : message;
    }

    private static ValidationProblemDetails Apply(ValidationProblemDetails problem)
    {
        problem.Type = ProblemType;
        problem.Title = ProblemTitle;
        problem.Status = StatusCodes.Status400BadRequest;
        return problem;
    }
}