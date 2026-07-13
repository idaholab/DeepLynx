using Apache.Arrow;
using Apache.Arrow.Flight;
using Apache.Arrow.Flight.Server;
using Grpc.Core;
using deeplynx.interfaces;
using deeplynx.models;
using Apache.Arrow.Types;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace deeplynx.api.Services;

public class NexusFlightServer : FlightServer
{
    private readonly IRecordBusiness _recordBusiness;

    public NexusFlightServer(IRecordBusiness recordBusiness)
    {
        _recordBusiness = recordBusiness;
    }

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

    public override async Task DoGet(FlightTicket ticket, FlightServerRecordBatchStreamWriter responseStream, ServerCallContext context)
    {

        var ticketBytes = ticket.Ticket.ToByteArray();
        var requestPayload = DeserializeFromBytes(ticketBytes);

        if (requestPayload.TicketName != "dev-ticket123")
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Unknown ticket"));
        }

        long organizationId = requestPayload.OrganizationId;
        long projectId = requestPayload.ProjectId;
        long currentUserId = requestPayload.CurrentUserId;

        var records = await _recordBusiness.GetAllRecords(
            currentUserId,
            organizationId,
            projectId,
            dataSourceId: null,
            hideArchived: true,
            fileType: null,
            isSysAdmin: false,
            isOrgAdmin: false,
            isProjectAdmin: false,
            isInsightEligible: false);


        var recordBatch = ConvertRecordsToRecordBatch(records);

        if (recordBatch.Schema == null || recordBatch.Schema.FieldsList.Count == 0)
        {
            Console.WriteLine("ERROR: recordBatch.Schema is null or empty!");
            throw new InvalidOperationException("Cannot send empty schema");
        }


        await responseStream.SetupStream(recordBatch.Schema);

        await responseStream.WriteAsync(recordBatch);

        Console.WriteLine("DoGet completed successfully.");

    }

    private static RecordBatch ConvertRecordsToRecordBatch(List<RecordResponseDto> records)
    {
        var schema = new Schema.Builder()
        .Field(f => f.Name("id").DataType(Int64Type.Default).Nullable(false))
        .Field(f => f.Name("uri").DataType(StringType.Default).Nullable(true))
        .Field(f => f.Name("properties").DataType(StringType.Default).Nullable(false))
        .Field(f => f.Name("original_id").DataType(StringType.Default).Nullable(false))
        .Field(f => f.Name("name").DataType(StringType.Default).Nullable(false))
        .Field(f => f.Name("class_id").DataType(Int64Type.Default).Nullable(true))
        .Field(f => f.Name("data_source_id").DataType(Int64Type.Default).Nullable(false))
        .Field(f => f.Name("project_id").DataType(Int64Type.Default).Nullable(false))
        .Field(f => f.Name("organization_id").DataType(Int64Type.Default).Nullable(false))
        .Field(f => f.Name("last_updated_at").DataType(TimestampType.Default).Nullable(false))
        .Field(f => f.Name("last_updated_by").DataType(Int64Type.Default).Nullable(true))
        .Field(f => f.Name("description").DataType(StringType.Default).Nullable(false))
        .Field(f => f.Name("object_storage_id").DataType(Int64Type.Default).Nullable(true))
        .Field(f => f.Name("is_archived").DataType(BooleanType.Default).Nullable(false))
        .Field(f => f.Name("file_type").DataType(StringType.Default).Nullable(true))
        .Field(f => f.Name("file_size").DataType(Int64Type.Default).Nullable(true))
        .Field(f => f.Name("embedded").DataType(BooleanType.Default).Nullable(false))
        .Build();

        var idArray = new Int64Array.Builder();
        var uriArray = new StringArray.Builder();
        var propertiesArray = new StringArray.Builder();
        var originalIdArray = new StringArray.Builder();
        var nameArray = new StringArray.Builder();
        var classIdArray = new Int64Array.Builder();
        var dataSourceIdArray = new Int64Array.Builder();
        var projectIdArray = new Int64Array.Builder();
        var organizationIdArray = new Int64Array.Builder();
        var lastUpdatedAtArray = new TimestampArray.Builder();
        var lastUpdatedByArray = new Int64Array.Builder();
        var descriptionArray = new StringArray.Builder();
        var objectStorageIdArray = new Int64Array.Builder();
        var isArchivedArray = new BooleanArray.Builder();
        var fileTypeArray = new StringArray.Builder();
        var fileSizeArray = new Int64Array.Builder();
        var embeddedArray = new BooleanArray.Builder();

        foreach (var record in records)
        {
            idArray.Append(record.Id);
            uriArray.Append(record.Uri ?? string.Empty);
            propertiesArray.Append(record.Properties);
            originalIdArray.Append(record.OriginalId);
            nameArray.Append(record.Name);
            classIdArray.Append(record.ClassId ?? 0);
            dataSourceIdArray.Append(record.DataSourceId);
            projectIdArray.Append(record.ProjectId);
            organizationIdArray.Append(record.OrganizationId);
            lastUpdatedAtArray.Append(new DateTimeOffset(record.LastUpdatedAt));
            lastUpdatedByArray.Append(record.LastUpdatedBy ?? 0);
            descriptionArray.Append(record.Description);
            objectStorageIdArray.Append(record.ObjectStorageId ?? 0);
            isArchivedArray.Append(record.IsArchived);
            fileTypeArray.Append(record.FileType ?? string.Empty);
            fileSizeArray.Append(record.FileSize ?? 0);
            embeddedArray.Append(record.Embedded);
        }

        IArrowArray[] arrays =
        [
            idArray.Build(),
                uriArray.Build(),
                propertiesArray.Build(),
                originalIdArray.Build(),
                nameArray.Build(),
                classIdArray.Build(),
                dataSourceIdArray.Build(),
                projectIdArray.Build(),
                organizationIdArray.Build(),
                lastUpdatedAtArray.Build(),
                lastUpdatedByArray.Build(),
                descriptionArray.Build(),
                objectStorageIdArray.Build(),
                isArchivedArray.Build(),
                fileTypeArray.Build(),
                fileSizeArray.Build(),
                embeddedArray.Build(),
        ];

        return new RecordBatch(schema, arrays, arrays[0].Length);
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

    public class TicketPayload
    {
        public long OrganizationId { get; set; }
        [Required]
        public required string TicketName { get; set; }

        public long ProjectId { get; set; }
        [Required]
        public long CurrentUserId { get; set; }
    }

    public TicketPayload DeserializeFromBytes(byte[] bytes)
    {
        var jsonString = System.Text.Encoding.UTF8.GetString(bytes);
        var payload = JsonSerializer.Deserialize<TicketPayload>(jsonString) ?? throw new InvalidOperationException("Failed to deserialize ticket payload");
        return payload;
    }
}
