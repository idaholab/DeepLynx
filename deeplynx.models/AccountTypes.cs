namespace deeplynx.models;

/// <summary>
/// Canonical account type values for users. Use these constants instead of magic strings.
/// </summary>
public static class AccountTypes
{
    public const string Human = "human";
    public const string Service = "service";
    public const string Test = "test";

    /// <summary>The account type assigned when none is specified.</summary>
    public const string Default = Human;

    /// <summary>All valid account type values.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Human, Service, Test
    };
}
