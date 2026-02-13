namespace deeplynx.interfaces;

public interface ISensitivityLabelService
{
    
    Task<List<long>> GetAuthorizedSensitivityLabels(
        long userId,
        long organizationId,
        long projectIds,
        string action);
    
    Task<List<long>> GetAuthorizedSensitivityLabels(
        long userId,
        long organizationId,
        long[] projectIds,
        string action);
    
    Task<bool> IsSensitivityLabelRequired(
        long organizationId,
        long? projectId);
    
    Task<List<long>> GetRecordSensitivityLabels(long recordId);
}