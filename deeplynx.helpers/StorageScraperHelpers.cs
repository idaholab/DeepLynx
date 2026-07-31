namespace deeplynx.helpers;

public static class StorageScraperHelpers
{
    public static void ValidateScraperParameters(
        long objectStorageId,
        long dataSourceId,
        int batchSize,
        int maxBatches)
    {
        if (objectStorageId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(objectStorageId),
                objectStorageId,
                "Object storage ID must be greater than zero.");
        }

        if (dataSourceId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dataSourceId),
                dataSourceId,
                "Data source ID must be greater than zero.");
        }

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                "Batch size must be greater than zero.");
        }

        if (maxBatches <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBatches),
                maxBatches,
                "Maximum batches must be greater than zero.");
        }
    }

    public static List<long>? NormalizeAndValidateSensitivityLabelIds(
        List<long>? sensitivityLabelIds)
    {
        sensitivityLabelIds = sensitivityLabelIds?
            .Distinct()
            .ToList();

        if (sensitivityLabelIds?.Any(id => id <= 0) == true)
        {
            var invalidIds = sensitivityLabelIds.Where(id => id <= 0);

            throw new ArgumentException(
                $"Sensitivity label IDs must be greater than zero. " +
                $"Invalid IDs: {string.Join(", ", invalidIds)}",
                nameof(sensitivityLabelIds));
        }

        return sensitivityLabelIds;
    }
}