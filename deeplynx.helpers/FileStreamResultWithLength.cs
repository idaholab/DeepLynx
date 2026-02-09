using Microsoft.AspNetCore.Mvc;

namespace deeplynx.helpers;

/// <summary>
/// FileStreamResult that includes Content-Length for progress tracking
/// </summary>
public class FileStreamResultWithLength : FileStreamResult
{
    private readonly long _contentLength;

    public FileStreamResultWithLength(Stream fileStream, string contentType, long contentLength)
        : base(fileStream, contentType)
    {
        _contentLength = contentLength;
    }

    public override Task ExecuteResultAsync(ActionContext context)
    {
        context.HttpContext.Response.ContentLength = _contentLength;
        return base.ExecuteResultAsync(context);
    }
}