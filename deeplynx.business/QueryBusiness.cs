using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace deeplynx.business;

/// <summary>
///     Filter record request
/// </summary>
public class QueryBusiness : IQueryBusiness
{
    private readonly DeeplynxContext _context;
    private readonly ISensitivityLabelService _sensitivityLabelService;

    /// <summary>
    ///     Filter record request
    /// </summary>
    /// <param name="context">The database context to be used for filter operations.</param>
    /// <param name="sensitivityLabelService">Helper service for Sensitivity Label Authorization.</param>
    public QueryBusiness(DeeplynxContext context, ISensitivityLabelService sensitivityLabelService)
    {
        _context = context;
        _sensitivityLabelService = sensitivityLabelService;
    }

    /// <summary>
    ///     Build a query
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="request">Array of query component dtos, initial connector string will be null</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="textSearch">Full text search phrase</param>
    /// <param name="projectIds">Project ids that a user has access to</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>A list of historical record response dtos that match provided filters</returns>
    public async Task<IEnumerable<HistoricalRecordResponseDto>> QueryBuilder(
        long currentUserId, CustomQueryDtos.CustomQueryRequestDto[] request, long organizationId, long[] projectIds,
        string? textSearch = null, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        if (request == null) throw new ArgumentException("Custom query request dto cannot be null");
        try
        {
            var authorizedLabelIds = new List<long>();
            if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
            {
                authorizedLabelIds = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                    currentUserId, organizationId, projectIds, "read record");
            }

            var authorizationFilter = (!isSysAdmin && !isOrgAdmin && !isProjectAdmin) ? @"
                AND (
                    NOT EXISTS (
                        SELECT 1 
                        FROM deeplynx.record_labels 
                        WHERE record_id = hr.record_id
                    )
                    OR
                    NOT EXISTS (
                        SELECT 1
                        FROM deeplynx.record_labels rl2
                        WHERE rl2.record_id = hr.record_id
                        AND rl2.label_id != ALL(@authorizedLabelIds)
                    )
                )" : "";

            var sql = $@"
                SELECT DISTINCT ON (hr.record_id)
                    hr.*,
                    hr.class_id as ClassId,
                    hr.class_name as ClassName,
                    hr.original_id as OriginalId,
                    hr.data_source_name as DataSourceName,
                    hr.data_source_id as DataSourceId,
                    hr.project_name as ProjectName,
                    hr.project_id as ProjectId,
                    hr.last_updated_at as LastUpdatedAt,
                    hr.last_updated_by as LastUpdatedBy,
                    hr.object_storage_name as ObjectStorageName,
                    hr.object_storage_id as ObjectStorageId,
                    hr.record_id as RecordId,
                    hr.is_archived as IsArchived
                FROM deeplynx.historical_records hr
                WHERE hr.is_archived = false
                AND hr.project_id = ANY(@projectIds)
                AND hr.organization_id = @organizationId
                {authorizationFilter}";

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("projectIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = projectIds },
                new NpgsqlParameter("organizationId", organizationId)
            };

            if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
            {
                parameters.Add(new NpgsqlParameter("authorizedLabelIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
                {
                    Value = authorizedLabelIds.ToArray()
                });
            }

            // Build individual conditions
            var conditions = new List<string>();
            if (request?.Length > 0)
                for (var i = 0; i < request.Length; i++)
                {
                    var query = request[i];
                    if (string.IsNullOrWhiteSpace(query.Value) && query.Operator != "KEY_VALUE")
                        throw new ArgumentException("Value cannot be null or empty.");
                    var condition = "";
                    var paramName = $"param{i}";

                    // Build the individual condition
                    if (query.Operator == "KEY_VALUE")
                    {
                        condition = $"({query.Filter}::jsonb @> @{paramName}::jsonb)";
                        parameters.Add(new NpgsqlParameter(paramName, query.Json));
                    }
                    else if (query.Operator == "LIKE")
                    {
                        // Check if this is a JSONB column that needs special handling
                        var jsonbColumns = new[] { "properties", "tags" };

                        if (jsonbColumns.Contains(query.Filter.ToLower()))
                        {
                            if (query.Filter.ToLower() == "tags")
                                // Tags are an array of objects - flatten and search only the name values
                                condition =
                                    $"EXISTS (SELECT 1 FROM jsonb_array_elements(hr.{query.Filter}) elem WHERE elem->>'name' ILIKE @{paramName})";
                            else
                                // Properties is a flat object already - we can just search the values
                                condition =
                                    $"EXISTS (SELECT 1 FROM jsonb_each_text(hr.{query.Filter}) WHERE value ILIKE @{paramName})";
                        }
                        else
                        {
                            condition = $"hr.{query.Filter} ILIKE @{paramName}";
                        }

                        parameters.Add(new NpgsqlParameter(paramName, $"%{query.Value}%"));
                    }
                    else if (query.Operator == "=")
                    {
                        // Check if this is a JSONB column that needs special handling
                        var jsonbColumns = new[] { "properties", "tags" };

                        if (jsonbColumns.Contains(query.Filter.ToLower()))
                        {
                            // For JSONB columns, convert to text for exact match
                            condition = $"jsonb_pretty(hr.{query.Filter}) ILIKE @{paramName}";
                            parameters.Add(new NpgsqlParameter(paramName, $"%{query.Value}%"));
                        }
                        else
                        {
                            condition = $"hr.{query.Filter} = @{paramName}";

                            if (int.TryParse(query.Value, out var intVal))
                                parameters.Add(new NpgsqlParameter(paramName, intVal));
                            else if (DateTime.TryParse(query.Value, out var dateVal))
                                parameters.Add(new NpgsqlParameter(paramName, dateVal));
                            else
                                parameters.Add(new NpgsqlParameter(paramName, query.Value));
                        }
                    }
                    else if (query.Operator == ">")
                    {
                        condition = $"hr.{query.Filter} > @{paramName}";

                        if (DateTime.TryParse(query.Value, out var dateVal))
                            parameters.Add(new NpgsqlParameter(paramName, dateVal));
                        else
                            parameters.Add(new NpgsqlParameter(paramName, query.Value));
                    }
                    else if (query.Operator == "<")
                    {
                        condition = $"hr.{query.Filter} < @{paramName}";

                        if (DateTime.TryParse(query.Value, out var dateVal))
                            parameters.Add(new NpgsqlParameter(paramName, dateVal));
                        else
                            parameters.Add(new NpgsqlParameter(paramName, query.Value));
                    }
                    else
                    {
                        throw new ArgumentException("Invalid operator in query.");
                    }

                    if (!string.IsNullOrEmpty(condition)) conditions.Add(condition);
                }

            if (conditions.Any())
            {
                sql += " AND (";

                for (var i = 0; i < conditions.Count; i++)
                {
                    if (i > 0)
                    {
                        var connector = request[i].Connector?.ToUpper() == "OR" ? " OR " : " AND ";
                        sql += connector;
                    }

                    sql += conditions[i];
                }

                sql += ")";
            }

            if (!string.IsNullOrWhiteSpace(textSearch))
            {
                // Split query into words and add :* to each for prefix matching
                var processedQuery = string.Join(" & ",
                    textSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(word => word.Trim() + ":*"));
                var processedQueryParam = new NpgsqlParameter("processedQuery", processedQuery);
                var originalQueryParam = new NpgsqlParameter("originalQuery", textSearch);
                parameters.Add(processedQueryParam);
                parameters.Add(originalQueryParam);

                var textSearchCondition = @"
                    AND (
                        to_tsvector('english',
                                coalesce(name, '') || ' ' ||
                                coalesce(description, '') || ' ' ||
                                coalesce(class_name, '') || ' ' ||
                                coalesce(uri, '') || ' ' ||
                                coalesce(original_id, '') || ' ' ||
                                coalesce(data_source_name, '') || ' ' ||
                                coalesce(project_name, '') || ' ' ||
                                coalesce(properties::text, '') || ' ' ||
                                coalesce(tags::text, '')
                            )@@ to_tsquery('english', @processedQuery)
                        OR hr.name ILIKE '%' || @originalQuery || '%'
                        OR hr.description ILIKE '%' || @originalQuery || '%'
                        OR hr.original_id ILIKE '%' || @originalQuery || '%'
                        OR hr.data_source_name ILIKE '%' || @originalQuery || '%'
                        OR hr.project_name ILIKE '%' || @originalQuery || '%'
                        OR hr.class_name ILIKE '%' || @originalQuery || '%'
                    )";

                sql += textSearchCondition;
            }

            // Add ORDER BY
            sql += " ORDER BY hr.record_id, hr.last_updated_at DESC";

            // Execute the query with parameters
            var historicalRecordResults = _context.HistoricalRecords.FromSqlRaw(sql, parameters.ToArray());

            return await historicalRecordResults
                .Select(r => new HistoricalRecordResponseDto
                {
                    Id = r.RecordId,
                    Uri = r.Uri,
                    Properties = r.Properties,
                    OriginalId = r.OriginalId,
                    Name = r.Name,
                    Description = r.Description,
                    ClassId = r.ClassId,
                    ClassName = r.ClassName,
                    DataSourceId = r.DataSourceId,
                    DataSourceName = r.DataSourceName,
                    ObjectStorageId = r.ObjectStorageId,
                    ObjectStorageName = r.ObjectStorageName,
                    ProjectId = r.ProjectId,
                    ProjectName = r.ProjectName,
                    Tags = r.Tags,
                    LastUpdatedBy = r.LastUpdatedBy,
                    LastUpdatedAt = r.LastUpdatedAt
                }).ToListAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "42703") // undefined_column
        {
            throw new ArgumentException(
                "Invalid column name in query. Please check your filter fields against the historical_records table structure.",
                ex);
        }
        catch (PostgresException ex) when (ex.SqlState == "42601") // syntax_error
        {
            throw new ArgumentException("Invalid query syntax. Please check your operators and values.", ex);
        }
        catch (PostgresException ex) when (ex.SqlState == "22P02")
        {
            throw new ArgumentException(
                "Invalid data type in query. Please check that your values match the expected column data types.", ex);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON format in KEY_VALUE operation: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Error executing query: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Full text records search
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="userQuery">String query</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectIds">Project ids that a user has access to</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>A list of historical record response dtos that match provided query parameters</returns>
    public async Task<IEnumerable<HistoricalRecordResponseDto>> Search(
        long currentUserId, string userQuery, long organizationId, long[] projectIds,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
            throw new Exception("Search query is required.");

        // if user is not admin, filter out unauthorized labels
        var authorizedLabelIds = new List<long>();
        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            authorizedLabelIds = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                    currentUserId, organizationId, projectIds, "read record");
        }

        var processedQuery = string.Join(" & ",
            userQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Trim() + ":*"));

