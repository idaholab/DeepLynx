using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

public class SanitizedFormFile : IFormFile
{
    private readonly IFormFile _inner;

    public SanitizedFormFile(IFormFile file)
    {
        _inner = file;
        FileName = SanitizeFileName(file.FileName);
    }

    public string FileName { get; }

    public string ContentType        => _inner.ContentType;
    public string ContentDisposition => _inner.ContentDisposition;
    public IHeaderDictionary Headers => _inner.Headers;
    public long Length               => _inner.Length;
    public string Name               => _inner.Name;

    public Stream OpenReadStream()                                           => _inner.OpenReadStream();
    public void CopyTo(Stream target)                                        => _inner.CopyTo(target);
    public Task CopyToAsync(Stream target, CancellationToken c = default)    => _inner.CopyToAsync(target, c);

    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext  = Path.GetExtension(fileName);

        name = Regex.Replace(name, @"[^\w\-]", "_");
        name = Regex.Replace(name, @"_+", "_").Trim('_');

        if (string.IsNullOrWhiteSpace(name)) name = "file";
        if (name.Length > 100) name = name[..100];

        ext = Regex.Replace(ext, @"[^\w.]", "");

        return name + ext;
    }
}