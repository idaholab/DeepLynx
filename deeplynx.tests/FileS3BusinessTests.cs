using System.Text;
using deeplynx.business;
using Microsoft.AspNetCore.Http;

namespace deeplynx.tests;

public class FileS3BusinessTests
{
    [Fact]
    public async Task CalculateFileContentHash_ReturnsNullPlaceholder()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("S3 placeholder"));
        var file = new FormFile(stream, 0, stream.Length, "file", "hash.txt");
        var business = new FileS3Business();

        var result = await business.CalculateFileContentHash(file);
        var storedResult = await business.CalculateStoredFileContentHash(
            "placeholder",
            new deeplynx.models.ObjectStorageConfigDto());

        Assert.Null(result);
        Assert.Null(storedResult);
    }
}
