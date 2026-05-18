namespace deeplynx.models;

public class UserActivityUsersDto : UserActivityCountsDto
{
    public List<UserResponseDto> Users { get; set; } = new();
}
