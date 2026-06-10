using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class CreateUserRequestDto
{
    private string? _accountType;

    public string Name { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public bool? IsArchived { get; set; } = false;
    public bool? IsActive { get; set; } = false;

    /// <summary>The type of account to create. Defaults to <see cref="AccountTypes.Default"/> when not specified.</summary>
    [AllowedValues(AccountTypes.Human, AccountTypes.Service, AccountTypes.Test, ErrorMessage = "AccountType must be 'human', 'service', or 'test'")]
    public string? AccountType
    {
        get => _accountType ?? AccountTypes.Default;
        set => _accountType = value?.ToLowerInvariant();
    }
}