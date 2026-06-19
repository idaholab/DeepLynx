using System.ComponentModel.DataAnnotations;
namespace deeplynx.models;

public class CreateUserRequestDto : IValidatableObject
{
    [Required]
    public string Name { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public bool? IsArchived { get; set; } = false;
    public bool? IsActive { get; set; } = false;

    /// <summary>
    /// The type of account to create. Defaults to standard.
    /// Valid values: "standard", "service", "test"
    /// </summary>
    public string AccountType { get; set; } = models.AccountType.Standard;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
            yield return new ValidationResult("Name is required.", [nameof(Name)]);

        if (AccountType == models.AccountType.Standard)
        {
            if (string.IsNullOrWhiteSpace(Email))
                yield return new ValidationResult("Email is required for standard accounts.", [nameof(Email)]);

            if (string.IsNullOrWhiteSpace(Username))
                yield return new ValidationResult("Username is required for standard accounts.", [nameof(Username)]);
        }
    }
}