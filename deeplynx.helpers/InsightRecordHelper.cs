using System.Diagnostics.CodeAnalysis;
using deeplynx.datalayer.Models;

namespace deeplynx.helpers;

/// <summary>
/// Record inspecting utilities for Insight.
/// </summary>
public static class InsightRecordHelper
{
    /// <summary>
    /// File types supported by Insight.
    /// </summary>
    private static readonly string[] SupportedFileTypes =
        ["pdf", "txt", "html", "htm", "png", "jpg", "jpeg", "webp"];

    /// <summary>
    /// Filters records supported for use with Insight.
    ///
    /// Guarantees that the record's file type (or URI/name if missing) equals or ends with a valid file type.
    /// Guarantees that the record has a URI.
    ///
    /// This is a best-attempt filter. Makes no guarantee about true insight eligibility.
    /// The file data behind a record may be different than reported/detected or assigned in the URI/Name.
    /// </summary>
    /// <param name="query">The records query</param>
    /// <returns>the filtered records</returns>
    [SuppressMessage("Usage", "CA1862", Justification = "Cannot use `string.Equals` here; this is an SQL query.")]
    public static IQueryable<Record> WhereInsightEligible(this IQueryable<Record> query) =>
        query.Where(r => SupportedFileTypes.Any(ext =>
            // URI must exist (insight eligibility depends on the file existing!)
            !string.IsNullOrWhiteSpace(r.Uri) && (
                // filetype exists - extension is file type
                (!string.IsNullOrWhiteSpace(r.FileType) && r.FileType.ToLower() == ext) ||
                // filetype missing - URI or name ends with extension (may fail logically with more complex URIs)
                (string.IsNullOrWhiteSpace(r.FileType) && (r.Uri.ToLower().EndsWith("." + ext) || r.Name.ToLower().EndsWith("." + ext)))
            )));
}