        var authorizationFilter = (!isSysAdmin && !isOrgAdmin && !isProjectAdmin) ? @"
            AND (
                NOT EXISTS (
                    SELECT 1 
                    FROM deeplynx.record_labels 
                    WHERE record_id = hr.record_id
                )
                OR
                NOT EXISTS (
                    SELECT 1
                    FROM deeplynx.record_labels rl2
                    WHERE rl2.record_id = hr.record_id
                    AND rl2.label_id != ALL(@authorized_label_ids)
                )
            )" : "";

        var sql = $@"
            SELECT DISTINCT ON (hr.record_id)
            hr.*,
            hr.class_id as ClassId,
            hr.class_name as ClassName,
            hr.original_id as OriginalId,
            hr.data_source_name as DataSourceName,
            hr.data_source_id as DataSourceId,
            hr.project_name as ProjectName,
            hr.project_id as ProjectId,
            hr.last_updated_at as LastUpdatedAt,
            hr.last_updated_by as LastUpdatedBy,
            hr.object_storage_name as ObjectStorageName,
            hr.object_storage_id as ObjectStorageId,
            hr.record_id as RecordId,
            hr.is_archived as IsArchived
        FROM deeplynx.historical_records hr
        WHERE hr.is_archived = false
        AND hr.project_id = ANY(@project_ids)
        AND hr.organization_id = @organization_id
        {authorizationFilter}
        AND (
            to_tsvector('english',
                    coalesce(name, '') || ' ' ||
                    coalesce(description, '') || ' ' ||
                    coalesce(class_name, '') || ' ' ||
                    coalesce(uri, '') || ' ' ||
                    coalesce(original_id, '') || ' ' ||
                    coalesce(data_source_name, '') || ' ' ||
                    coalesce(project_name, '') || ' ' ||
                    coalesce(properties::text, '') || ' ' ||
                    coalesce(tags::text, '')
                ) @@ to_tsquery('english', @processed_query)
            OR hr.name ILIKE '%' || @original_query || '%'
            OR hr.description ILIKE '%' || @original_query || '%'
            OR hr.original_id ILIKE '%' || @original_query || '%'
            OR hr.data_source_name ILIKE '%' || @original_query || '%'
            OR hr.project_name ILIKE '%' || @original_query || '%'
            OR hr.class_name ILIKE '%' || @original_query || '%'
        )
        ORDER BY hr.record_id, hr.last_updated_at DESC";

