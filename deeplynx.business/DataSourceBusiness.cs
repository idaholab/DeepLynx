using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Cache;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

public class DataSourceBusiness : IDataSourceBusiness
{
    private readonly DeeplynxContext _context;

    // dependants used to trigger downstream soft deletes
    private readonly IEdgeBusiness _edgeBusiness;
    private readonly IProjectRolePermissionService _projectRolePermissionService;
    private readonly IAdminService _adminService;
    private readonly IEventBusiness _eventBusiness;
    private readonly IRecordBusiness _recordBusiness;
    private readonly ILogger<DataSourceBusiness>? _logger;
    private static readonly TimeSpan DataSourceCacheTtl = TimeSpan.FromHours(1);

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataSourceBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the data source operations.</param>
    /// <param name="edgeBusiness">Passed in context for downstream edge objects.</param>
    /// <param name="recordBusiness">Passed in context for downstream record objects.</param>
    /// <param name="eventBusiness">Used for logging events during create, update, and delete Operations.</param>
    /// <param name="projectRolePermissionService">Used to get permissions allowed for a user</param>
    /// <param name="adminService">Used to check level the user is</param>
    /// <param name="logger">Optional logger used for cache read/write/invalidation warnings.</param>
    public DataSourceBusiness(
        DeeplynxContext context,
        IEdgeBusiness edgeBusiness,
        IRecordBusiness recordBusiness,
        IEventBusiness eventBusiness,
        IProjectRolePermissionService projectRolePermissionService,
        IAdminService adminService,
        ILogger<DataSourceBusiness>? logger = null
    )
    {
        _context = context;
        _edgeBusiness = edgeBusiness;
        _recordBusiness = recordBusiness;
        _eventBusiness = eventBusiness;
        _projectRolePermissionService = projectRolePermissionService;
        _adminService = adminService;
        _logger = logger;
    }

