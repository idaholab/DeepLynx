using Npgsql;

namespace deeplynx.interfaces;

public interface IBulkCopyUpsertExecutor
{
    Task<List<TOut>> CopyUpsertAsync<TIn, TOut>(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string createTempSql,
        string copyCommandText,
        IEnumerable<TIn> rows,
        Action<NpgsqlBinaryImporter, TIn> writeRow,
        string upsertSql,
        Func<NpgsqlDataReader, TOut> mapRow,
        CancellationToken ct = default);

    Task<int> CopyInsertAsync<TIn>(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string createTempSql,
        string copyCommandText,
        IEnumerable<TIn> rows,
        Action<NpgsqlBinaryImporter, TIn> writeRow,
        string insertSql,
        CancellationToken ct = default);
}