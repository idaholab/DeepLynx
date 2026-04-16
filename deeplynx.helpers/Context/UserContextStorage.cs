namespace deeplynx.helpers.Context;

public static class UserContextStorage
{
    private static AsyncLocal<string> _email = new();
    private static AsyncLocal<long> _userId = new();
    private static AsyncLocal<long> _organizationId = new();
    private static AsyncLocal<string> _token = new();

    public static string Email
    {
        get => _email.Value;
        set => _email.Value = value;
    }

    public static long UserId
    {
        get => _userId.Value;
        set => _userId.Value = value;
    }

    public static long OrganizationId
    {
        get => _organizationId.Value;
        set => _organizationId.Value = value;
    }

    public static string Token
    {
        get => _token.Value;
        set => _token.Value = value;
    }
}