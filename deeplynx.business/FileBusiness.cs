using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace deeplynx.business;

public class FileBusiness
{
    private readonly IClassBusiness _classBusiness;
    private readonly DeeplynxContext _context;
    private readonly IDataSourceBusiness _dataSourceBusiness;
    private readonly IFileBusinessFactory _factory;
    private readonly IObjectStorageBusiness _objectStorageBusiness;
    private readonly IRecordBusiness _recordBusiness;

    public FileBusiness(
        DeeplynxContext context,
        IFileBusinessFactory factory,
        IObjectStorageBusiness objectStorageBusiness,
        IDataSourceBusiness dataSourceBusiness,
        IClassBusiness classBusiness,
        IRecordBusiness recordBusiness)
    {
        _context = context;
        _factory = factory;
        _objectStorageBusiness = objectStorageBusiness;
        _dataSourceBusiness = dataSourceBusiness;
        _classBusiness = classBusiness;
        _recordBusiness = recordBusiness;
    }

    /// <summary>
    ///     Uploads file using specified object storage method
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">ID of the Organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">ID of the object storage method to use</param>
    /// <param name="file">file to upload</param>
    public async Task<RecordResponseDto> UploadFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        IFormFile file)
    {
        long realDataSourceId;
        if (file == null || file.Length == 0) throw new ArgumentException("File is required and cannot be empty.");
        if (dataSourceId.HasValue)
        {
            await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId.Value, projectId);
            realDataSourceId = dataSourceId.Value;
        }
        else
        {
            var defaultDataSource = await _dataSourceBusiness.GetDefaultDataSource(organizationId, projectId) ??
                                    throw new KeyNotFoundException("Default data source not found");
            realDataSourceId = defaultDataSource.Id;
        }

        ObjectStorage? objectStorage;

        if (objectStorageId is not null)
        {
            objectStorage = await _context.ObjectStorages.FirstOrDefaultAsync(os => os.Id == objectStorageId
                && os.ProjectId == projectId
                && !os.IsArchived
            );
        }
        else
        {
            var defaultObjectStorageResponseDto = await _objectStorageBusiness.GetDefaultObjectStorage(
                organizationId, projectId);
            objectStorage = await _context.ObjectStorages.FindAsync(defaultObjectStorageResponseDto.Id);
        }

        if (objectStorage is null) throw new KeyNotFoundException("No object storage found for project");

        // Check config to confirm it is valid (could be part of the object storage fetch)
        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null");

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var guid = Guid.NewGuid();

        var uri = await fileBusiness.UploadFile(organizationId, projectId, realDataSourceId, configData, file, guid);

        var fileClass = await _classBusiness.GetOrCreateClass(currentUserId, organizationId, projectId, "File");
        var recordRequest = new CreateRecordRequestDto
        {
            Properties = new JsonObject
            {
                ["fileType"] = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
            },
            Name = file.FileName,
            ObjectStorageId = objectStorage.Id,
            Description = file.FileName,
            OriginalId = guid.ToString(),
            Uri = uri,
            ClassId = fileClass.Id,
            ClassName = fileClass.Name,
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
        };

        // return the newly created metadata record for the file
        return await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, realDataSourceId,
            recordRequest);
    }

    /// <summary>
    ///     Relaces a file but uses the same guid for the file name
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="recordId">ID of record that contains the info of the file to replace</param>
    /// <param name="file">file to update to</param>
    public async Task<RecordResponseDto> UpdateFile(long currentUserId, long organizationId, long projectId,
        long recordId, IFormFile file)
    {
        var record = await _recordBusiness.GetRecord(organizationId, projectId, recordId, true);
        if (file == null || file.Length == 0) throw new ArgumentException("File is required and cannot be empty.");

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage =
            await _objectStorageBusiness.GetObjectStorage(organizationId, projectId, record.ObjectStorageId.Value,
                true);

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var uri = await fileBusiness.UpdateFile(record, file);

        var updateRecordRequest = new UpdateRecordRequestDto
        {
            Properties = new JsonObject
            {
                ["fileType"] = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
            },
            Name = file.FileName,
            Uri = uri,
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
        };
        return await _recordBusiness.UpdateRecord(currentUserId, organizationId, projectId, recordId,
            updateRecordRequest);
    }

    /// <summary>
    ///     Downloads file
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="recordId">ID of record that contains the info of the file to download</param>
    public async Task<FileStreamResult> DownloadFile(long organizationId, long projectId, long recordId)
    {
        var record = await _recordBusiness.GetRecord(organizationId, projectId, recordId, true);
        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");
        var objectStorage =
            await _objectStorageBusiness.GetObjectStorage(organizationId, projectId, record.ObjectStorageId.Value,
                true);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        return await fileBusiness.DownloadFile(record);
    }

    /// <summary>
    ///     Deletes a file
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="recordId">ID of record that contains the info of the file to delete</param>
    public async Task<bool> DeleteFile(long currentUserId, long organizationId, long projectId, long recordId)
    {
        var record = await _recordBusiness.GetRecord(organizationId, projectId, recordId, true);
        if (record == null) throw new KeyNotFoundException("Record not found");
        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");
        var objectStorage =
            await _objectStorageBusiness.GetObjectStorage(organizationId, projectId, record.ObjectStorageId.Value,
                true);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        await fileBusiness.DeleteFile(record);
        return await _recordBusiness.DeleteRecord(currentUserId, organizationId, projectId, recordId);
    }
}