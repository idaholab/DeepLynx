using deeplynx.datalayer.Models;
using System.Text.Json;

namespace deeplynx.helpers;

public static class ExposeUriHelper
{
    public static bool CanExposeUri(
        Record record,
        List<long> authorizedDownloadLabels,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false)
    {
        return isSysAdmin ||
               isOrgAdmin ||
               isProjectAdmin ||
               record.Labels.Count == 0 ||
               record.Labels.All(l => authorizedDownloadLabels.Contains(l.Id));
    }

    /// <summary>
    /// Checks user's ability to view record URI
    /// </summary>
    /// <param name="record">The record to check</param>
    /// <param name="authorizedDownloadLabels">List of labels allowed for user to download</param>
    /// <param name="isSysAdmin">User is system admin</param>
    /// <param name="isOrgAdmin">User is organization admin</param>
    /// <param name="isProjectAdmin">User is project admin</param>
    /// <returns>`true` if the user is allowed to view the record</returns>
    public static bool CanExposeUri(
        QueryRecord record,
        List<long> authorizedDownloadLabels,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false)
    {
        if (string.IsNullOrEmpty(record.Labels))
            return true; // No sensitivity labels, URI may be exposed

        try
        {
            var labels = JsonSerializer.Deserialize<List<Label>>(record.Labels);
            if (labels is null) return false;
            return isSysAdmin
                || isOrgAdmin
                || isProjectAdmin
                || labels.Count == 0
                || labels.All(l => authorizedDownloadLabels.Contains(l.id));
        }
        catch (JsonException)
        {
            return false; // likely malformed JSON, don't show URI
        }
    }

    /// <summary>
    /// Helper class for deserializing query record labels.
    /// </summary>
    private class Label
    {
        public required long id;
        public required string name;
    }
}