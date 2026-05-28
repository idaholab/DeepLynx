namespace deeplynx.models;

public class PlotDataDto
{
    public string[] Columns { get; set; } = [];
    public object[][] Data { get; set; }
}