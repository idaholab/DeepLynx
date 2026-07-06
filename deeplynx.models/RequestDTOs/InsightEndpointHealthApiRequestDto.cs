namespace deeplynx.models;

public class InsightEndpointHealthApiRequestDto
{
    public long? ModelConfigId {get; set;}
    public string ModelType { get; set; } = string.Empty;
}