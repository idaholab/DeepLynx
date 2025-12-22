using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class DataSourceBusiness : IDataSourceBusiness
{
    private readonly DeeplynxContext _context;

    // dependants used to trigger downstream soft deletes
    private readonly IEdgeBusiness _edgeBusiness;
    private readonly IEventBusiness _eventBusiness;
    private readonly IRecordBusiness _recordBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataSourceBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the data source operations.</param>
    /// <param name="edgeBusiness">Passed in context for downstream edge objects.</param>
    /// <param name="recordBusiness">Passed in context for downstream record objects.</param>
    /// <param name="eventBusiness">Used for logging events during create, update, and delete Operations.</param>
    public DataSourceBusiness(
        DeeplynxContext context,
        IEdgeBusiness edgeBusiness,
        IRecordBusiness recordBusiness,
        IEventBusiness eventBusiness
    )
    {
        _context = context;
        _edgeBusiness = edgeBusiness;
        _recordBusiness = recordBusiness;
        _eventBusiness = eventBusiness;
    }

    /// <summary>
    ///     Retrieves all data sources for a specific organization or project.
    /// </summary>
    /// <param name="organizationId">The ID of the organization for which the data source belongs to</param>
    /// <param name="projectIds">ID's of the projects whose data sources are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result</param>
    /// <returns>A list of data sources within the given project.</returns>
    public async Task<List<DataSourceResponseDto>> GetAllDataSources(
        long organizationId,
        long[]? projectIds,
        bool hideArchived = true)
    {
        var dataSources = _context.DataSources
            .Where(c => c.OrganizationId == organizationId).AsQueryable();

        // Filter by projectIds if provided and not empty
        if (projectIds is { Length: > 0 })
            dataSources = dataSources.Where(c => c.ProjectId.HasValue && projectIds.Contains(c.ProjectId.Value));

        // Optionally hide archived classes
        if (hideArchived)
            dataSources = dataSources.Where(c => !c.IsArchived);

        var dataSourceList = await dataSources.ToListAsync();

        return dataSourceList.Select(d => new DataSourceResponseDto
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            OrganizationId = d.OrganizationId,
            Default = d.Default,
            Abbreviation = d.Abbreviation,
            Type = d.Type,
            BaseUri = d.BaseUri,
            Config = string.IsNullOrEmpty(d.Config)
                ? null
                : JsonNode.Parse(d.Config) as JsonObject,
            ProjectId = d.ProjectId,
            LastUpdatedAt = d.LastUpdatedAt,
            LastUpdatedBy = d.LastUpdatedBy,
            IsArchived = d.IsArchived
        }).ToList();
    }

    /// <summary>
    ///     Retrieve a specific data source by its ID.
    /// </summary>
    /// <param name="organizationId">The ID of the organization for which the data source belongs to</param>
    /// <param name="projectId">The ID of the project to which the data source belongs</param>
    /// <param name="datasourceId">The ID of the data source</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result</param>
    /// <returns>The data source in question</returns>
    /// <exception cref="KeyNotFoundException">Returned if the data source is not found or is archived</exception>
    public async Task<DataSourceResponseDto> GetDataSource(long organizationId, long? projectId,
        long datasourceId,
        bool hideArchived
    )
    {
        var query = _context.DataSources
            .Where(d => d.OrganizationId == organizationId && d.Id == datasourceId)
            .AsQueryable();

        if (projectId is not null)
            query = query.Where(d => d.ProjectId == projectId);

        var dataSource = await query.FirstOrDefaultAsync();
        if (dataSource == null)
            throw new KeyNotFoundException($"Data Source with id {datasourceId} not found");

        if (hideArchived && dataSource.IsArchived)
            throw new KeyNotFoundException($"Data Source with id {datasourceId} is archived");

        return new DataSourceResponseDto
        {
            Id = dataSource.Id,
            Name = dataSource.Name,
            Description = dataSource.Description,
            OrganizationId = dataSource.OrganizationId,
            Default = dataSource.Default,
            Abbreviation = dataSource.Abbreviation,
            Type = dataSource.Type,
            BaseUri = dataSource.BaseUri,
            Config = string.IsNullOrEmpty(dataSource.Config)
                ? null
                : JsonNode.Parse(dataSource.Config) as JsonObject,
            ProjectId = dataSource.ProjectId,
            LastUpdatedAt = dataSource.LastUpdatedAt,
            LastUpdatedBy = dataSource.LastUpdatedBy,
            IsArchived = dataSource.IsArchived
        };
    }

    /// <summary>
    ///     Retrieve a organization or project's default data source.
    /// </summary>
    /// <param name="organizationId">The ID of the organization for which the data source belongs to</param>
    /// <param name="projectId">The ID of the project to which the data source belongs</param>
    /// <returns>The data source in question</returns>
    /// <exception cref="KeyNotFoundException">Returned if the data source is not found or is archived</exception>
    public async Task<DataSourceResponseDto> GetDefaultDataSource(long organizationId, long? projectId)
    {
        var query = _context.DataSources
            .Where(d => d.OrganizationId == organizationId && d.Default == true && !d.IsArchived);

        // If projectId is provided, get project-level default
        // If projectId is null, get org-level default (where ProjectId IS NULL)
        query = projectId.HasValue
            ? query.Where(d => d.ProjectId == projectId.Value)
            : query.Where(d => d.ProjectId == null);

        var dataSource = await query.FirstOrDefaultAsync();

        if (dataSource == null)
        {
            var context = projectId.HasValue
                ? $"project {projectId}"
                : $"organization {organizationId}";
            throw new KeyNotFoundException($"Default data source for {context} not found");
        }

        return new DataSourceResponseDto
        {
            Id = dataSource.Id,
            Name = dataSource.Name,
            Description = dataSource.Description,
            Default = dataSource.Default,
            Abbreviation = dataSource.Abbreviation,
            Type = dataSource.Type,
            BaseUri = dataSource.BaseUri,
            Config = string.IsNullOrEmpty(dataSource.Config)
                ? null
                : JsonNode.Parse(dataSource.Config) as JsonObject,
            ProjectId = dataSource.ProjectId,
            LastUpdatedAt = dataSource.LastUpdatedAt,
            LastUpdatedBy = dataSource.LastUpdatedBy,
            IsArchived = dataSource.IsArchived
        };
    }

    /// <summary>
    ///     Asynchronously creates a new data source for a specified organization or project.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization for which the data source belongs to</param>
    /// <param name="projectId">The ID of the project to which the data source belongs</param>
    /// <param name="dto">The data transfer object containing data source details</param>
    /// <returns>The created data source.</returns>
    public async Task<DataSourceResponseDto> CreateDataSource(long organizationId, long? projectId, long currentUserId,
        CreateDataSourceRequestDto dto)
    {
        ValidationHelper.ValidateModel(dto);
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        var dataSource = new DataSource
        {
            Name = dto.Name,
            OrganizationId = organizationId,
            ProjectId = projectId,
            Description = dto.Description,
            Default = dto.Default,
            BaseUri = dto.BaseUri,
            Abbreviation = dto.Abbreviation,
            Config = dto.Config?.ToString(),
            Type = dto.Type,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = currentUserId,
            IsArchived = false
        };

        await _context.DataSources.AddAsync(dataSource);

        if (dto.Default)
            if (projectId.HasValue)
                await ResetProjectDefaults(projectId.Value, dataSource.Id);
            else
                await ResetOrganizationDefaults(organizationId, dataSource.Id);

        await _context.SaveChangesAsync();

        // Log DataSource Create Event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "create",
            EntityType = "data_source",
            EntityId = dataSource.Id,
            EntityName = dataSource.Name,
            DataSourceId = null,
            Properties = JsonSerializer.Serialize(new { dataSource.Name })
        });

        return new DataSourceResponseDto
        {
            Id = dataSource.Id,
            Name = dataSource.Name,
            OrganizationId = dataSource.OrganizationId,
            Description = dataSource.Description,
            Default = dataSource.Default,
            Abbreviation = dataSource.Abbreviation,
            Type = dataSource.Type,
            BaseUri = dataSource.BaseUri,
            Config = string.IsNullOrEmpty(dataSource.Config)
                ? null
                : JsonNode.Parse(dataSource.Config) as JsonObject,
            ProjectId = dataSource.ProjectId,
            LastUpdatedAt = dataSource.LastUpdatedAt,
            LastUpdatedBy = dataSource.LastUpdatedBy
        };
    }

    /// <summary>
    ///     Asynchronously updates an existing data source based on its ID.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization for which the data source belongs to</param>
    /// <param name="projectId">The ID of the project to which the data source belongs</param>
    /// <param name="dataSourceId">The ID of the existing data source to update.</param>
    /// <param name="dto">The data transfer object containing the updated data source details</param>
    /// <returns>The updated data source.</returns>
    /// <exception cref="KeyNotFoundException">Returned if data source not found</exception>
    public async Task<DataSourceResponseDto> UpdateDataSource(long organizationId,
        long? projectId,
        long currentUserId,
        long dataSourceId,
        UpdateDataSourceRequestDto dto
    )
    {
        ValidationHelper.ValidateModel(dto);

        var query = _context.DataSources
            .Where(d => d.Id == dataSourceId && d.OrganizationId == organizationId);

        if (projectId.HasValue) query = query.Where(d => d.ProjectId == projectId.Value);

        var dataSource = await query.FirstOrDefaultAsync();

        if (dataSource is null || dataSource.IsArchived)
            throw new KeyNotFoundException($"Data Source with id {dataSourceId} not found");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            dataSource.Name = dto.Name ?? dataSource.Name;
            dataSource.Description = dto.Description ?? dataSource.Description;
            dataSource.Abbreviation = dto.Abbreviation ?? dataSource.Abbreviation;
            dataSource.BaseUri = dto.BaseUri ?? dataSource.BaseUri;
            dataSource.Config = dto.Config?.ToString() ?? new JsonObject().ToString();
            dataSource.Type = dto.Type ?? dataSource.Type;
            dataSource.LastUpdatedBy = currentUserId;
            dataSource.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            await _context.SaveChangesAsync();

            await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
            {
                Operation = "update",
                EntityType = "data_source",
                EntityId = dataSource.Id,
                DataSourceId = null,
                EntityName = dataSource.Name,
                Properties = JsonSerializer.Serialize(new { dataSource.Name })
            });

            await transaction.CommitAsync();

            return new DataSourceResponseDto
            {
                Id = dataSource.Id,
                Name = dataSource.Name,
                Description = dataSource.Description,
                Default = dataSource.Default,
                Abbreviation = dataSource.Abbreviation,
                Type = dataSource.Type,
                BaseUri = dataSource.BaseUri,
                Config = string.IsNullOrEmpty(dataSource.Config)
                    ? null
                    : JsonNode.Parse(dataSource.Config) as JsonObject,
                ProjectId = dataSource.ProjectId,
                OrganizationId = dataSource.OrganizationId,
                LastUpdatedAt = dataSource.LastUpdatedAt,
                LastUpdatedBy = dataSource.LastUpdatedBy,
                IsArchived = dataSource.IsArchived
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new Exception($"Unable to update data source: {ex.Message}");
        }
    }

    /// <summary>
    ///     Deletes a specific data source by its ID.
    /// </summary>
    /// <param name="organizationId">The ID of the organization for which the data source belongs to</param>
    /// <param name="projectId">The ID of the project to which the data source belongs.</param>
    /// <param name="dataSourceId">The ID of the data source to delete</param>
    /// <returns>Boolean true on successful deletion.</returns>
    /// <exception cref="KeyNotFoundException">Returned if data source not found or if ids missing</exception>
    public async Task<bool> DeleteDataSource(long organizationId, long? projectId, long dataSourceId)
    {
        var query = _context.DataSources
            .Where(d =>
                d.Id == dataSourceId &&
                d.OrganizationId == organizationId);

        if (projectId.HasValue) query = query.Where(d => d.ProjectId == projectId.Value);

        var dataSource = await query.FirstOrDefaultAsync();

        if (dataSource == null) throw new KeyNotFoundException($"Data Source with id {dataSourceId} not found");

        _context.DataSources.Remove(dataSource);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Archives a specific data source by its ID.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization for which the data source belongs to</param>
    /// <param name="projectId">The ID of the project to which the data source belongs.</param>
    /// <param name="dataSourceId">The ID of the data source to archive</param>
    /// <returns>Boolean true on successful archival.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if data source is not found</exception>
    public async Task<bool> ArchiveDataSource(long organizationId, long? projectId, long currentUserId,
        long dataSourceId)
    {
        var query = _context.DataSources
            .Where(d =>
                d.Id == dataSourceId &&
                d.OrganizationId == organizationId &&
                (!projectId.HasValue || d.ProjectId == projectId.Value) &&
                !d.IsArchived);

        var dataSource = await query.FirstOrDefaultAsync();

        if (dataSource == null) throw new KeyNotFoundException($"Data Source with id {dataSourceId} not found");

        dataSource.IsArchived = true;
        dataSource.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        dataSource.LastUpdatedBy = currentUserId;

        await _context.SaveChangesAsync();

        // Log dataSource archive event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "archive",
            EntityType = "data_source",
            EntityId = dataSource.Id,
            DataSourceId = null,
            EntityName = dataSource.Name,
            Properties = JsonSerializer.Serialize(new { dataSource.Name })
        });

        return true;
    }

    /// <summary>
    ///     Unarchives a specific data source by its ID.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization for which the data source belongs to</param>
    /// <param name="projectId">The ID of the project to which the data source belongs.</param>
    /// <param name="dataSourceId">The ID of the data source to unarchive</param>
    /// <returns>Boolean true on successful unarchive action.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if data source is not found</exception>
    public async Task<bool> UnarchiveDataSource(long organizationId, long? projectId, long currentUserId,
        long dataSourceId)
    {
        var dataSource = await _context.DataSources
            .FirstOrDefaultAsync(d =>
                d.Id == dataSourceId &&
                d.OrganizationId == organizationId &&
                (!projectId.HasValue || d.ProjectId == projectId.Value) &&
                d.IsArchived);

        if (dataSource == null)
            throw new KeyNotFoundException($"Data Source with id {dataSourceId} not found or is not archived");

        dataSource.IsArchived = false;
        dataSource.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        dataSource.LastUpdatedBy = currentUserId;
        _context.DataSources.Update(dataSource);
        await _context.SaveChangesAsync();

        // Log dataSource unarchive event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "unarchive",
            EntityType = "data_source",
            EntityId = dataSource.Id,
            EntityName = dataSource.Name,
            DataSourceId = null,
            Properties = JsonSerializer.Serialize(new { dataSource.Name })
        });

        return true;
    }

    /// <summary>
    ///     Sets an existing data source as default for an organization or project.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization for which the data source belongs to</param>
    /// <param name="projectId">The ID of the project to which the data source belongs</param>
    /// <param name="dataSourceId">The ID of the existing data source to update.</param>
    /// <returns>The updated data source.</returns>
    /// <exception cref="KeyNotFoundException">Returned if data source not found</exception>
    public async Task<DataSourceResponseDto> SetDefaultDataSource(long organizationId, long? projectId,
        long currentUserId,
        long dataSourceId)
    {
        var dataSource = await _context.DataSources
            .FirstOrDefaultAsync(d =>
                d.Id == dataSourceId &&
                d.OrganizationId == organizationId &&
                (!projectId.HasValue || d.ProjectId == projectId.Value) &&
                !d.IsArchived);

        if (dataSource == null)
            throw new KeyNotFoundException($"Data Source with id {dataSourceId} not found");

        if (!dataSource.Default)
        {
            dataSource.Default = true;
            dataSource.LastUpdatedBy = currentUserId;
            dataSource.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // reset the defaults at the project or org level
                if (projectId.HasValue)
                    await ResetProjectDefaults(projectId.Value, dataSource.Id);
                else
                    await ResetOrganizationDefaults(organizationId, dataSource.Id);

                await _context.SaveChangesAsync();

                await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
                {
                    Operation = "update",
                    EntityType = "data_source",
                    EntityId = dataSource.Id,
                    EntityName = dataSource.Name,
                    DataSourceId = null,
                    Properties = JsonSerializer.Serialize(new { dataSource.Name })
                });
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw new Exception("Unable to set data source to default");
            }
        }

        return new DataSourceResponseDto
        {
            Id = dataSource.Id,
            Name = dataSource.Name,
            Description = dataSource.Description,
            Default = dataSource.Default,
            Abbreviation = dataSource.Abbreviation,
            Type = dataSource.Type,
            BaseUri = dataSource.BaseUri,
            Config = string.IsNullOrEmpty(dataSource.Config)
                ? null
                : JsonNode.Parse(dataSource.Config) as JsonObject,
            OrganizationId = dataSource.OrganizationId,
            ProjectId = dataSource.ProjectId,
            LastUpdatedAt = dataSource.LastUpdatedAt,
            LastUpdatedBy = dataSource.LastUpdatedBy,
            IsArchived = dataSource.IsArchived
        };
    }

    private async Task ResetProjectDefaults(long projectId, long newDefaultId)
    {
        // check for existing defaults at the project level and remove them from being default
        await _context.DataSources
            .Where(d => d.ProjectId == projectId && d.Id != newDefaultId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Default, false));
    }

    private async Task ResetOrganizationDefaults(long organizationId, long newDefaultId)
    {
        // check for existing defaults at the org level and remove them from being default
        await _context.DataSources
            .Where(d => d.OrganizationId == organizationId && d.ProjectId == null && d.Id != newDefaultId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Default, false));
    }
}