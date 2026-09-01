    public enum ObjectStorageStatus
    {
        Active,
        Archived,
        Deleted
    }

    public class ObjectStorageCacheEntry
    {
        public long OrganizationId { get; set; }
        public long? ProjectId { get; set; }
        public ObjectStorageStatus Status { get; set; }
    }