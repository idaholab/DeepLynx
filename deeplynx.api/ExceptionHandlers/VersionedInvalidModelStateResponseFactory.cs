using deeplynx.helpers.ExceptionHandlers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace deeplynx.api.ExceptionHandlers;

internal static class VersionedInvalidModelStateResponseFactory
{
    private const string ProblemJsonContentType = "application/problem+json";

    public static IActionResult Create(ActionContext context)
    {
        var requestedVersion = context.HttpContext.GetRequestedApiVersion();
        ValidationProblemDetails problemDetails;

        if (requestedVersion is not null &&
            requestedVersion.MajorVersion != NexusApiVersions.V1.MajorVersion)
        {
            problemDetails = BadRequestProblemDetailsFactory.CreateForModelState(
                context.ModelState,
                context.ActionDescriptor.Parameters);
        }
        else
        {
            problemDetails = CreateLegacyProblemDetails(context);
        }

        return new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { ProblemJsonContentType }
        };
    }

    private static ValidationProblemDetails CreateLegacyProblemDetails(ActionContext context)
    {
        var problemDetailsFactory = context.HttpContext.RequestServices
            .GetRequiredService<ProblemDetailsFactory>();

        return problemDetailsFactory.CreateValidationProblemDetails(
            context.HttpContext,
            context.ModelState);
    }
}
