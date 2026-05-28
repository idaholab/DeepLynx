namespace deeplynx.models;

public class GraphResponse
{
    public List<GraphNode>? Nodes { get; set; }
    public List<GraphLink>? Links { get; set; }
}

public class GraphNode
{
    public long Id { get; set; }
    public string Label { get; set; }
    public string Type { get; set; }
    public long? ClassId { get; set; }
    public string? ClassName { get; set; }
}

public class GraphLink
{
    public long Source { get; set; }
    public long Target { get; set; }
    public long? RelationshipId { get; set; }
    public string? RelationshipName { get; set; }
    public long EdgeId { get; set; }
}