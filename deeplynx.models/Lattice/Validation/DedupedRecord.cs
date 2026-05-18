using System.Text.Json.Nodes;

namespace deeplynx.models;

public record DedupedRecord(string Name, string ClassType, double Confidence, JsonObject? Attributes, int Frequency);
