namespace deeplynx.models.ResponseDTOs;

public class BackfillFileSizesResponseDto
{
    public int Processed { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
}