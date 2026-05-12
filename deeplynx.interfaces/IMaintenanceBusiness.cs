namespace deeplynx.interfaces;

public interface IMaintenanceBusiness
{
    Task<List<long>> GetTimeseriesRecordIds();
    
    Task<bool> ExportDuckDbTableToFile(long recordId);
}