using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using deeplynx.datalayer.Models;

namespace deeplynx.business;

/// <summary>
///     Builds a deterministic canonical envelope for a provenance record and hashes it, chaining
///     each record to the previous one in the same record's provenance history. This replaces
///     external signing with a tamper-evident hash chain: no keys, no external trust authority.
/// </summary>
public static class ProvenanceChainEnvelope
{
    public const int CurrentVersion = 1;

    /// <summary>
    ///     Sentinel previous-hash for the first chained provenance record of a given record_id.
    ///     Used instead of NULL so a partial unique index on (record_id, previous_hash) can catch
    ///     two concurrent writers both trying to start a fresh chain for the same record.
    /// </summary>
    public static readonly string GenesisHash = Convert.ToBase64String(new byte[32]);

    public static byte[] Build(ProvenanceRecord record, string previousHash)
    {
        using var provenance = JsonDocument.Parse(record.ProvenanceJson);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteString("provenanceId", record.ProvId);
            writer.WriteNumber("recordId", record.RecordId);
            writer.WriteNumber("historicalRecordId", record.HistoricalRecordId);
            writer.WriteNumber("organizationId", record.OrganizationId);
            writer.WriteNumber("projectId", record.ProjectId);
            if (record.FileContentHash is null)
                writer.WriteNull("fileContentHash");
            else
                writer.WriteString("fileContentHash", record.FileContentHash);
            writer.WriteString("previousHash", previousHash);
            writer.WritePropertyName("provenance");
            WriteCanonical(writer, provenance.RootElement);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static byte[] Hash(ProvenanceRecord record, string previousHash) =>
        SHA256.HashData(Build(record, previousHash));

    public static string HashBase64(ProvenanceRecord record, string previousHash) =>
        Convert.ToBase64String(Hash(record, previousHash));

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                var raw = element.GetRawText();
                string normalized;
                if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    normalized = number == 0 ? "0" : number.ToString("G29", CultureInfo.InvariantCulture);
                else
                    normalized = double.Parse(raw, CultureInfo.InvariantCulture)
                        .ToString("R", CultureInfo.InvariantCulture);
                writer.WriteRawValue(normalized, skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON token {element.ValueKind}");
        }
    }
}