        var parameters = new List<NpgsqlParameter>
        {
            new NpgsqlParameter("processed_query", processedQuery),
            new NpgsqlParameter("original_query", userQuery),
            new NpgsqlParameter("project_ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = projectIds },
            new NpgsqlParameter("organization_id", organizationId)
        };

        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            parameters.Add(new NpgsqlParameter("authorized_label_ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
            {
                Value = authorizedLabelIds.ToArray()
            });
        }

        var historicalRecordsResults =
            _context.HistoricalRecords.FromSqlRaw(sql, parameters.ToArray());

        return await historicalRecordsResults
            .Select(r => new HistoricalRecordResponseDto
            {
                Id = r.RecordId,
                Uri = r.Uri,
                Properties = r.Properties,
                OriginalId = r.OriginalId,
                Name = r.Name,
                Description = r.Description,
                ClassId = r.ClassId,
                ClassName = r.ClassName,
                DataSourceId = r.DataSourceId,
                DataSourceName = r.DataSourceName,
                ObjectStorageId = r.ObjectStorageId,
                ObjectStorageName = r.ObjectStorageName,
                ProjectId = r.ProjectId,
                ProjectName = r.ProjectName,
                Tags = r.Tags,
                LastUpdatedBy = r.LastUpdatedBy,
                LastUpdatedAt = r.LastUpdatedAt
            }).ToListAsync();
    }

