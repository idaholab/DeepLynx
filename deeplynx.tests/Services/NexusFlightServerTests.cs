using System.Reflection;
using System.Runtime.ExceptionServices;
using Apache.Arrow;
using Apache.Arrow.Flight;
using Apache.Arrow.Types;
using deeplynx.api.Services;
using Grpc.Core;

namespace deeplynx.tests.Services;

[Collection("Test Suite Collection")]

public class NexusFlightServerTests
{
    [Fact]
    public void ResolveNexusTarget_BuildsTargetFromDescriptorPath()
    {
        var descriptor = FlightDescriptor.CreatePathDescriptor(
            "deeplynx-nexus",
            "test_dataset");

        var target = InvokePrivate<string>(
            "ResolveNexusTarget",
            descriptor);

        Assert.Equal("deeplynx-nexus/test_dataset", target);
    }

    [Fact]
    public void ValidateSchema_ThrowsInvalidArgument_WhenDuplicateFieldNamesExist()
    {
        var schema = new Schema.Builder()
            .Field(f => f.Name("node_id").DataType(StringType.Default))
            .Field(f => f.Name("node_id").DataType(StringType.Default))
            .Build();

        var ex = Assert.Throws<RpcException>(() =>
            InvokePrivate("ValidateSchema", schema));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public void ValidateBatch_ThrowsInvalidArgument_WhenColumnCountDoesNotMatchSchema()
    {
        var expectedSchema = BuildNexusSchema();

        var actualSchema = new Schema.Builder()
            .Field(f => f.Name("timestamp").DataType(StringType.Default).Nullable(true))
            .Field(f => f.Name("node_id").DataType(StringType.Default).Nullable(false))
            .Field(f => f.Name("value").DataType(StringType.Default).Nullable(true))
            .Build();

        using var batch = new RecordBatch(
            actualSchema,
            new IArrowArray[]
            {
                StringColumn("2026-06-23T17:05:37Z"),
                StringColumn("v0668"),
                StringColumn("123")
            },
            1);

        var ex = Assert.Throws<RpcException>(() =>
            InvokePrivate("ValidateBatch", batch, expectedSchema));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public void BuildBatchPreview_ReturnsSchemaAndFirstRow()
    {
        var schema = BuildNexusSchema();

        using var batch = new RecordBatch(
            schema,
            new IArrowArray[]
            {
                StringColumn("2026-06-23T17:05:37Z"),
                StringColumn("v0668"),
                StringColumn("123"),
                StringColumn("Good")
            },
            1);

        var preview = InvokePrivate<(string SchemaDisplay, string FirstRowDisplay)>(
            "BuildBatchPreview",
            schema,
            batch);

        Assert.Equal(
            "timestamp: utf8, node_id: utf8, value: utf8, status: utf8",
            preview.SchemaDisplay);

        Assert.Equal(
            "timestamp=2026-06-23T17:05:37Z, node_id=v0668, value=123, status=Good",
            preview.FirstRowDisplay);
    }

    private static Schema BuildNexusSchema()
    {
        return new Schema.Builder()
            .Field(f => f.Name("timestamp").DataType(StringType.Default).Nullable(true))
            .Field(f => f.Name("node_id").DataType(StringType.Default).Nullable(false))
            .Field(f => f.Name("value").DataType(StringType.Default).Nullable(true))
            .Field(f => f.Name("status").DataType(StringType.Default).Nullable(true))
            .Build();
    }

    private static StringArray StringColumn(string value)
    {
        return new StringArray.Builder()
            .Append(value)
            .Build();
    }

    private static void InvokePrivate(string methodName, params object?[] args)
    {
        InvokePrivate<object?>(methodName, args);
    }

    private static T InvokePrivate<T>(string methodName, params object?[] args)
    {
        var method = typeof(NexusFlightServer).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        Assert.NotNull(method);

        var instance = method!.IsStatic
            ? null
            : new NexusFlightServer();

        try
        {
            return (T)method.Invoke(instance, args)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}