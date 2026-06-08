using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace deeplynx.api.Filters;

public class CleanPermissionValidationErrorsAttribute : ActionFilterAttribute
{
    public CleanPermissionValidationErrorsAttribute()
    {
        // Run before ApiController's automatic invalid ModelState response.
        Order = int.MinValue;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Let valid requests continue to the controller action.
        if (context.ModelState.IsValid)
            return;
        
        // Only override the response when the Permission Create request has a labelId validation/binding error.
        var hasLabelIdError = context.ModelState.Any(entry =>
            IsLabelIdField(entry.Key) &&
            entry.Value?.Errors.Count > 0);

        if (!hasLabelIdError)
            return;

        var errors = new Dictionary<string, string[]>();

        foreach (var entry in context.ModelState)
        {
            var fieldName = CleanFieldName(entry.Key);
            
            // Ignore extra framework error.
            if (fieldName.Equals("dto", StringComparison.OrdinalIgnoreCase))
                continue;
            
            // Replace labelId binding errors with a user-friendly message.
            if (fieldName.Equals("labelId", StringComparison.OrdinalIgnoreCase))
            {
                errors["labelId"] = new[] { "Label ID must be a valid numbrer." };
                continue;
            }
            
            var messages = entry.Value?.Errors
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();

            if (messages is { Length: > 0 })
                errors[fieldName] = messages;
        }
        
        // Return cleaned validation response.
        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors)
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    private static bool IsLabelIdField(string key)
    {
        return CleanFieldName(key).Equals("labelId", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanFieldName(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return key;

        return key
            .Replace("$.", string.Empty)
            .Replace("$", string.Empty)
            .TrimStart('.');
    }
}