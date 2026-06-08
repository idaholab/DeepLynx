using Apache.Arrow;
using Apache.Arrow.Flight;
using Apache.Arrow.Flight.Server;
using Grpc.Core;

namespace deeplynx.api.Services;

public class NexusFlightServer : FlightServer
{
    public override Task DoPut(
        FlightServerRecordBatchStreamReader requestStream,
        IAsyncStreamWriter<FlightPutResult> responseStream,
        ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "DoPut not yet implemented"));
    }
}