    /// <summary>
    ///     Retrieves all data sources for a specific organization or project.
    /// </summary>
    /// <param name="organizationId">The ID of the organization for which the data source belongs to</param>
    /// <param name="currentUserId">The ID of the current user for which the data source belongs to</param>
    /// <param name="projectIds">ID's of the projects whose data sources are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result</param>
    /// <returns>A list of data sources within the given project.</returns>
    public async Task<List<DataSourceResponseDto>> GetAllDataSources(
        long currentUserId,
        long organizationId,
        long[]? projectIds,
        bool hideArchived = true)
    {
        var userProjectAdminStatus = new Dictionary<long, bool>();

        bool isSysAdmin = await _adminService.SysAdminCheck(currentUserId);
        bool isOrgAdmin = await _adminService.OrgAdminCheck(currentUserId, organizationId);

        if (projectIds != null && projectIds.Length > 0)
        {
            foreach (var projectId in projectIds)
            {
                var isProjectAdmin = await _context.ProjectMembers
                    .AnyAsync(pm =>
                        pm.ProjectId == projectId &&
                        pm.IsProjectAdmin &&
                        (
                            (pm.UserId != null && pm.UserId == currentUserId) ||
                            pm.Group!.Users.Any(u => u.Id == currentUserId)
                        )
                    );

                userProjectAdminStatus[projectId] = isProjectAdmin;
            }
        }

        // Determine authorized projects based on admin status or permissions
        var authorizedProjectIds = new List<long>();
        foreach (var projectId in projectIds ?? [])
        {
            if (isSysAdmin || isOrgAdmin || userProjectAdminStatus.GetValueOrDefault(projectId, false))
            {
                authorizedProjectIds.Add(projectId);
                continue;
            }

            var hasPermission = await _projectRolePermissionService.PermissionInProject(
                currentUserId, projectId, "read", "data_source");

            if (hasPermission)
                authorizedProjectIds.Add(projectId);
        }

        if (projectIds != null && authorizedProjectIds.Count == 0)
        {
            return [];
        }

        var dsQuery = _context.DataSources.Where(d => d.OrganizationId == organizationId);

        if (hideArchived)
            dsQuery = dsQuery.Where(d => !d.IsArchived);

        if (projectIds != null && projectIds.Length > 0)
        {
            dsQuery = dsQuery.Where(d =>
                (d.ProjectId.HasValue && authorizedProjectIds.Contains(d.ProjectId.Value))
                || d.ProjectId == null);
        }
        else
        {
            dsQuery = dsQuery.Where(d => d.ProjectId == null);
        }

        var dataSourceList = await dsQuery.ToListAsync();

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
            Config = string.IsNullOrEmpty(d.Config) ? null : JsonNode.Parse(d.Config) as JsonObject,
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
        var dsQuery = _context.DataSources
            .Where(d => d.OrganizationId == organizationId && d.Id == datasourceId);

        // hide archived data sources
        if (hideArchived)
            dsQuery = dsQuery.Where(d => !d.IsArchived);

        // If project id supplied, inherit org level data sources too
        if (projectId.HasValue)
            dsQuery = dsQuery.Where(d => d.ProjectId == projectId.Value || d.ProjectId == null);
        else
            // If no project id, only org-level data sources
            dsQuery = dsQuery.Where(d => d.ProjectId == null);

        var dataSource = await dsQuery.FirstOrDefaultAsync();

        if (dataSource == null)
            throw new KeyNotFoundException(
                $"Data source with id {datasourceId} not found or does not belong to the specified organization/project context");

        // update the status cache for this datasource
        var status = dataSource.IsArchived ? EntityStatus.Archived : EntityStatus.Active;
        await SetDataSourceStatusCache(dataSource, status);

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
    /// <exception cref="KeyNotFoundException">Returned if the default data source is not found or is archived</exception>
    public async Task<DataSourceResponseDto> GetDefaultDataSource(long organizationId, long? projectId)
    {
        long? defaultDataSourceId = null;

        string cacheKey = projectId.HasValue
            ? CacheKeys.ProjectDefaultDataSource(projectId.Value)
            : CacheKeys.OrganizationDefaultDataSource(organizationId);

        long? cachedId = null;
        try
        {
            cachedId = await CacheService.Instance.GetAsync<long?>(cacheKey);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Cache read failed for default-data-source key {CacheKey}", cacheKey);
        }

        bool cacheHit = cachedId.HasValue;

        // Check the cache before querying the db
        if (cacheHit)
        {
            defaultDataSourceId = cachedId;
        }
        else
        {
            var dsQuery = _context.DataSources
                .Where(d => d.OrganizationId == organizationId && d.Default == true && !d.IsArchived);

            // If project id supplied, inherit org level data sources too
            if (projectId.HasValue)
                dsQuery = dsQuery.Where(d => d.ProjectId == projectId.Value || d.ProjectId == null && d.Default == true).OrderByDescending(d => d.ProjectId == projectId.Value);
            else
                // If no project id, only org-level data sources
                dsQuery = dsQuery.Where(d => d.ProjectId == null && d.OrganizationId == organizationId).OrderByDescending(d => d.OrganizationId == organizationId);

            var dataSourceLookup = await dsQuery.Select(d => new { d.Id }).FirstOrDefaultAsync();

            if (dataSourceLookup == null)
                throw new KeyNotFoundException(
                    "Default data source not found for the specified organization/project context");

            defaultDataSourceId = dataSourceLookup.Id;
        }

        var returnedDataSource = await _context.DataSources
            .Where(d => d.Id == defaultDataSourceId && !d.IsArchived)
            .FirstOrDefaultAsync();

        if (returnedDataSource == null)
            throw new KeyNotFoundException(
                "Default data source not found for the specified organization/project context");

        // Repopulate cache on miss for subsequent reads
        if (!cacheHit)
        {
            try
            {
                await CacheService.Instance.SetAsync(cacheKey, returnedDataSource.Id, DataSourceCacheTtl);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache population failed for default-data-source key {CacheKey}", cacheKey);
            }
        }

        // update the status cache for this datasource
        var status = returnedDataSource.IsArchived ? EntityStatus.Archived : EntityStatus.Active;
        await SetDataSourceStatusCache(returnedDataSource, status);

        return new DataSourceResponseDto
        {
            Id = returnedDataSource.Id,
            Name = returnedDataSource.Name,
            Description = returnedDataSource.Description,
            Default = true,
            Abbreviation = returnedDataSource.Abbreviation,
            Type = returnedDataSource.Type,
            BaseUri = returnedDataSource.BaseUri,
            Config = string.IsNullOrEmpty(returnedDataSource.Config)
                ? null
                : JsonNode.Parse(returnedDataSource.Config) as JsonObject,
            OrganizationId = returnedDataSource.OrganizationId,
            ProjectId = returnedDataSource.ProjectId,
            LastUpdatedAt = returnedDataSource.LastUpdatedAt,
            LastUpdatedBy = returnedDataSource.LastUpdatedBy,
            IsArchived = returnedDataSource.IsArchived
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

        // Update cached default data source
        if (dto.Default)
        {
            await UpdateDefaultDataSourceCache(dataSource.Id, organizationId, projectId);
        }

        await InvalidateDataSourceCountCaches(organizationId, projectId);

        if (projectId.HasValue)
        {
            await ProjectBusiness.InvalidateProjectStatsCache(projectId.Value, _logger);
        }

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

        // update the status cache for this datasource
        var status = dataSource.IsArchived ? EntityStatus.Archived : EntityStatus.Active;
        await SetDataSourceStatusCache(dataSource, status);

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

        var dsQuery = _context.DataSources.Where(d => d.OrganizationId == organizationId
                                                      && d.Id == dataSourceId
                                                      && !d.IsArchived);

        // If project id supplied, inherit org level data sources too 
        if (projectId.HasValue)
            dsQuery = dsQuery.Where(d => d.ProjectId == projectId.Value || d.ProjectId == null);
        else
            // If no project id, only org-level data sources
            dsQuery = dsQuery.Where(d => d.ProjectId == null);

        var dataSource = await dsQuery.FirstOrDefaultAsync();

        if (dataSource == null)
            throw new KeyNotFoundException(
                $"Data source with id {dataSourceId} not found or does not belong to the specified organization/project context");

        // Organization data sources cannot be updated from a project level
        if (projectId.HasValue && dataSource.ProjectId == null)
            throw new InvalidOperationException("Organization data sources cannot be updated from the child projects.");

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

            // update the status cache for this datasource
            var status = dataSource.IsArchived ? EntityStatus.Archived : EntityStatus.Active;
            await SetDataSourceStatusCache(dataSource, status);

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
        var dsQuery = _context.DataSources.Where(d => d.OrganizationId == organizationId
                                                      && d.Id == dataSourceId);

        // If project id supplied, inherit org level data sources too
        if (projectId.HasValue)
            dsQuery = dsQuery.Where(d => d.ProjectId == projectId.Value || d.ProjectId == null);
        else
            // If no project id, only org-level data sources
            dsQuery = dsQuery.Where(d => d.ProjectId == null);

        var dataSource = await dsQuery.FirstOrDefaultAsync();

        if (dataSource == null)
            throw new KeyNotFoundException(
                $"Data source with id {dataSourceId} not found or does not belong to the specified organization/project context");

        // Organization data sources cannot be updated from a project level
        if (projectId.HasValue && dataSource.ProjectId == null)
            throw new InvalidOperationException("Organization data sources cannot be updated from the child projects.");

        _context.DataSources.Remove(dataSource);
        await _context.SaveChangesAsync();

        // Invalidate cached default data source
        if (dataSource.Default)
        {
            await UpdateDefaultDataSourceCache(dataSourceId, dataSource.OrganizationId, dataSource.ProjectId, invalidateKey: true);
        }

        await InvalidateDataSourceCountCaches(dataSource.OrganizationId, dataSource.ProjectId);

        if (projectId.HasValue)
        {
            await ProjectBusiness.InvalidateProjectStatsCache(projectId.Value, _logger);
        }

        // update the status cache for this datasource
        var status = EntityStatus.Deleted;
        await SetDataSourceStatusCache(dataSource, status);

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
        var dsQuery = _context.DataSources.Where(d => d.OrganizationId == organizationId
                                                      && d.Id == dataSourceId
                                                      && d.IsArchived == false);

        // If project id supplied, inherit org level data sources too 
        if (projectId.HasValue)
            dsQuery = dsQuery.Where(d => d.ProjectId == projectId.Value || d.ProjectId == null);
        else
            // If no project id, only org-level data sources
            dsQuery = dsQuery.Where(d => d.ProjectId == null);

        var dataSource = await dsQuery.FirstOrDefaultAsync();

        if (dataSource == null)
            throw new KeyNotFoundException(
                $"Data source with id {dataSourceId} not found or does not belong to the specified organization/project context");

        // Organization data sources cannot be updated from a project level
        if (projectId.HasValue && dataSource.ProjectId == null)
            throw new InvalidOperationException("Organization data sources cannot be updated from the child projects.");

        dataSource.IsArchived = true;
        dataSource.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        dataSource.LastUpdatedBy = currentUserId;

        await _context.SaveChangesAsync();

        // Invalidate cached default data source
        if (dataSource.Default)
        {
            await UpdateDefaultDataSourceCache(dataSourceId, dataSource.OrganizationId, dataSource.ProjectId, invalidateKey: true);
        }

        await InvalidateDataSourceCountCaches(dataSource.OrganizationId, dataSource.ProjectId);

        if (projectId.HasValue)
        {
            await ProjectBusiness.InvalidateProjectStatsCache(projectId.Value, _logger);
        }

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

        // update the status cache for this datasource
        var status = dataSource.IsArchived ? EntityStatus.Archived : EntityStatus.Active;
        await SetDataSourceStatusCache(dataSource, status);

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
        var dsQuery = _context.DataSources.Where(d => d.OrganizationId == organizationId
                                                      && d.Id == dataSourceId
                                                      && d.IsArchived == true);

        // If project id supplied, inherit org level data sources too 
        if (projectId.HasValue)
            dsQuery = dsQuery.Where(d => d.ProjectId == projectId.Value || d.ProjectId == null);
        else
            // If no project id, only org-level data sources
            dsQuery = dsQuery.Where(d => d.ProjectId == null);

        var dataSource = await dsQuery.FirstOrDefaultAsync();

        if (dataSource == null)
            throw new KeyNotFoundException(
                $"Data source with id {dataSourceId} not found or does not belong to the specified organization/project context");

        // Organization data sources cannot be updated from a project level
        if (projectId.HasValue && dataSource.ProjectId == null)
            throw new InvalidOperationException("Organization data sources cannot be updated from the child projects.");

        dataSource.IsArchived = false;
        dataSource.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        dataSource.LastUpdatedBy = currentUserId;
        _context.DataSources.Update(dataSource);
        await _context.SaveChangesAsync();

        // If this data source is flagged as default, repopulate the cache 
        if (dataSource.Default)
        {
            await UpdateDefaultDataSourceCache(dataSource.Id, dataSource.OrganizationId, dataSource.ProjectId);
        }

        await InvalidateDataSourceCountCaches(dataSource.OrganizationId, dataSource.ProjectId);

        if (projectId.HasValue)
        {
            await ProjectBusiness.InvalidateProjectStatsCache(projectId.Value, _logger);
        }

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

        // update the status cache for this datasource
        var status = dataSource.IsArchived ? EntityStatus.Archived : EntityStatus.Active;
        await SetDataSourceStatusCache(dataSource, status);

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
        var dsQuery = _context.DataSources.Where(d => d.OrganizationId == organizationId
                                                      && d.Id == dataSourceId
                                                      && d.IsArchived == false);

        // If project id supplied, inherit org level data sources too 
        if (projectId.HasValue)
            dsQuery = dsQuery.Where(d => d.ProjectId == projectId.Value || d.ProjectId == null);
        else
            // If no project id, only org-level data sources
            dsQuery = dsQuery.Where(d => d.ProjectId == null);

        var dataSource = await dsQuery.FirstOrDefaultAsync();

        if (dataSource == null)
            throw new KeyNotFoundException(
                $"Data source with id {dataSourceId} not found or does not belong to the specified organization/project context");

        // Organization data sources cannot be updated from a project level
        if (projectId.HasValue && dataSource.ProjectId == null)
            throw new InvalidOperationException("Organization data sources cannot be updated from the child projects.");

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

            // Update cached default data source
            await UpdateDefaultDataSourceCache(dataSource.Id, organizationId, projectId);
        }

        // update the status cache for this datasource
        var status = dataSource.IsArchived ? EntityStatus.Archived : EntityStatus.Active;
        await SetDataSourceStatusCache(dataSource, status);

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

    private async Task SetDataSourceStatusCache(DataSource ds, EntityStatus status)
    {
        var cacheKey = CacheKeys.DataSourceStatus(ds.Id);
        try
        {
            await CacheService.Instance.SetAsync(cacheKey,
            new EntityStatusCacheEntry
            {
                OrganizationId = ds.OrganizationId,
                ProjectId = ds.ProjectId,
                Status = status
            },
            DataSourceCacheTtl);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Cache update failed for data source status: {CacheKey}", cacheKey);
        }
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

    private static Task InvalidateDataSourceCountCaches(long organizationId, long? projectId)
    {
        var keys = new List<string>
        {
            CacheKeys.SystemDataSourceCount(true),
            CacheKeys.SystemDataSourceCount(false),
            CacheKeys.OrganizationDataSourceCount(organizationId, true),
            CacheKeys.OrganizationDataSourceCount(organizationId, false)
        };

        if (projectId.HasValue)
        {
            keys.Add(CacheKeys.ProjectDataSourceCount(projectId.Value, true));
            keys.Add(CacheKeys.ProjectDataSourceCount(projectId.Value, false));
        }

        return Task.WhenAll(keys.Select(CacheService.Instance.DeleteAsync));
    }

    private async Task UpdateDefaultDataSourceCache(long dataSourceId, long organizationId, long? projectId, bool invalidateKey = false)
    {
        var key = projectId.HasValue
            ? CacheKeys.ProjectDefaultDataSource(projectId.Value)
            : CacheKeys.OrganizationDefaultDataSource(organizationId);

        if (invalidateKey)
        {
            try
            {
                await CacheService.Instance.DeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Default data source cache invalidation failed for key {CacheKey}", key);
            }
        }
        else
        {
            try
            {
                await CacheService.Instance.SetAsync(key, dataSourceId, DataSourceCacheTtl);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Default data source cache update failed for key {CacheKey}", key);
            }
        }

    }
}