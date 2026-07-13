using Apache.Arrow;
using Apache.Arrow.Flight;
using Apache.Arrow.Flight.Server;
using Grpc.Core;

namespace deeplynx.api.Services;

public class NexusFlightServer : FlightServer
{
    public override async Task DoPut(
        FlightServerRecordBatchStreamReader requestStream,
        IAsyncStreamWriter<FlightPutResult> responseStream,
        ServerCallContext context)
    {
        var flightDescriptor = await requestStream.FlightDescriptor;

        var target = ResolveNexusTarget(flightDescriptor);

        try
        {
            var schema = await requestStream.Schema;

            ValidateSchema(schema);

            long rowsReceived = 0;
            int batchesReceived = 0;

            var expectedColumnCount = schema.FieldsList.Count;

            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var batch = requestStream.Current;


                if (batch is null || batch.ColumnCount != expectedColumnCount)
                {
                    throw new RpcException(new Status(
                        StatusCode.InvalidArgument,
                        $"Expected batch with {expectedColumnCount} columns, got {batch?.ColumnCount.ToString() ?? "null batch"}."));
                }


                rowsReceived += batch.Length;
                batchesReceived++;

                //temporary status hardcode for now
                string status = "not_persisted";

                var result = BuildPutAck(
                        status,
                        target,
                        rowsReceived);
                //per-batch acks are temporary until the storage/checkpointing ticket defines final ack semantics
                await responseStream.WriteAsync(result);

                //TODO: Add a durable sink for the recieved. Persistance is per batch/checkpoint. 
            }
        }
        catch (Exception ex)
        {
            //add abort and storage cleanup here if needed once storage gets hooked up.
            throw ToRpcException(ex);
        }
    }

    private static string ResolveNexusTarget(FlightDescriptor descriptor)
    {
        return string.Join(
            "/",
            descriptor.Paths.Select(path => path.Trim()));
    }

    private void ValidateSchema(Schema schema)
    {
        if (schema == null)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Schema is required"));
        }

        if (schema.FieldsList == null || schema.FieldsList.Count == 0)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Schema must contain at least one field"));
        }

        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in schema.FieldsList)
        {
            if (field == null)
            {
                throw new RpcException(
                    new Status(StatusCode.InvalidArgument, "Schema contains a null field"));
            }

            if (string.IsNullOrWhiteSpace(field.Name))
            {
                throw new RpcException(
                    new Status(StatusCode.InvalidArgument, "Schema contains a field with no name"));
            }

            var fieldName = field.Name.Trim();

            if (!fieldNames.Add(fieldName))
            {
                throw new RpcException(
                    new Status(
                        StatusCode.InvalidArgument,
                        $"Schema contains a duplicate field name: '{fieldName}'"));
            }

            if (field.DataType == null)
            {
                throw new RpcException(
                    new Status(
                        StatusCode.InvalidArgument,
                        $"Field '{fieldName}' is missing a data type"));
            }
        }
    }

    private FlightPutResult BuildPutAck(
        string status,
        string target,
        long rowsReceived)
    {
        var message =
            $"status={status}; target={target}; rowsReceived={rowsReceived}";

        return new FlightPutResult(message);
    }

    private RpcException ToRpcException(Exception exception)
    {
        if (exception is RpcException rpcException)
        {
            return rpcException;
        }

        if (exception is OperationCanceledException)
        {
            return new RpcException(
                new Status(StatusCode.Cancelled, "Upload was cancelled"));
        }

        if (exception is ArgumentException)
        {
            return new RpcException(
                new Status(StatusCode.InvalidArgument, exception.Message));
        }

        return new RpcException(
            new Status(
                StatusCode.Internal,
                $"DoPut failed: {exception.Message}"));
    }
}