    /// <summary>
    ///     Retrieves current records for projects, ordered by last_updated_at first
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectIds">An array of project ids</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>An array of records</returns>
    public async Task<IEnumerable<HistoricalRecordResponseDto>> GetRecentlyAddedRecords(
        long currentUserId, long organizationId, long[] projectIds,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        if (projectIds.Length == 0)
            return new List<HistoricalRecordResponseDto>();

        var query = _context.HistoricalRecords
            .Where(r => r.OrganizationId == organizationId && !r.IsArchived)
            .Where(r => projectIds.Contains(r.ProjectId));

        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var authorizedLabelIds = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectIds, "read record");

            var authorizedRecordIds = await _context.Records
                .WithAuthorizedLabels(authorizedLabelIds)
                .Select(rec => rec.Id)
                .ToListAsync();

            query = query.Where(r => authorizedRecordIds.Contains(r.RecordId));
        }

        var records = await query
            .GroupBy(r => r.RecordId)
            .Select(g => g.OrderByDescending(r => r.LastUpdatedAt).First())
            .ToListAsync();

        return records.Select(r => new HistoricalRecordResponseDto
        {
            Id = r.RecordId,
            Uri = r.Uri,
            Properties = r.Properties,
            OriginalId = r.OriginalId,
            Name = r.Name,
            ClassId = r.ClassId,
            ClassName = r.ClassName,
            DataSourceId = r.DataSourceId,
            DataSourceName = r.DataSourceName,
            ProjectId = r.ProjectId,
            ProjectName = r.ProjectName,
            Tags = r.Tags,
            Description = r.Description,
            LastUpdatedBy = r.LastUpdatedBy,
            IsArchived = r.IsArchived,
            LastUpdatedAt = r.LastUpdatedAt
        });
    }

    /// <summary>
    ///     Retrieves all records for multiple projects.
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId"> Orginization Id of projects</param>
    /// <param name="projects">Array of project ids whose records are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>A list of records based on the applied filters.</returns>
    public async Task<IEnumerable<HistoricalRecordResponseDto>> GetMultiProjectRecords(
        long currentUserId, long organizationId, long[] projects, bool hideArchived,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        if (projects.Length == 0)
            return new List<HistoricalRecordResponseDto>();

        var projectSet = new HashSet<long>(projects);

        var recordQuery = _context.HistoricalRecords
            .Where(r => projectSet.Contains(r.ProjectId) && r.OrganizationId == organizationId);

        if (hideArchived) recordQuery = recordQuery.Where(r => !r.IsArchived);

        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var authorizedLabelIds = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projects, "read record");

            var authorizedRecordIds = await _context.Records
                .WithAuthorizedLabels(authorizedLabelIds)
                .Select(rec => rec.Id)
                .ToListAsync();

            recordQuery = recordQuery.Where(r => authorizedRecordIds.Contains(r.RecordId));
        }

        var records = await recordQuery
            .GroupBy(e => e.RecordId)
            .Select(g => g.OrderByDescending(r => r.LastUpdatedAt).FirstOrDefault())
            .ToListAsync();

        return records
            .Where(r => r != null)
            .Select(r => new HistoricalRecordResponseDto
            {
                Id = r.RecordId,
                Description = r.Description,
                Uri = r.Uri,
                Properties = r.Properties,
                OriginalId = r.OriginalId,
                Name = r.Name,
                ClassId = r.ClassId,
                ClassName = r.ClassName,
                DataSourceId = r.DataSourceId,
                ProjectId = r.ProjectId,
                LastUpdatedAt = r.LastUpdatedAt,
                LastUpdatedBy = r.LastUpdatedBy,
                IsArchived = r.IsArchived,
                Tags = r.Tags
            });
    }
}