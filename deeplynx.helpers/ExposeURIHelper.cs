using deeplynx.datalayer.Models;

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
}