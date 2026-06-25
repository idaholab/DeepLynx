namespace deeplynx.models;

public record DedupedEdge(
    string Subject,
    string SubjectType,
    string RelationshipType,
    string Object,
    string ObjectType,
    double Confidence,
    int Frequency,
    long? RecordId);