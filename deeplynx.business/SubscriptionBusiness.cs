using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace deeplynx.business;

public class SubscriptionBusiness : ISubscriptionBusiness
{
    private readonly DeeplynxContext _context;

    public SubscriptionBusiness(DeeplynxContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Retrieves all subscriptions with a particular userId and projectId
    /// </summary>
    /// <param name="userId">The ID of the user to which the subscription belongs</param>
    /// <param name="projectId">The ID of the project to which the subscription belongs</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived classes from the result</param>
    /// <returns>A list of subscriptions</returns>
    /// TODO: CACHE ALL SUBSCRIPTIONS TO MAKE THE NOTIFICATION SERVICE MORE EFFICIENT
    public async Task<List<SubscriptionResponseDto>> GetAllSubscriptions(long userId, long projectId, bool hideArchived)
    {
        var subscriptions = await _context.Subscriptions
            .Where(s => s.UserId == userId && s.ProjectId == projectId)
            .ToListAsync();

        if (hideArchived) subscriptions = subscriptions.Where(s => !s.IsArchived).ToList();

        return subscriptions.Select(s => new SubscriptionResponseDto
        {
            Id = s.Id,
            UserId = s.UserId,
            OrganizationId = s.OrganizationId,
            ProjectId = s.ProjectId,
            ActionId = s.ActionId,
            Operation = s.Operation,
            DataSourceId = s.DataSourceId,
            EntityType = s.EntityType,
            EntityId = s.EntityId,
            LastUpdatedAt = s.LastUpdatedAt,
            LastUpdatedBy = s.LastUpdatedBy,
            IsArchived = s.IsArchived
        }).ToList();
    }

    /// <summary>
    ///     Retrieves a single subscription with a particular userId, projectId, and subscriptionId
    /// </summary>
    /// <param name="userId">The ID of the user to which the subscription belongs</param>
    /// <param name="projectId">The ID of the project to which the subscription belongs</param>
    /// <param name="subscriptionId">The ID of the subscription</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived classes from the result</param>
    /// <returns>A list of subscriptions</returns>
    /// <exception cref="KeyNotFoundException">Returned if subscription is not found or is archived</exception>
    public async Task<SubscriptionResponseDto> GetSubscription(long userId, long projectId, long subscriptionId,
        bool hideArchived)
    {
        var subscription = await _context.Subscriptions
            .Where(s => s.UserId == userId && s.ProjectId == projectId && s.Id == subscriptionId)
            .SingleOrDefaultAsync();

        if (subscription == null) throw new KeyNotFoundException($"Subscription with id {subscriptionId} not found");

        if (hideArchived && subscription.IsArchived)
            throw new KeyNotFoundException($"Subscription with id {subscriptionId} is archived");

        return new SubscriptionResponseDto
        {
            Id = subscription.Id,
            UserId = subscription.UserId,
            OrganizationId = subscription.OrganizationId,
            ProjectId = subscription.ProjectId,
            ActionId = subscription.ActionId,
            Operation = subscription.Operation,
            DataSourceId = subscription.DataSourceId,
            EntityType = subscription.EntityType,
            EntityId = subscription.EntityId,
            LastUpdatedAt = subscription.LastUpdatedAt,
            LastUpdatedBy = subscription.LastUpdatedBy,
            IsArchived = subscription.IsArchived
        };
    }

    /// <summary>
    ///     Bulk creates subscriptions
    /// </summary>
    /// <param name="currentUserId">The ID of the user to which the subscriptions belong</param>
    /// <param name="organizationId">The ID of the organization to which the subscription belong</param>
    /// <param name="projectId">The ID of the project to which the subscriptions belong</param>
    /// <returns>A list of created subscriptions</returns>
    public async Task<List<SubscriptionResponseDto>> BulkCreateSubscriptions(long currentUserId, long organizationId,
        long projectId,
        List<CreateSubscriptionRequestDto> dtos)
    {
        foreach (var dto in dtos)
        {
            ValidationHelper.ValidateModel(dto);

            // Validate the Operation property if it is not null
            if (dto.Operation != null) ValidationHelper.ValidateTypes(dto.Operation, "Operation");

            // Ensure both EntityType and EntityId are either both not null or both null
            if ((dto.EntityType != null && dto.EntityId != null) || (dto.EntityType == null && dto.EntityId == null))
            {
                if (dto.EntityType != null) ValidationHelper.ValidateTypes(dto.EntityType, "EntityType");
            }
            else
            {
                throw new ArgumentException("EntityType and EntityId must either both be null or both have values.");
            }
        }

        // Bulk insert into subscriptions; if there is a conflict, do nothing
        var sql = @"
        INSERT INTO deeplynx.subscriptions (user_id, action_id, operation, organization_id, project_id, data_source_id, entity_type, entity_id, last_updated_at, last_updated_by, is_archived)
        VALUES {0}
        ON CONFLICT (user_id, action_id, operation, project_id, data_source_id, entity_type, entity_id) DO NOTHING
        RETURNING id, user_id, organization_id, project_id, action_id, operation, data_source_id, entity_type, entity_id, last_updated_at, last_updated_by, is_archived;
    ";

        // Establish "constant" parameters
        var parameters = new List<NpgsqlParameter>
        {
            new("@now", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)),
            new("@lastUpdatedBy", currentUserId)
        };

        // Establish "dynamic" parameters (new for each dto in the list)
        parameters.AddRange(dtos.SelectMany((dto, i) => new[]
        {
            new NpgsqlParameter($"@p{i}_userId", currentUserId),
            new NpgsqlParameter($"@p{i}_actionId", dto.ActionId),
            new NpgsqlParameter($"@p{i}_operation", (object?)dto.Operation ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_organizationId", organizationId),
            new NpgsqlParameter($"@p{i}_projectId", projectId),
            new NpgsqlParameter($"@p{i}_dataSourceId", (object?)dto.DataSourceId ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_entityType", (object?)dto.EntityType ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_entityId", (object?)dto.EntityId ?? DBNull.Value)
        }));

        // Stringify the params and comma separate them
        var valueTuples = string.Join(", ", dtos.Select((dto, i) =>
            $"(@p{i}_userId, @p{i}_actionId, @p{i}_operation, @p{i}_organizationId, @p{i}_projectId, @p{i}_dataSourceId, @p{i}_entityType, @p{i}_entityId, @now, @lastUpdatedBy, false)"));

        // Put everything together and execute the query
        sql = string.Format(sql, valueTuples);

        // Returns the resulting inserted subscriptions
        var result = await _context.Subscriptions
            .FromSqlRaw(sql, parameters.ToArray())
            .ToListAsync();

        return result.Select(s => new SubscriptionResponseDto
        {
            Id = s.Id,
            UserId = s.UserId,
            ProjectId = s.ProjectId,
            OrganizationId = s.OrganizationId,
            ActionId = s.ActionId,
            Operation = s.Operation,
            DataSourceId = s.DataSourceId,
            EntityType = s.EntityType,
            EntityId = s.EntityId,
            LastUpdatedAt = s.LastUpdatedAt,
            LastUpdatedBy = s.LastUpdatedBy,
            IsArchived = s.IsArchived
        }).ToList();
    }

    /// <summary>
    ///     Bulk updates subscriptions
    /// </summary>
    /// <param name="currentUserId">The ID of the user to which the subscriptions belong</param>
    /// <param name="organizationId">The ID of the organization to which the subscription belong</param>
    /// <param name="projectId">The ID of the project to which the subscriptions belong</param>
    /// <returns>A list of updated subscriptions</returns>
    public async Task<List<SubscriptionResponseDto>> BulkUpdateSubscriptions(
        long currentUserId,
        long organizationId,
        long projectId,
        List<UpdateSubscriptionRequestDto> dtos)
    {
        foreach (var dto in dtos)
        {
            ValidationHelper.ValidateModel(dto);

            // Validate the Operation property if it is not null
            if (dto.Operation != null) ValidationHelper.ValidateTypes(dto.Operation, "Operation");

            // Ensure both EntityType and EntityId are either both not null or both null
            if ((dto.EntityType != null && dto.EntityId != null) || (dto.EntityType == null && dto.EntityId == null))
            {
                if (dto.EntityType != null) ValidationHelper.ValidateTypes(dto.EntityType, "EntityType");
            }
            else
            {
                throw new ArgumentException("EntityType and EntityId must either both be null or both have values.");
            }
        }

        // Build the SQL statement
        var sql = @"
        UPDATE deeplynx.subscriptions AS subs
        SET 
            action_id = data.action_id,
            operation = data.operation,
            data_source_id = data.data_source_id,
            entity_type = data.entity_type,
            entity_id = data.entity_id,
            last_updated_at = @now,
            last_updated_by = @userId
        FROM (VALUES {0}) AS data (id, action_id, operation, data_source_id, entity_type, entity_id)
        WHERE 
            subs.id = data.id 
            AND subs.user_id = @userId 
            AND subs.organization_id = @organizationId
            AND subs.project_id = @projectId
        RETURNING subs.id, subs.user_id, subs.organization_id, subs.project_id, subs.action_id, subs.operation, 
            subs.data_source_id, subs.entity_type, subs.entity_id, 
            subs.last_updated_at, subs.is_archived, subs.last_updated_by;
        ";

        // Establish "constant" parameters
        var parameters = new List<NpgsqlParameter>
        {
            new("@userId", currentUserId),
            new("@organizationId", organizationId),
            new("@projectId", projectId),
            new("@now", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified))
        };

        // Establish "dynamic" parameters (new for each dto in the list)
        parameters.AddRange(dtos.SelectMany((dto, i) => new[]
        {
            new NpgsqlParameter($"@p{i}_id", dto.Id),
            new NpgsqlParameter($"@p{i}_actionId", (object?)dto.ActionId ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_operation", (object?)dto.Operation ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_dataSourceId", (object?)dto.DataSourceId ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_entityType", (object?)dto.EntityType ?? DBNull.Value),
            new NpgsqlParameter($"@p{i}_entityId", (object?)dto.EntityId ?? DBNull.Value)
        }));

        // Stringify the params and comma separate them
        var valueTuples = string.Join(", ", dtos.Select((dto, i) =>
            $"(@p{i}_id, @p{i}_actionId, @p{i}_operation, @p{i}_dataSourceId, @p{i}_entityType, @p{i}_entityId)"));

        // Put everything together and execute the query
        sql = string.Format(sql, valueTuples);

        // Execute the query and map the results
        var updatedSubscriptions = await _context.Subscriptions
            .FromSqlRaw(sql, parameters.ToArray())
            .ToListAsync();

        return updatedSubscriptions.Select(s => new SubscriptionResponseDto
        {
            Id = s.Id,
            UserId = s.UserId,
            OrganizationId = s.OrganizationId,
            ProjectId = s.ProjectId,
            ActionId = s.ActionId,
            Operation = s.Operation,
            DataSourceId = s.DataSourceId,
            EntityType = s.EntityType,
            EntityId = s.EntityId,
            LastUpdatedAt = s.LastUpdatedAt,
            LastUpdatedBy = s.LastUpdatedBy,
            IsArchived = s.IsArchived
        }).ToList();
    }


    /// <summary>
    ///     Bulk deletes subscriptions
    /// </summary>
    /// <param name="currentUserId">The ID of the user to which the subscriptions belong</param>
    /// <param name="projectId">The ID of the project to which the subscriptions belong</param>
    /// <param name="subscriptionIds">The ID of the subscriptions to be deleted</param>
    /// <returns>A list of created subscriptions</returns>
    public async Task<bool> BulkDeleteSubscriptions(long currentUserId, long projectId, List<long> subscriptionIds)
    {
        await ExistenceHelper.EnsureProjectExistsAsync(_context, projectId);

        // Build the SQL statement
        var sql = @"
        DELETE FROM deeplynx.subscriptions
        WHERE 
            user_id = @userId 
            AND project_id = @projectId 
            AND id = ANY(@subscriptionIds);
    ";

        // Establish parameters
        var parameters = new List<NpgsqlParameter>
        {
            new("@userId", currentUserId),
            new("@projectId", projectId),
            new("@subscriptionIds", subscriptionIds.ToArray())
        };

        // Execute the query
        var result = await _context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());

        // Check if the number of affected rows matches the number of IDs provided
        if (result != subscriptionIds.Count)
            throw new InvalidOperationException("Some subscriptions were not deleted because they do not exist.");

        return true;
    }

