using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using System.Text.Json;

namespace deeplynx.helpers;

public static class ExposeUriHelper
{
    /// Kept for historical record business.
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
    /// Creates a function to check if URI exposure is allowed for a record
    /// </summary>
    /// <param name="sensitivityLabels">The label service to obtain authorization</param>
    /// <param name="userId">The user id</param>
    /// <param name="organizationId">The organization id</param>
    /// <param name="projectIds">The project ids</param>
    /// <param name="isAdmin">User is an admin (system, organization, or project)</param>
    /// <returns>function to check URI authorization for a record</returns>
    public static async Task<Func<QueryRecord, bool>> GetQueryRecordUriExposer(
        ISensitivityLabelService sensitivityLabels,
        long userId,
        long organizationId,
        long[] projectIds,
        bool isAdmin = false)
    {
        if (isAdmin) return (record) => true;
        var authorizedDownloadLabels = await sensitivityLabels.GetAuthorizedSensitivityLabels(
                    userId, organizationId, projectIds, "download file");
        return (record) => CanExposeUri(record, authorizedDownloadLabels, isAdmin);
    }

    /// <summary>
    /// Creates a function to check if URI exposure is allowed for a record
    /// </summary>
    /// <param name="sensitivityLabels">The label service to obtain authorization</param>
    /// <param name="userId">The user id</param>
    /// <param name="organizationId">The organization id</param>
    /// <param name="projectIds">The project ids</param>
    /// <param name="isAdmin">User is an admin (system, organization, or project)</param>
    /// <returns>function to check URI authorization for a record</returns>
    public static async Task<Func<Record, bool>> GetRecordUriExposer(
        ISensitivityLabelService sensitivityLabels,
        long userId,
        long organizationId,
        long[] projectIds,
        bool isAdmin = false)
    {
        if (isAdmin) return (record) => true;
        var authorizedDownloadLabels = await sensitivityLabels.GetAuthorizedSensitivityLabels(
                    userId, organizationId, projectIds, "download file");
        return (record) => CanExposeUri(record, authorizedDownloadLabels, isAdmin);
    }

    /// <summary>
    /// Checks user's ability to view record URI
    /// </summary>
    /// <param name="record">The record to check</param>
    /// <param name="authorizedDownloadLabels">List of labels allowed for user to download</param>
    /// <param name="isAdmin">User is an admin (system, organization, or project)</param>
    /// <returns>`true` if the user is allowed to view the record</returns>
    private static bool CanExposeUri(
        Record record,
        List<long> authorizedDownloadLabels,
        bool isAdmin = false)
    {
        return isAdmin ||
               record.Labels.Count == 0 ||
               record.Labels.All(l => authorizedDownloadLabels.Contains(l.Id));
    }

    /// <summary>
    /// Checks user's ability to view record URI
    /// </summary>
    /// <param name="record">The record to check</param>
    /// <param name="authorizedDownloadLabels">List of labels allowed for user to download</param>
    /// <param name="isAdmin">User is an admin (system, organization, or project)</param>
    /// <returns>`true` if the user is allowed to view the record</returns>
    private static bool CanExposeUri(
        QueryRecord record,
        List<long> authorizedDownloadLabels,
        bool isAdmin = false)
    {
        if (isAdmin || string.IsNullOrEmpty(record.Labels))
            return true;

        try
        {
            Console.WriteLine("This is it:");
            var labels = JsonSerializer.Deserialize<List<Label>>(record.Labels, CaseInsensitive);
            if (labels is null) return true; // JSON value is explicitly "null" -  generally shouldn't happen, but that's OK
            Console.WriteLine(JsonSerializer.Serialize(labels));
            return labels.Count == 0
                || labels.All(l => authorizedDownloadLabels.Contains(l.Id));
        }
        catch (JsonException)
        {
            return false; // likely malformed JSON, don't show URI - we don't know what it's supposed to be
        }
    }

    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Helper class for deserializing query record labels.
    /// </summary>
    private class Label
    {
        public required long Id { get; init; }
        public required string Name { get; init; }
    }
}