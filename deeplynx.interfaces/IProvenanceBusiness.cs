using deeplynx.models;
using deeplynx.models.MetricsDTOs;

namespace deeplynx.interfaces;

public interface IProvenanceBusiness
{
    // File Storage (GB)
    Task<bool> CreateProvenanceRecord(long recordId, string action, long currentUserId, long? aiConfigId);
}