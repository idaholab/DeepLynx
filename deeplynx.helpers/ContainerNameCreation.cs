namespace deeplynx.helpers;

public static class ContainerName
{
    /// <summary>
    ///     Create an azure-acceptable container name based on an input string
    /// </summary>
    /// <param name="inputString">The input string on which the unique name will be based</param>
    /// <returns></returns>
    public static string UniqueContainerNameFromString(string inputString)
    {
        // max length based on the azure container name constraints found at the link below
        // https://learn.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-containers--blobs--and-metadata#container-names
        const int maxContainerNameLength = 63;
        const int guidLength = 36;
        const int separatorLength = 1;
        int maxInputStringLength = maxContainerNameLength - guidLength - separatorLength;

        string truncatedInputString = inputString.Length > maxInputStringLength
            ? inputString[..maxInputStringLength]
            : inputString;

        truncatedInputString = new string(truncatedInputString
            .ToLower()
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray());

        string guid = Guid.NewGuid().ToString();

        string finalString = $"{truncatedInputString}-{guid}".ToLower().Replace("--", "-");


        return finalString;
    }
}