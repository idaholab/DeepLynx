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

            bool firstRow = true;

            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var batch = requestStream.Current;

                ValidateBatch(batch, schema);

                if (firstRow)
                {
                    var preview = BuildBatchPreview(schema, batch);

                    Console.WriteLine("Schema:");
                    Console.WriteLine(preview.SchemaDisplay);

                    Console.WriteLine("First row sample:");
                    Console.WriteLine(preview.FirstRowDisplay);

                    firstRow = false;
                }

                rowsReceived += batch.Length;
                batchesReceived++;

                //temporary status hardcode for now
                string status = "ok";

                var result = BuildPutAck(
                        status,
                        target,
                        rowsReceived);

                await responseStream.WriteAsync(result);
            }


            //commit upload to storage here
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

    private void ValidateBatch(RecordBatch batch, Schema expectedSchema)
    {
        if (batch == null)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Record batch is required"));
        }

        if (expectedSchema == null)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Expected schema is required"));
        }

        if (batch.Schema == null)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Record batch is missing a schema"));
        }

        if (batch.ColumnCount != expectedSchema.FieldsList.Count)
        {
            throw new RpcException(
                new Status(
                    StatusCode.InvalidArgument,
                    $"Record batch has {batch.ColumnCount} columns, but schema has {expectedSchema.FieldsList.Count} fields"));
        }

        for (var i = 0; i < expectedSchema.FieldsList.Count; i++)
        {
            var expectedField = expectedSchema.FieldsList[i];
            var actualField = batch.Schema.FieldsList[i];

            if (!string.Equals(actualField.Name, expectedField.Name, StringComparison.Ordinal))
            {
                throw new RpcException(
                    new Status(
                        StatusCode.InvalidArgument,
                        $"Field {i} name mismatch. Expected '{expectedField.Name}', received '{actualField.Name}'"));
            }

            if (actualField.IsNullable != expectedField.IsNullable)
            {
                throw new RpcException(
                    new Status(
                        StatusCode.InvalidArgument,
                        $"Field '{expectedField.Name}' nullability does not match"));
            }

            if (actualField.DataType == null || expectedField.DataType == null)
            {
                throw new RpcException(
                    new Status(
                        StatusCode.InvalidArgument,
                        $"Field '{expectedField.Name}' is missing a data type"));
            }

            if (actualField.DataType.TypeId != expectedField.DataType.TypeId)
            {
                throw new RpcException(
                    new Status(
                        StatusCode.InvalidArgument,
                        $"Field '{expectedField.Name}' type mismatch. Expected '{expectedField.DataType.Name}', received '{actualField.DataType.Name}'"));
            }

            if (!string.Equals(actualField.DataType.Name, expectedField.DataType.Name, StringComparison.Ordinal))
            {
                throw new RpcException(
                    new Status(
                        StatusCode.InvalidArgument,
                        $"Field '{expectedField.Name}' type name mismatch. Expected '{expectedField.DataType.Name}', received '{actualField.DataType.Name}'"));
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

    //temporary schema and first row sample
    private static (string SchemaDisplay, string FirstRowDisplay) BuildBatchPreview(
        Schema schema,
        RecordBatch batch)
    {
        string schemaDisplay = "";
        string firstRowDisplay = "";

        for (int i = 0; i < schema.FieldsList.Count; i++)
        {
            var field = schema.FieldsList[i];

            schemaDisplay += $"{field.Name}: {field.DataType.Name}";

            if (i < schema.FieldsList.Count - 1)
            {
                schemaDisplay += ", ";
            }
        }

        for (int i = 0; i < batch.ColumnCount; i++)
        {
            var field = schema.FieldsList[i];
            var column = (StringArray)batch.Arrays.ElementAt(i);
            var value = column.GetString(0) ?? "null";

            firstRowDisplay += $"{field.Name}={value}";

            if (i < batch.ColumnCount - 1)
            {
                firstRowDisplay += ", ";
            }
        }

        return (schemaDisplay, firstRowDisplay);
    }
}