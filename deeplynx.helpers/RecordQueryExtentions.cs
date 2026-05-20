using deeplynx.datalayer.Models;

namespace deeplynx.helpers;

public static class RecordQueryExtensions
{
    public static IQueryable<Record> WithAuthorizedLabels(
        this IQueryable<Record> query,
        List<long> authorizedLabelIds)
    {
        return query.Where(r =>
            !r.Labels.Any() ||
            r.Labels.All(label => authorizedLabelIds.Contains(label.Id)));
    }
}