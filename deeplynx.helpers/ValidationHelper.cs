using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace deeplynx.helpers;

public class ValidationHelper
{
    public static readonly List<string> AllowedEntityTypes = new List<string>
    {
        "organization",
        "group",
        "role",
        "sensitivity_label",
        "class",
        "data_source",
        "relationship",
        "project",
        "edge",
        "record",
        "record_collection",
        "metadata",
        "user",
        "tag",
        "permission",
        "oauth_application",
        "oauth_token"
    };

    public static readonly List<string> AllowedOperations = new List<string>
    {
        "create",
        "update",
        "delete",
        "archive",
        "unarchive"
    };

    public static bool ValidateTypes(string value, string type)
    {
        if (type == "EntityType")
        {
            if (!AllowedEntityTypes.Contains(value))
            {
                throw new ArgumentException($"EntityType must be one of {string.Join(", ", AllowedEntityTypes)}");
            }

            return true;
        }

        if (type == "Operation")
        {
            if (!AllowedOperations.Contains(value))
            {
                throw new ArgumentException($"Operation must be one of {string.Join(", ", AllowedOperations)}");
            }

            return true;
        }

        return false;
    }

    public static void ValidateModel<T>(T model)
    {
        var valContext = new ValidationContext(model, null, null);
        Validator.ValidateObject(model, valContext, validateAllProperties: true);
        ValidateLongProperties(model);
    }
    private static void ValidateLongProperties<T>(T model)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (property.PropertyType == typeof(long))
            {
                var value = (long)property.GetValue(model);
                if (value < 1 || value > long.MaxValue)
                {
                    throw new ValidationException($"{property.Name} must be between 1 and {long.MaxValue}.");
                }
            }
        }
    }
}
