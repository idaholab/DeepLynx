using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using NpgsqlTypes;

public class EventBusiness : IEventBusiness
{
    private readonly IBulkCopyUpsertExecutor _bulkCopyUpsertExecutor;
    private readonly ICacheBusiness _cacheBusiness;
    private readonly DeeplynxContext _context;
    private readonly INotificationBusiness _notificationBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EventBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context to be used for class operations</param>
    /// <param name="notificationBusiness">Used for initiating notifications for subscribed users</param>
    /// <param name="bulkCopyUpsertExecutor">Used to access bulk upsert operations</param>
    public EventBusiness(
        DeeplynxContext context,
        INotificationBusiness notificationBusiness,
        IBulkCopyUpsertExecutor bulkCopyUpsertExecutor
    )
    {
        _context = context;
        _notificationBusiness = notificationBusiness;
        _bulkCopyUpsertExecutor = bulkCopyUpsertExecutor;
    }

    /// <summary>
    ///     Retrieves all events without pagination.
    /// </summary>
    /// <param name="projectId">Optional filter to only include events matching the projectId</param>
    /// <param name="organizationId">Optional filter </param>
    /// <returns>List of all events matching the filter criteria</returns>
    public async Task<List<EventResponseDto>> GetAllEvents(long? organizationId, long? projectId)
    {
        var eventQuery = _context.Events
            .Include(e => e.Organization)
            .Include(e => e.Project)
            .Include(e => e.DataSource)
            .OrderByDescending(e => e.LastUpdatedAt)
            .AsQueryable();

        if (organizationId.HasValue)
        {
            eventQuery = eventQuery.Where(e => e.OrganizationId == organizationId.Value);
        }
        else if (projectId.HasValue)
        {
            eventQuery = eventQuery.Where(e => e.ProjectId == projectId.Value);
        }

        eventQuery = eventQuery.OrderByDescending(e => e.LastUpdatedAt);

        var items = await (from e in eventQuery
                join u in _context.Users on e.LastUpdatedBy equals u.Id into userGroup
                from user in userGroup.DefaultIfEmpty()
                select new EventResponseDto
                {
                    Id = e.Id,
                    Operation = e.Operation,
                    EntityType = e.EntityType,
                    EntityId = e.EntityId,
                    EntityName = e.EntityName,
                    ProjectId = e.ProjectId,
                    OrganizationId = e.OrganizationId,
                    DataSourceId = e.DataSourceId,
                    Properties = e.Properties,
                    LastUpdatedAt = e.LastUpdatedAt,
                    LastUpdatedBy = e.LastUpdatedBy,
                    LastUpdatedByUserName = user != null ? user.Name : null,
                    ProjectName = e.ProjectId != null ? e.Project.Name : null,
                    DataSourceName = e.DataSourceId != null ? e.DataSource.Name : null,
                    OrganizationName = e.OrganizationId != null ? e.Organization.Name : null
                })
            .ToListAsync();

        return items;
    }

