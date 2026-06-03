namespace deeplynx.models;

public static class EnumExtensions
{
    public static string ToCamelCaseValue<TEnum>(this TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();

        if (string.IsNullOrEmpty(name))
            return string.Empty;

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}