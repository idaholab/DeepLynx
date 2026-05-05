using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

public class OlapControllerTests
{
    [Fact]
    public async Task ExecuteOlapQuery_PassesRequestDtoToBusiness()
    {
        var request = new OlapQueryRequestDto
        {
            Limit = 5,
            Columns = ["timestamp"]
        };
        var expected = new PlotDataDto
        {
            Columns = ["timestamp"],
            Data = []
        };
        var olapBusiness = new Mock<IOlapBusiness>();
        olapBusiness
            .Setup(b => b.QueryTabularFile(123, 1, 2, 3, request, "data"))
            .ReturnsAsync(expected);

        var controller = new OlapController(olapBusiness.Object, Mock.Of<ILogger<OlapController>>());
        UserContextStorage.UserId = 123;

        try
        {
            var response = await controller.ExecuteOlapQuery(1, 2, 3, "data", request);

            var ok = Assert.IsType<OkObjectResult>(response.Result);
            Assert.Same(expected, ok.Value);
            olapBusiness.Verify(
                b => b.QueryTabularFile(123, 1, 2, 3, request, "data"),
                Times.Once);
        }
        finally
        {
            UserContextStorage.UserId = 0;
        }
    }

    [Fact]
    public async Task ExecuteOlapQuery_BusinessValidationError_ReturnsBadRequest()
    {
        var request = new OlapQueryRequestDto
        {
            StartRow = 10,
            StopRow = 1
        };
        var olapBusiness = new Mock<IOlapBusiness>();
        olapBusiness
            .Setup(b => b.QueryTabularFile(123, 1, 2, 3, request, "data"))
            .ThrowsAsync(new ArgumentException("Start row cannot be greater than stop row."));

        var controller = new OlapController(olapBusiness.Object, Mock.Of<ILogger<OlapController>>());
        UserContextStorage.UserId = 123;

        try
        {
            var response = await controller.ExecuteOlapQuery(1, 2, 3, "data", request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
            Assert.Equal("Start row cannot be greater than stop row.", badRequest.Value);
        }
        finally
        {
            UserContextStorage.UserId = 0;
        }
    }
}
