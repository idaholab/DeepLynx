using System.Text;
using deeplynx.blobhash.functions;

namespace deeplynx.tests;

public class BlobHashFunctionTests
{
    [Fact]
    public void BlobNameParser_ParsesFinalNexusBlobName()
    {
        var parser = new BlobNameParser();
        const string blobName = "organization_1/project_2/datasource_3/abc_file.pdf";

        var result = parser.ParseFinalBlobName(blobName);

        Assert.NotNull(result);
        Assert.Equal(1, result.OrganizationId);
        Assert.Equal(2, result.ProjectId);
        Assert.Equal(blobName, result.BlobName);
    }

    [Fact]
    public void BlobNameParser_ParsesCurrentAzureUpdateBlobName()
    {
        var parser = new BlobNameParser();
        const string blobName = "organization_1/projects_2/datasource_3/abc_file.pdf";

        var result = parser.ParseFinalBlobName(blobName);

        Assert.NotNull(result);
        Assert.Equal(1, result.OrganizationId);
        Assert.Equal(2, result.ProjectId);
        Assert.Equal(blobName, result.BlobName);
    }

    [Fact]
    public void BlobNameParser_SkipsTemporaryUploadBlob()
    {
        var parser = new BlobNameParser();
        const string blobName = "organization_1/project_2/datasource_3/uploads/upload-id";

        var result = parser.ParseFinalBlobName(blobName);

        Assert.Null(result);
    }

    [Fact]
    public void BlobNameParser_SkipsNonNexusBlobName()
    {
        var parser = new BlobNameParser();

        var result = parser.ParseFinalBlobName("misc/file.pdf");

        Assert.Null(result);
    }

    [Fact]
    public async Task BlobHashComputer_ComputesSha256ForOrderedStreamBytes()
    {
        var computer = new BlobHashComputer();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));

        var result = await computer.ComputeSha256HexAsync(stream, CancellationToken.None);

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result);
    }
}
