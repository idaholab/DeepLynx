namespace deeplynx.models.Configuration;

public class DefaultRolePermissions
{
    public static class Admin
    {
        // project admin gets everything except for write on organization
        public static readonly Dictionary<string, string[]> AllowedPermissions = new()
        {
            { "project", new[] { "read", "write", "update" } },
            { "object_storage", new[] { "read", "write", "update" } },
            { "data_source", new[] { "read", "write", "update" } },
            { "record", new[] { "read", "write", "update" } },
            { "edge", new[] { "read", "write", "update" } },
            { "file", new[] { "read", "write", "update" } },
            { "tag", new[] { "read", "write", "update" } },
            { "class", new[] { "read", "write", "update" } },
            { "relationship", new[] { "read", "write", "update" } },
            { "user", new[] { "read", "write", "update" } },
            { "group", new[] { "read", "write", "update" } },
            { "organization", new[] { "read", "update" } },
            { "role", new[] { "read", "write", "update" } },
            { "permission", new[] { "read", "write", "update" } },
            { "sensitivity_label", new[] { "read", "write", "update" } },
        };
    }

    public static class User
    {
        // user has more limited permissions than admin
        public static readonly Dictionary<string, string[]> AllowedPermissions = new()
        {
            { "project", new[] { "read" } },
            { "object_storage", new[] { "read" } },
            { "data_source", new[] { "read", "write", "update" } },
            { "record", new[] { "read", "write", "update" } },
            { "edge", new[] { "read", "write", "update" } },
            { "file", new[] { "read", "write", "update" } },
            { "tag", new[] { "read", "write", "update" } },
            { "class", new[] { "read", "write", "update" } },
            { "relationship", new[] { "read", "write", "update" } },
            { "user", new[] { "read" } },
            { "group", new[] { "read" } },
            { "organization", new[] { "read" } },
            { "role", new[] { "read" } },
            { "permission", new[] { "read" } },
            { "sensitivity_label", new[] { "read" } },
        };
    }
}