    public enum EntityStatus
    {
        Active,
        Archived,
        Deleted
    }

    public class EntityStatusCacheEntry
    {
        public long OrganizationId { get; set; }
        public long? ProjectId { get; set; }
        public EntityStatus Status { get; set; }
    }