    /// <summary>
    ///     Retrieves all project events with pagination.
    /// </summary>
    /// <param name="queryDto">Filter criteria and pagination parameters</param>
    /// <param name="organizationId">The id of the organization the events are a part of</param>
    /// <param name="projectId">The id of the project the events are a part of</param>
    /// <returns>Paginated response containing events and pagination metadata</returns>
    public async Task<PaginatedResponse<EventResponseDto>> QueryAllEvents(
        long organizationId, long? projectId, EventsQueryRequestDto? queryDto)
    {
        var eventQuery = _context.Events
            .Include(e => e.Organization)
            .Include(e => e.Project)
            .Include(e => e.DataSource)
            .AsQueryable();

        eventQuery = eventQuery.Where(e => e.OrganizationId == organizationId);

        if (projectId.HasValue)
        {
            eventQuery = eventQuery.Where(e => e.ProjectId == projectId.Value);
        }

        eventQuery = eventQuery.OrderByDescending(e => e.LastUpdatedAt);

        if (queryDto != null)
        {
            if (queryDto.LastUpdatedBy.HasValue)
            {
                eventQuery = eventQuery.Where(e => e.LastUpdatedBy == queryDto.LastUpdatedBy.Value);
            }

            if (!string.IsNullOrWhiteSpace(queryDto.Operation))
            {
                var searchTerm = queryDto.Operation.Trim();
                eventQuery = eventQuery.Where(e =>
                    EF.Functions.ILike(e.Operation, $"%{searchTerm}%"));
            }

            if (!string.IsNullOrWhiteSpace(queryDto.EntityType))
            {
                var searchTerm = queryDto.EntityType.Trim();
                eventQuery = eventQuery.Where(e =>
                    EF.Functions.ILike(e.EntityType, $"%{searchTerm}%"));
            }

            if (!string.IsNullOrWhiteSpace(queryDto.EntityName))
            {
                var searchTerm = queryDto.EntityName.Trim();
                eventQuery = eventQuery.Where(e =>
                    EF.Functions.ILike(e.EntityName, $"%{searchTerm}%"));
            }

            if (!string.IsNullOrWhiteSpace(queryDto.DataSourceName))
            {
                var searchTerm = queryDto.DataSourceName.Trim();
                eventQuery = eventQuery.Where(e =>
                    e.DataSource != null &&
                    EF.Functions.ILike(e.DataSource.Name, $"%{searchTerm}%"));
            }

            if (queryDto.StartDate.HasValue)
                eventQuery = eventQuery.Where(e => e.LastUpdatedAt >= queryDto.StartDate.Value);

            if (queryDto.EndDate.HasValue)
                eventQuery = eventQuery.Where(e => e.LastUpdatedAt <= queryDto.EndDate.Value);
        }

        // Get total count before pagination
        var totalCount = await eventQuery.CountAsync();

        // Get pagination values
        var pageNumber = queryDto?.PageNumber ?? 1;
        var pageSize = queryDto?.GetValidatedPageSize() ?? 500;

        // Apply pagination and execute query
        var items = await (from e in eventQuery
                join u in _context.Users on e.LastUpdatedBy equals u.Id into userGroup
                from user in userGroup.DefaultIfEmpty()
                select new EventResponseDto
                {
                    Id = e.Id,
                    Operation = e.Operation,
                    EntityType = e.EntityType,
                    EntityId = e.EntityId,
                    EntityName = e.EntityName,
                    ProjectId = e.ProjectId,
                    OrganizationId = e.OrganizationId,
                    DataSourceId = e.DataSourceId,
                    Properties = e.Properties,
                    LastUpdatedAt = e.LastUpdatedAt,
                    LastUpdatedBy = e.LastUpdatedBy,
                    LastUpdatedByUserName = user != null ? user.Name : null,
                    ProjectName = e.ProjectId != null ? e.Project.Name : null,
                    DataSourceName = e.DataSourceId != null ? e.DataSource.Name : null,
                    OrganizationName = e.OrganizationId != null ? e.Organization.Name : null
                })
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResponse<EventResponseDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    ///     Retrieves all project events for projects that the user is a member of, with pagination.
    /// </summary>
    /// <param name="queryDto">Filter criteria and pagination parameters</param>
    /// <returns>Paginated response containing events and pagination metadata</returns>
    public async Task<PaginatedResponse<EventResponseDto>> QueryAuthorizedEvents(long currentUserId,
        long organizationId, long[] projectIds, EventsQueryRequestDto? queryDto)
    {
        var requestingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
        bool isSysAdmin = requestingUser?.IsSysAdmin ?? false;

        var eventQuery = _context.Events
            .Include(e => e.Organization)
            .Include(e => e.Project)
            .Include(e => e.DataSource)
            .AsQueryable();

        List<long> userProjectIds;
        // Get all project IDs the user has access to within this organization
        if (!isSysAdmin)
        {
            userProjectIds = await _context.Projects
                .Where(p => p.OrganizationId == organizationId &&
                            p.ProjectMembers.Any(pm =>
                                pm.UserId == currentUserId ||
                                (pm.GroupId.HasValue && pm.Group != null &&
                                 pm.Group.Users.Any(u => u.Id == currentUserId))
                            ))
                .Select(p => p.Id)
                .ToListAsync();
        }
        // if user is system admin then return all projects regardless if they are an official member
        else
        {
            userProjectIds = await _context.Projects.Select(p => p.Id).ToListAsync();
        }

        eventQuery = eventQuery.Where(e =>
            (e.OrganizationId == organizationId) ||
            (e.ProjectId.HasValue && userProjectIds.Contains(e.ProjectId.Value))
        );

        if (projectIds.Length > 0)
        {
            var projectIdsList = projectIds.AsEnumerable();
            eventQuery = eventQuery.Where(e =>
                e.ProjectId.HasValue && projectIdsList.Contains(e.ProjectId.Value));
        }

        eventQuery = eventQuery.OrderByDescending(e => e.LastUpdatedAt);

        if (queryDto != null)
        {
            if (!string.IsNullOrWhiteSpace(queryDto.ProjectName))
            {
                var searchTerm = queryDto.ProjectName.Trim();
                eventQuery = eventQuery.Where(e =>
                    e.Project != null &&
                    EF.Functions.ILike(e.Project.Name, $"%{searchTerm}%"));
            }

            if (queryDto.LastUpdatedBy.HasValue)
            {
                eventQuery = eventQuery.Where(e => e.LastUpdatedBy == queryDto.LastUpdatedBy.Value);
            }

            if (!string.IsNullOrWhiteSpace(queryDto.Operation))
            {
                var searchTerm = queryDto.Operation.Trim();
                eventQuery = eventQuery.Where(e =>
                    EF.Functions.ILike(e.Operation, $"%{searchTerm}%"));
            }

            if (!string.IsNullOrWhiteSpace(queryDto.EntityType))
            {
                var searchTerm = queryDto.EntityType.Trim();
                eventQuery = eventQuery.Where(e =>
                    EF.Functions.ILike(e.EntityType, $"%{searchTerm}%"));
            }

            if (!string.IsNullOrWhiteSpace(queryDto.EntityName))
            {
                var searchTerm = queryDto.EntityName.Trim();
                eventQuery = eventQuery.Where(e =>
                    EF.Functions.ILike(e.EntityName, $"%{searchTerm}%"));
            }

            if (!string.IsNullOrWhiteSpace(queryDto.DataSourceName))
            {
                var searchTerm = queryDto.DataSourceName.Trim();
                eventQuery = eventQuery.Where(e =>
                    e.DataSource != null &&
                    EF.Functions.ILike(e.DataSource.Name, $"%{searchTerm}%"));
            }

            if (queryDto.StartDate.HasValue)
                eventQuery = eventQuery.Where(e => e.LastUpdatedAt >= queryDto.StartDate.Value);

            if (queryDto.EndDate.HasValue)
                eventQuery = eventQuery.Where(e => e.LastUpdatedAt <= queryDto.EndDate.Value);
        }

        // Get total count before pagination
        var totalCount = await eventQuery.CountAsync();

        // Get pagination values
        var pageNumber = queryDto?.PageNumber ?? 1;
        var pageSize = queryDto?.GetValidatedPageSize() ?? 25;

        // Apply pagination and execute query
        var items = await (from e in eventQuery
                join u in _context.Users on e.LastUpdatedBy equals u.Id into userGroup
                from user in userGroup.DefaultIfEmpty()
                select new EventResponseDto
                {
                    Id = e.Id,
                    Operation = e.Operation,
                    EntityType = e.EntityType,
                    EntityId = e.EntityId,
                    EntityName = e.EntityName,
                    ProjectId = e.ProjectId,
                    OrganizationId = e.OrganizationId,
                    DataSourceId = e.DataSourceId,
                    Properties = e.Properties,
                    LastUpdatedAt = e.LastUpdatedAt,
                    LastUpdatedBy = e.LastUpdatedBy,
                    LastUpdatedByUserName = user != null ? user.Name : null,
                    ProjectName = e.Project != null ? e.Project.Name : null,
                    DataSourceName = e.DataSource != null ? e.DataSource.Name : null
                })
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResponse<EventResponseDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Queries all events that the user is subscribed to at both organization and project levels.
    /// Organization-level subscriptions (projectId is null) match all events in the organization.
    /// Project-level subscriptions (projectId is not null) match only events in that specific project.
    /// </summary>
    /// <param name="currentUserId">The ID of the user to which the subscription belongs</param>
    /// <param name="organizationId">The ID of the organization</param>
    /// <param name="projectId">Optional ID of the project to filter events</param>
    public async Task<PaginatedResponse<EventResponseDto>> QueryEventsBySubscriptions(long currentUserId,
        long organizationId, long? projectId, EventsQueryRequestDto? queryDto)
    {
        var subscriptionsQuery = _context.Set<Subscription>()
            .Where(s => s.UserId == currentUserId && s.OrganizationId == organizationId);

        if (projectId.HasValue)
        {
            subscriptionsQuery = subscriptionsQuery.Where(s =>
                s.ProjectId == projectId.Value);
        }
        else
        {
            subscriptionsQuery = subscriptionsQuery.Where(s => s.ProjectId == null);
        }

        var subscriptions = await subscriptionsQuery.ToListAsync();

        if (!subscriptions.Any())
            return new PaginatedResponse<EventResponseDto>
            {
                Items = new List<EventResponseDto>(),
                PageNumber = queryDto?.PageNumber ?? 1,
                PageSize = queryDto?.GetValidatedPageSize() ?? 25,
                TotalCount = 0
            };

        string sql;

        if (projectId.HasValue)
        {
            sql = @"
                SELECT DISTINCT e.*
                FROM deeplynx.events e
                INNER JOIN deeplynx.subscriptions s
                    ON s.user_id = @currentUserId
                    AND s.project_id = @projectId
                WHERE e.project_id = @projectId
                    AND ((s.entity_id = e.entity_id) OR s.entity_id IS NULL)
                    AND ((s.entity_type = e.entity_type) OR s.entity_type IS NULL)
                    AND ((s.data_source_id = e.data_source_id) OR s.data_source_id IS NULL)
                    AND ((s.operation = e.operation) OR s.operation IS NULL)";
        }
        else
        {
            sql = @"
                SELECT DISTINCT e.*
                FROM deeplynx.events e
                INNER JOIN deeplynx.subscriptions s
                    ON s.user_id = @currentUserId
                    AND s.organization_id = @organizationId
                    AND s.project_id IS NULL
                WHERE e.organization_id = @organizationId
                    AND ((s.entity_id = e.entity_id) OR s.entity_id IS NULL)
                    AND ((s.entity_type = e.entity_type) OR s.entity_type IS NULL)
                    AND ((s.data_source_id = e.data_source_id) OR s.data_source_id IS NULL)
                    AND ((s.operation = e.operation) OR s.operation IS NULL)";
        }

        var currentUserIdParam = new NpgsqlParameter("currentUserId", currentUserId);
        var organizationIdParam = new NpgsqlParameter("organizationId", organizationId);
        var projectIdParam = new NpgsqlParameter("projectId", (object?)projectId ?? DBNull.Value);

        var eventQuery = _context.Events
            .FromSqlRaw(sql, currentUserIdParam, organizationIdParam, projectIdParam)
            .Include(e => e.Organization)
            .Include(e => e.Project)
            .Include(e => e.DataSource)
            .AsQueryable();

        if (queryDto != null)
        {
            if (queryDto.LastUpdatedBy.HasValue)
            {
                eventQuery = eventQuery.Where(e => e.LastUpdatedBy == queryDto.LastUpdatedBy.Value);
            }

            if (!string.IsNullOrWhiteSpace(queryDto.ProjectName))
            {
                var searchTerm = queryDto.ProjectName.Trim();
                eventQuery = eventQuery.Where(e =>
                    e.Project != null &&
                    EF.Functions.ILike(e.Project.Name, $"%{searchTerm}%"));
            }

            if (!string.IsNullOrWhiteSpace(queryDto.Operation))
            {
                var searchTerm = queryDto.Operation.Trim();
                eventQuery = eventQuery.Where(e =>
                    EF.Functions.ILike(e.Operation, $"%{searchTerm}%"));
            }

            if (!string.IsNullOrWhiteSpace(queryDto.EntityType))
            {
                var searchTerm = queryDto.EntityType.Trim();
                eventQuery = eventQuery.Where(e =>
                    EF.Functions.ILike(e.EntityType, $"%{searchTerm}%"));
            }

            if (!string.IsNullOrWhiteSpace(queryDto.EntityName))
            {
                var searchTerm = queryDto.EntityName.Trim();
                eventQuery = eventQuery.Where(e =>
                    EF.Functions.ILike(e.EntityName, $"%{searchTerm}%"));
            }

            if (!string.IsNullOrWhiteSpace(queryDto.DataSourceName))
            {
                var searchTerm = queryDto.DataSourceName.Trim();
                eventQuery = eventQuery.Where(e =>
                    e.DataSource != null &&
                    EF.Functions.ILike(e.DataSource.Name, $"%{searchTerm}%"));
            }

            if (queryDto.StartDate.HasValue)
                eventQuery = eventQuery.Where(e => e.LastUpdatedAt >= queryDto.StartDate.Value);

            if (queryDto.EndDate.HasValue)
                eventQuery = eventQuery.Where(e => e.LastUpdatedAt <= queryDto.EndDate.Value);
        }

        eventQuery = eventQuery.OrderByDescending(e => e.LastUpdatedAt);

        var totalCount = await eventQuery.CountAsync();

        var pageNumber = queryDto?.PageNumber ?? 1;
        var pageSize = queryDto?.GetValidatedPageSize() ?? 25;

        var items = await (from e in eventQuery
                join u in _context.Users on e.LastUpdatedBy equals u.Id into userGroup
                from user in userGroup.DefaultIfEmpty()
                select new EventResponseDto
                {
                    Id = e.Id,
                    Operation = e.Operation,
                    EntityType = e.EntityType,
                    EntityId = e.EntityId,
                    EntityName = e.EntityName,
                    ProjectId = e.ProjectId,
                    OrganizationId = e.OrganizationId,
                    DataSourceId = e.DataSourceId,
                    Properties = e.Properties,
                    LastUpdatedAt = e.LastUpdatedAt,
                    LastUpdatedBy = e.LastUpdatedBy,
                    LastUpdatedByUserName = user != null ? user.Name : null,
                    ProjectName = e.ProjectId != null ? e.Project.Name : null,
                    DataSourceName = e.DataSourceId != null ? e.DataSource.Name : null,
                    OrganizationName = e.OrganizationId != null ? e.Organization.Name : null
                })
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResponse<EventResponseDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    ///     Creates a new Event based on the event data provided.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">ID of the organization the event belongs to.</param>
    /// <param name="projectId">ID of the project the event belongs to.</param>
    /// <param name="dto">A data transfer object with details on the new event to be created.</param>
    /// <param name="count">A data transfer object with details on the new event to be created.</param>
    /// <returns>The new Event which was just created.</returns>
    public async Task<EventResponseDto> CreateEvent(long currentUserId, long organizationId, long? projectId,
        CreateEventRequestDto dto, long? count = 1)
    {
        ValidationHelper.ValidateModel(dto);
        ValidationHelper.ValidateTypes(dto.EntityType, "EntityType");
        ValidationHelper.ValidateTypes(dto.Operation, "Operation");

        var project = projectId != null
            ? await _context.Projects.FindAsync(projectId)
            : null;

        var organization = await _context.Organizations.FindAsync(organizationId);

        var dataSource = dto.DataSourceId.HasValue
            ? await _context.DataSources.FindAsync(dto.DataSourceId.Value)
            : null;

        Dictionary<string, object> properties;

        if (!string.IsNullOrWhiteSpace(dto.Properties))
        {
            properties = JsonSerializer.Deserialize<Dictionary<string, object>>(dto.Properties)
                         ?? new Dictionary<string, object>();
        }
        else
        {
            properties = new Dictionary<string, object>();
        }

        properties["Count"] = count.Value;

        var propertiesJson = JsonSerializer.Serialize(properties);

        var newEvent = new Event
        {
            Operation = dto.Operation,
            EntityType = dto.EntityType,
            OrganizationId = organizationId,
            ProjectId = projectId,
            Properties = propertiesJson,
            LastUpdatedBy = currentUserId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            DataSourceId = dto.DataSourceId,
            EntityId = dto.EntityId,
            EntityName = dto.EntityName,
        };

        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync();

        var response = new EventResponseDto
        {
            Id = newEvent.Id,
            Operation = newEvent.Operation,
            EntityType = newEvent.EntityType,
            EntityId = newEvent.EntityId,
            EntityName = newEvent.EntityName,
            ProjectId = newEvent.ProjectId,
            OrganizationId = newEvent.OrganizationId,
            DataSourceId = newEvent.DataSourceId,
            Properties = newEvent.Properties,
            LastUpdatedAt = newEvent.LastUpdatedAt,
            LastUpdatedBy = newEvent.LastUpdatedBy,
            ProjectName = project?.Name,
            DataSourceName = dataSource?.Name,
            OrganizationName = organization?.Name
        };

        if (Environment.GetEnvironmentVariable("ENABLE_NOTIFICATION_SERVICE") == "true")
            await _notificationBusiness.SendEventNotification(response);

        return response;
    }

    /// <summary>
    ///     Bulk creates Events based on the event data provided using the copy executor.
    /// </summary>
    /// <param name="conn">NPGSQL PostgreSQL Connection</param>
    /// <param name="tx">NPGSQL PostgreSQL Transaction for rollback</param>
    /// <param name="events">Event objects to upsert</param>
    /// <param name="projectId">The ID of the project to which the event belongs</param>
    /// <param name="userId">The ID of the user who will be the last to update these</param>
    /// <param name="ct">Optional cancellation token to end long rquests</param>
    /// <returns>The list of new Events which were created.</returns>
    public async Task BulkInsertEventsWithCopyAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        IReadOnlyList<CreateEventRequestDto> events,
        long projectId,
        long? userId,
        CancellationToken ct = default)
    {
        if (events is null || events.Count == 0) return;

        // If your table is not public.events, fully-qualify it and quote as needed:
        // e.g., deeplynx.events or deeplynx."Events"
        const string createTempSql = @"
        CREATE TEMP TABLE tmp_events
        (
            project_id      BIGINT NOT NULL,
            operation       TEXT   NOT NULL,
            entity_type     TEXT   NOT NULL,
            entity_id       BIGINT NOT NULL,
            entity_name     TEXT   NULL,
            data_source_id  BIGINT NULL,
            properties      JSONB  NOT NULL,
            last_updated_by BIGINT NULL,
            last_updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL
        ) ON COMMIT DROP;";

        const string copyCmd = @"
        COPY tmp_events
        (project_id, operation, entity_type, entity_id, entity_name, data_source_id, properties, last_updated_by, last_updated_at)
        FROM STDIN (FORMAT BINARY)";

        // adjust table identifier to your real one (schema + quoting)
        const string insertSql = @"
        INSERT INTO deeplynx.events
        (project_id, operation, entity_type, entity_id, entity_name, data_source_id, properties, last_updated_by, last_updated_at)
        SELECT project_id, operation, entity_type, entity_id, entity_name, data_source_id, properties, last_updated_by, last_updated_at
        FROM tmp_events;";

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _bulkCopyUpsertExecutor.CopyInsertAsync(
            conn, tx,
            createTempSql, copyCmd,
            events,
            (w, e) =>
            {
                w.Write(projectId, NpgsqlDbType.Bigint);
                w.Write(e.Operation, NpgsqlDbType.Text);
                w.Write(e.EntityType, NpgsqlDbType.Text);
                w.Write(e.EntityId, NpgsqlDbType.Bigint);
                if (e.EntityName is null) w.WriteNull();
                else w.Write(e.EntityName, NpgsqlDbType.Text);
                if (e.DataSourceId.HasValue) w.Write(e.DataSourceId.Value, NpgsqlDbType.Bigint);
                else w.WriteNull();
                w.Write(e.Properties ?? "{}", NpgsqlDbType.Jsonb);
                if (userId.HasValue) w.Write(userId.Value, NpgsqlDbType.Bigint);
                else w.WriteNull();
                w.Write(now, NpgsqlDbType.Timestamp);
            },
            insertSql,
            ct);
    }

    /// <summary>
    ///     Map an NPGSQL data reader to a return DTO usually during high scale read operations
    /// </summary>
    /// <param name="r">NPGSQL reader object containing DTO params</param>
    /// <returns>A response data transfer object with fields mapped from the pg reader</returns>
    private static Func<NpgsqlDataReader, EventResponseDto> MakeEventMapper(NpgsqlDataReader r)
    {
        var iId = r.GetOrdinal("id");
        var iOp = r.GetOrdinal("operation");
        var iType = r.GetOrdinal("entity_type");
        var iEid = r.GetOrdinal("entity_id");
        var iEname = r.GetOrdinal("entity_name");
        var iProj = r.GetOrdinal("project_id");
        var iDs = r.GetOrdinal("data_source_id");
        var iProps = r.GetOrdinal("properties");
        var iLuat = r.GetOrdinal("last_updated_at");
        var iLuBy = r.GetOrdinal("last_updated_by");

        return rr => new EventResponseDto
        {
            Id = rr.GetInt64(iId),
            Operation = rr.GetString(iOp),
            EntityType = rr.GetString(iType),
            EntityId = rr.GetInt64(iEid),
            EntityName = rr.IsDBNull(iEname) ? null : rr.GetString(iEname),
            ProjectId = rr.GetInt64(iProj),
            DataSourceId = rr.IsDBNull(iDs) ? null : rr.GetInt64(iDs),
            Properties = rr.GetFieldValue<string>(iProps), // or JSON -> string
            LastUpdatedAt = rr.GetDateTime(iLuat),
            LastUpdatedBy = rr.IsDBNull(iLuBy) ? null : rr.GetInt64(iLuBy)
        };
    }

    /// <summary>
    /// Bulk creates Events based on the event data provided.
    /// </summary>
    /// <param name="projectId">The ID of the project to which the event belongs</param>
    /// <param name="events">A List of data transfer objects with details on the new event to be created.</param>
    /// <returns>The list of new Events which were created.</returns>
    public async Task<List<EventResponseDto>> BulkCreateEvents(
        long projectId,
        List<CreateEventRequestDto> events
    )
    {
        foreach (var dto in events)
        {
            ValidationHelper.ValidateTypes(dto.EntityType, "EntityType");
            ValidationHelper.ValidateTypes(dto.Operation, "Operation");
        }

        var project = await _context.Projects.FindAsync(projectId);
        var dataSource = events.First().DataSourceId != null
            ? await _context.DataSources.FindAsync(events.First().DataSourceId)
            : null;

        var eventEntities = events.Select(dto => new Event
        {
            ProjectId = projectId,
            Operation = dto.Operation,
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            EntityName = dto.EntityName,
            Properties = dto.Properties,
            DataSourceId = dto.DataSourceId,
            LastUpdatedBy = UserContextStorage.UserId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        }).ToList();

        _context.Events.AddRange(eventEntities);
        await _context.SaveChangesAsync();

        var response = eventEntities.Select(e => new EventResponseDto
        {
            Id = e.Id,
            Operation = e.Operation,
            EntityType = e.EntityType,
            EntityId = e.EntityId,
            EntityName = e.EntityName,
            ProjectId = e.ProjectId,
            OrganizationId = e.OrganizationId,
            DataSourceId = e.DataSourceId,
            Properties = e.Properties,
            LastUpdatedAt = e.LastUpdatedAt,
            LastUpdatedBy = e.LastUpdatedBy,
            ProjectName = project?.Name,
            DataSourceName = dataSource?.Name
        }).ToList();

        if (Environment.GetEnvironmentVariable("ENABLE_NOTIFICATION_SERVICE") == "true")
            await _notificationBusiness.SendBulkEventNotifications(response);

        return response;
    }
}