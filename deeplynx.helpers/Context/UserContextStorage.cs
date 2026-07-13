namespace deeplynx.helpers.Context;

public static class UserContextStorage
{
    private static AsyncLocal<string> _accountType = new();
    private static AsyncLocal<string> _email = new();
    private static AsyncLocal<long> _userId = new();
    private static AsyncLocal<long> _organizationId = new();
    private static AsyncLocal<string> _token = new();
    private static AsyncLocal<bool> _isSysAdmin = new();
    private static AsyncLocal<bool> _isOrgAdmin = new();
    private static AsyncLocal<bool> _isOrgMember = new();
    private static AsyncLocal<bool> _isProjectAdmin = new();

    public static string AccountType
    {
        get => _accountType.Value;
        set => _accountType.Value = value;
    }

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

    public static bool IsSysAdmin
    {
        get => _isSysAdmin.Value;
        set => _isSysAdmin.Value = value;
    }

    public static bool IsOrgAdmin
    {
        get => _isOrgAdmin.Value;
        set => _isOrgAdmin.Value = value;
    }

    public static bool IsProjectAdmin
    {
        get => _isProjectAdmin.Value;
        set => _isProjectAdmin.Value = value;
    }
}
