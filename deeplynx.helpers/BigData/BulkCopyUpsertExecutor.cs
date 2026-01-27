using deeplynx.interfaces;
using Npgsql;

namespace deeplynx.helpers.BigData;

public sealed class BulkCopyUpsertExecutor : IBulkCopyUpsertExecutor
{
    /// <summary>
    /// Performs a high-throughput bulk upsert using:
    /// 1) CREATE TEMP TABLE (staging)
    /// 2) COPY BINARY into the temp table
    /// 3) A single INSERT ... SELECT ... ON CONFLICT ... RETURNING
    /// </summary>
    /// <param name="conn">Open Npgsql connection</param>
    /// <param name="tx">Active transaction covering COPY and UPSERT</param>
    /// <param name="createTempSql">DDL to create the staging temp table (column order must match copyCommandText)</param>
    /// <param name="copyCommandText">COPY command text (e.g., 'COPY tmp_x (col1, col2, ...) FROM STDIN (FORMAT BINARY)')</param>
    /// <param name="rows">Input rows to write via COPY</param>
    /// <param name="writeRow">Delegate that writes one row to the binary importer (columns must match the COPY list)</param>
    /// <param name="upsertSql">Final INSERT ... SELECT ... ON CONFLICT ... RETURNING statement</param>
    /// <param name="mapRow">Maps one returned row from the data reader to <typeparamref name="TOut" /></param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of objects mapped from the UPSERT's RETURNING clause</returns>
    public async Task<List<TOut>> CopyUpsertAsync<TIn, TOut>(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string createTempSql,
        string copyCommandText,
        IEnumerable<TIn> rows,
        Action<NpgsqlBinaryImporter, TIn> writeRow,
        string upsertSql,
        Func<NpgsqlDataReader, TOut> mapRow,
        CancellationToken ct = default)
    {
        await using (var cmd = new NpgsqlCommand(createTempSql, conn, tx))
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var writer = conn.BeginBinaryImport(copyCommandText))
        {
            foreach (var row in rows)
            {
                await writer.StartRowAsync(ct);
                writeRow(writer, row);
            }

            await writer.CompleteAsync(ct);
        }

        var result = new List<TOut>();
        await using (var upsert = new NpgsqlCommand(upsertSql, conn, tx))
        await using (var reader = await upsert.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                result.Add(mapRow(reader));
        }

        return result;
    }

    /// <summary>
    /// Performs a high-throughput bulk insert using:
    /// 1) CREATE TEMP TABLE (staging)
    /// 2) COPY BINARY into the temp table
    /// 3) A single INSERT ... SELECT from the temp table (no RETURNING)
    /// </summary>
    /// <param name="conn">Open Npgsql connection</param>
    /// <param name="tx">Active transaction covering COPY and UPSERT</param>
    /// <param name="createTempSql">DDL to create the staging temp table (column order must match copyCommandText)</param>
    /// <param name="copyCommandText">COPY command text (e.g., 'COPY tmp_x (col1, col2, ...) FROM STDIN (FORMAT BINARY)')</param>
    /// <param name="rows">Input rows to write via COPY</param>
    /// <param name="writeRow">Delegate that writes one row to the binary importer (columns must match the COPY list)</param>
    /// <param name="insertSql">Final INSERT ... SELECT ... ON CONFLICT ... RETURNING statement</param>
    /// <param name="ct">Cancellation token</param>
    public async Task<int> CopyInsertAsync<TIn>(
        NpgsqlConnection conn, NpgsqlTransaction tx,
        string createTempSql, string copyCommandText,
        IEnumerable<TIn> rows,
        Action<NpgsqlBinaryImporter, TIn> writeRow,
        string insertSql,
        CancellationToken ct = default)
    {
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (createTempSql is null) throw new ArgumentNullException(nameof(createTempSql));
        if (copyCommandText is null) throw new ArgumentNullException(nameof(copyCommandText));
        if (rows is null) throw new ArgumentNullException(nameof(rows));
        if (writeRow is null) throw new ArgumentNullException(nameof(writeRow));
        if (insertSql is null) throw new ArgumentNullException(nameof(insertSql));

        await using (var cmd = new NpgsqlCommand(createTempSql, conn, tx))
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var writer = conn.BeginBinaryImport(copyCommandText))
        {
            foreach (var row in rows)
            {
                if (row is null) throw new ArgumentException("One or more rows were null");
                await writer.StartRowAsync(ct);
                writeRow(writer, row);
            }

            await writer.CompleteAsync(ct);
        }

        await using var insert = new NpgsqlCommand(insertSql, conn, tx);
        return await insert.ExecuteNonQueryAsync(ct);
    }
}