using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class CreateUserRequestDto
{
    private static readonly HashSet<string> ValidAccountTypes = ["human", "service", "test"];
    
    private string? _accountType; /// The type of account to create. Defaults to 'human' when null.

    public string Name { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public bool? IsArchived { get; set; } = false;
    public bool? IsActive { get; set; } = false;
    
    [AllowedValues("human", "service", "test", ErrorMessage = "AccountType must be 'human', 'service', or 'test'")]
    public string? AccountType
    {
        get => _accountType;
        set => _accountType = value?.ToLowerInvariant();
    }
}