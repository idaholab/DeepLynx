using System.Text.RegularExpressions;

namespace deeplynx.helpers;

public class SanitizeFilePath
{
    public static bool IsValidFilePath(string filePath)
    {
        var filePathRegex = new Regex(@"^[a-zA-Z0-9/]*$");
        return filePathRegex.IsMatch(filePath);
    }
}