    /// <summary>
    ///     Bulk archives subscriptions
    /// </summary>
    /// <param name="currentUserId">The ID of the user to which the subscriptions belong</param>
    /// <param name="projectId">The ID of the project to which the subscriptions belong</param>
    /// <param name="subscriptionIds">The ID of the subscriptions to be archived</param>
    /// <returns>A list of created subscriptions</returns>
    public async Task<bool> BulkArchiveSubscriptions(long currentUserId, long projectId, List<long> subscriptionIds)
    {
        await ExistenceHelper.EnsureProjectExistsAsync(_context, projectId);

        // Build the SQL statement
        var sql = @"
        UPDATE deeplynx.subscriptions
        SET 
            is_archived = true,
            last_updated_at = @now,
            last_updated_by = @userId
        WHERE 
            user_id = @userId 
            AND project_id = @projectId 
            AND id = ANY(@subscriptionIds);
    ";

        // Establish parameters
        var parameters = new List<NpgsqlParameter>
        {
            new("@userId", currentUserId),
            new("@projectId", projectId),
            new("@subscriptionIds", subscriptionIds.ToArray()),
            new("@now", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified))
        };

        // Execute the query
        var result = await _context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());

        // Check if the number of affected rows matches the number of IDs provided
        if (result != subscriptionIds.Count)
            throw new InvalidOperationException("Some subscriptions were not archived because they do not exist.");

        return true;
    }

    /// <summary>
    ///     Bulk unarchives subscriptions
    /// </summary>
    /// <param name="currentUserId">The ID of the user to which the subscriptions belong</param>
    /// <param name="projectId">The ID of the project to which the subscriptions belong</param>
    /// <param name="subscriptionIds">The ID of the subscriptions to be unarchived</param>
    /// <returns>A list of created subscriptions</returns>
    public async Task<bool> BulkUnarchiveSubscriptions(long currentUserId, long projectId, List<long> subscriptionIds)
    {
        await ExistenceHelper.EnsureProjectExistsAsync(_context, projectId);

        // Build the SQL statement
        var sql = @"
    UPDATE deeplynx.subscriptions
    SET 
        is_archived = false,
        last_updated_at = @now,
        last_updated_by = @userId
    WHERE 
        user_id = @userId 
        AND project_id = @projectId 
        AND id = ANY(@subscriptionIds);
    ";

        // Establish parameters
        var parameters = new List<NpgsqlParameter>
        {
            new("@userId", currentUserId),
            new("@projectId", projectId),
            new("@subscriptionIds", subscriptionIds.ToArray()),
            new("@now", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified))
        };

        // Execute the query
        var result = await _context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());

        // Check if the number of affected rows matches the number of IDs provided
        if (result != subscriptionIds.Count)
            throw new InvalidOperationException("Some subscriptions were not unarchived because they do not exist.");

        return true;
    }
}