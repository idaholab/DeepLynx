using deeplynx.models;
using deeplynx.models.MetricsDTOs;
using deeplynx.models.ResponseDTOs;

namespace deeplynx.interfaces;

public interface IProvenanceBusiness
{
    Task<bool> CreateProvenanceRecord(long recordId, string action, long currentUserId, long? aiConfigId);
    Task<bool> BulkCreateProvenanceRecords(List<long> recordIds, string action, long currentUserId, long? aiConfigId);
    Task<ProvenanceRecordResponseDto> GetProvenanceRecord(long recordId);
    Task<ProvenanceHistoryResponseDto> GetProvenanceHistory(long recordId);
}