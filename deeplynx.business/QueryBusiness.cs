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
    private readonly IProjectRolePermissionService? _projectRolePermissionService;

    /// <summary>
    ///     Filter record request
    /// </summary>
    /// <param name="context">The database context to be used for filter operations.</param>
    /// <param name="sensitivityLabelService">Helper service for Sensitivity Label Authorization.</param>
    public QueryBusiness(DeeplynxContext context, ISensitivityLabelService sensitivityLabelService, IProjectRolePermissionService? projectRolePermissionService = null)
    {
        _context = context;
        _sensitivityLabelService = sensitivityLabelService;
        _projectRolePermissionService = projectRolePermissionService;
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
    /// <returns>A list of record response dtos from the query view that match provided filters</returns>
    public async Task<IEnumerable<QueryRecordViewResponseDto>> QueryBuilder(
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
                        FROM deeplynx.record_labels rl
                        WHERE rl.record_id = qr.id
                    )
                    OR
                    NOT EXISTS (
                        SELECT 1
                        FROM deeplynx.record_labels rl2
                        WHERE rl2.record_id = qr.id
                        AND rl2.label_id != ALL(@authorizedLabelIds)
                    )
                )" : "";

            var sql = $@"
                SELECT
                    qr.*,
                    qr.class_id as ClassId,
                    qr.class_name as ClassName,
                    qr.original_id as OriginalId,
                    qr.data_source_name as DataSourceName,
                    qr.data_source_id as DataSourceId,
                    qr.project_name as ProjectName,
                    qr.project_id as ProjectId,
                    qr.last_updated_at as LastUpdatedAt,
                    qr.last_updated_by as LastUpdatedBy,
                    qr.object_storage_name as ObjectStorageName,
                    qr.object_storage_id as ObjectStorageId,
                    qr.id as RecordId,
                    qr.is_archived as IsArchived
                FROM deeplynx.query_records qr
                WHERE qr.is_archived = false
                AND qr.project_id = ANY(@projectIds)
                AND qr.organization_id = @organizationId
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
                                    $"EXISTS (SELECT 1 FROM jsonb_array_elements(qr.{query.Filter}) elem WHERE elem->>'name' ILIKE @{paramName})";
                            else
                                // Properties is a flat object already - we can just search the values
                                condition =
                                    $"EXISTS (SELECT 1 FROM jsonb_each_text(qr.{query.Filter}) WHERE value ILIKE @{paramName})";
                        }
                        else
                        {
                            condition = $"qr.{query.Filter} ILIKE @{paramName}";
                        }

                        parameters.Add(new NpgsqlParameter(paramName, $"%{query.Value}%"));
                    }
                    else if (query.Operator == "=")
                    {
                        // Check if this is a JSONB column that needs special handling
                        var jsonbColumns = new[] { "properties", "tags" };

                        if (jsonbColumns.Contains(query.Filter.ToLower()))
                        {
                            if (query.Filter.ToLower() == "tags")
                            {
                                condition = $"EXISTS (SELECT 1 FROM jsonb_array_elements(qr.{query.Filter}) elem WHERE elem->>'name' = @{paramName})";
                                parameters.Add(new NpgsqlParameter(paramName, query.Value));
                            }
                            else
                            {
                                condition = $"jsonb_pretty(qr.{query.Filter}) ILIKE @{paramName}";
                                parameters.Add(new NpgsqlParameter(paramName, $"%{query.Value}%"));
                            }
                        }
                        else
                        {
                            condition = $"qr.{query.Filter} = @{paramName}";
                            if (int.TryParse(query.Value, out var intVal))
                                parameters.Add(new NpgsqlParameter(paramName, intVal));
                            else if (DateTime.TryParse(query.Value, out var dateVal))
                            {
                                var startOfDay = dateVal.Date;
                                var startOfNextDay = dateVal.Date.AddDays(1);
                                var paramName2 = $"p{parameters.Count + 1}";
                                condition = $"qr.{query.Filter} >= @{paramName} AND qr.{query.Filter} < @{paramName2}";
                                parameters.Add(new NpgsqlParameter(paramName, startOfDay));
                                parameters.Add(new NpgsqlParameter(paramName2, startOfNextDay));
                            }
                            else
                                parameters.Add(new NpgsqlParameter(paramName, query.Value));
                        }
                    }
                    else if (query.Operator == ">")
                    {

                        condition = $"qr.{query.Filter} > @{paramName}";

                        if (DateTime.TryParse(query.Value, out var dateVal))
                            parameters.Add(new NpgsqlParameter(paramName, dateVal));
                        else
                            parameters.Add(new NpgsqlParameter(paramName, query.Value));
                    }
                    else if (query.Operator == "<")
                    {
                        condition = $"qr.{query.Filter} < @{paramName}";

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
                        OR qr.name ILIKE '%' || @originalQuery || '%'
                        OR qr.description ILIKE '%' || @originalQuery || '%'
                        OR qr.original_id ILIKE '%' || @originalQuery || '%'
                        OR qr.data_source_name ILIKE '%' || @originalQuery || '%'
                        OR qr.project_name ILIKE '%' || @originalQuery || '%'
                        OR qr.class_name ILIKE '%' || @originalQuery || '%'
                    )";

                sql += textSearchCondition;
            }

            // Add ORDER BY
            sql += " ORDER BY qr.id, qr.last_updated_at DESC";

            // Execute the query with parameters
            var queryRecordResults = _context.QueryRecords.FromSqlRaw(sql, parameters.ToArray());

            var isUriAuthorized = await ExposeUriHelper.GetQueryRecordUriExposer(
                _sensitivityLabelService,
                currentUserId,
                organizationId,
                projectIds,
                isSysAdmin || isOrgAdmin || isProjectAdmin);

            return await queryRecordResults.Select(r => QueryRecordToResponse(r, isUriAuthorized(r))).ToListAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "42703") // undefined_column
        {
            throw new ArgumentException(
                "Invalid column name in query. Please check your filter fields against the query_records view structure.",
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
    ///     Build a paginated query
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="request">Array of query component dtos, initial connector string will be null</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectIds">Project ids that a user has access to</param>
    /// <param name="paginated">Pagination details</param>
    /// <param name="textSearch">Full text search phrase</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>A paginated list of record response dtos from the query view that match provided filters</returns>
    public async Task<PaginatedResponse<QueryRecordViewResponseDto>> QueryBuilderPaginated(
        long currentUserId,
        CustomQueryDtos.CustomQueryRequestDto[] request,
        long organizationId,
        long[] projectIds,
        PaginatedRequestDto paginated,
        string? textSearch = null,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false)
    {
        if (request == null) throw new ArgumentException("Custom query request dto cannot be null");
        try
        {
            if (_projectRolePermissionService == null)
            {
                Console.WriteLine("ProjectRolePermissionService is not available, skipping permission check.");
                return new PaginatedResponse<QueryRecordViewResponseDto>();
            }

            var userProjectAdminStatus = new Dictionary<long, bool>();

            foreach (var projectId in projectIds)
            {
                isProjectAdmin = await _context.ProjectMembers
                    .AnyAsync(pm =>
                        pm.ProjectId == projectId &&
                        pm.IsProjectAdmin &&
                        (
                            (pm.UserId != null && pm.UserId == currentUserId) ||
                            pm.Group.Users.Any(u => u.Id == currentUserId) // group membership
                        )
                    );

                userProjectAdminStatus[projectId] = isProjectAdmin;
            }

            // Filter project IDs based on user permission
            var authorizedProjectIds = new List<long>();
            foreach (var projectId in projectIds)
            {
                if (isSysAdmin || isOrgAdmin || userProjectAdminStatus.GetValueOrDefault(projectId))
                {
                    // Admin access: include project without further permission checks
                    authorizedProjectIds.Add(projectId);
                    continue;
                }

                // Check read permission for non-admin projects
                var hasPermission = await _projectRolePermissionService.PermissionInProject(
                    currentUserId, projectId, "read", "record");

                if (hasPermission)
                    authorizedProjectIds.Add(projectId);
                else
                    Console.WriteLine($"User {currentUserId} lacks read permission on project {projectId}, excluding.");
            }

            if (!authorizedProjectIds.Any())
            {
                Console.WriteLine($"User {currentUserId} has no access to any requested projects.");
                return new PaginatedResponse<QueryRecordViewResponseDto>();
            }

            var nonAdminProjects = authorizedProjectIds
                .Where(p => !userProjectAdminStatus.GetValueOrDefault(p))
                .ToArray();

            List<long> authorizedLabelIds = new List<long>();
            if (nonAdminProjects.Any() && !isSysAdmin && !isOrgAdmin)
            {
                authorizedLabelIds = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                    currentUserId, organizationId, nonAdminProjects, "read record");
            }

            var authorizationFilter = "";
            if (!isSysAdmin && !isOrgAdmin)
            {
                authorizationFilter = @"
                    AND (
                        qr.project_id = ANY(@adminProjects)
                        OR (
                            qr.project_id = ANY(@nonAdminProjects)
                            AND (
                                NOT EXISTS (
                                    SELECT 1 FROM deeplynx.record_labels rl WHERE rl.record_id = qr.id
                                )
                                OR
                                NOT EXISTS (
                                    SELECT 1 FROM deeplynx.record_labels rl2 WHERE rl2.record_id = qr.id AND rl2.label_id != ALL(@authorizedLabelIds)
                                )
                            )
                        )
                    )";
            }

            var sql = $@"
                SELECT
                    qr.*,
                    qr.class_id as ClassId,
                    qr.class_name as ClassName,
                    qr.original_id as OriginalId,
                    qr.data_source_name as DataSourceName,
                    qr.data_source_id as DataSourceId,
                    qr.project_name as ProjectName,
                    qr.project_id as ProjectId,
                    qr.last_updated_at as LastUpdatedAt,
                    qr.last_updated_by as LastUpdatedBy,
                    qr.object_storage_name as ObjectStorageName,
                    qr.object_storage_id as ObjectStorageId,
                    qr.id as RecordId,
                    qr.is_archived as IsArchived
                FROM deeplynx.query_records qr
                WHERE qr.is_archived = false
                AND qr.project_id = ANY(@authorizedProjectIds)
                AND qr.organization_id = @organizationId
                {authorizationFilter}";

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("authorizedProjectIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = authorizedProjectIds.ToArray() },
                new NpgsqlParameter("organizationId", organizationId),
                new NpgsqlParameter("adminProjects", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = authorizedProjectIds.Where(p => userProjectAdminStatus.GetValueOrDefault(p)).ToArray() },
                new NpgsqlParameter("nonAdminProjects", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = nonAdminProjects },
                new NpgsqlParameter("authorizedLabelIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = authorizedLabelIds.ToArray() }
            };

            if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
            {
                parameters.Add(new NpgsqlParameter("authorizedLabelIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
                {
                    Value = authorizedLabelIds.ToArray()
                });
            }

            var conditions = new List<string>();
            if (request?.Length > 0)
            {
                for (var i = 0; i < request.Length; i++)
                {
                    var query = request[i];
                    if (string.IsNullOrWhiteSpace(query.Value) && query.Operator != "KEY_VALUE")
                        throw new ArgumentException("Value cannot be null or empty.");

                    var condition = "";
                    var paramName = $"param{i}";

                    if (query.Operator == "KEY_VALUE")
                    {
                        condition = $"({query.Filter}::jsonb @> @{paramName}::jsonb)";
                        parameters.Add(new NpgsqlParameter(paramName, query.Json));
                    }
                    else if (query.Operator == "LIKE")
                    {
                        var jsonbColumns = new[] { "properties", "tags" };

                        if (jsonbColumns.Contains(query.Filter.ToLower()))
                        {
                            if (query.Filter.ToLower() == "tags")
                                condition = $"EXISTS (SELECT 1 FROM jsonb_array_elements(qr.{query.Filter}) elem WHERE elem->>'name' ILIKE @{paramName})";
                            else
                                condition = $"EXISTS (SELECT 1 FROM jsonb_each_text(qr.{query.Filter}) WHERE value ILIKE @{paramName})";
                        }
                        else
                        {
                            condition = $"qr.{query.Filter} ILIKE @{paramName}";
                        }
                        parameters.Add(new NpgsqlParameter(paramName, $"%{query.Value}%"));
                    }
                    else if (query.Operator == "=")
                    {
                        var jsonbColumns = new[] { "properties", "tags" };

                        if (jsonbColumns.Contains(query.Filter.ToLower()))
                        {
                            if (query.Filter.ToLower() == "tags")
                            {
                                condition = $"EXISTS (SELECT 1 FROM jsonb_array_elements(qr.{query.Filter}) elem WHERE elem->>'name' = @{paramName})";
                                parameters.Add(new NpgsqlParameter(paramName, query.Value));
                            }
                            else
                            {
                                condition = $"jsonb_pretty(qr.{query.Filter}) ILIKE @{paramName}";
                                parameters.Add(new NpgsqlParameter(paramName, $"%{query.Value}%"));
                            }
                        }
                        else
                        {
                            if (int.TryParse(query.Value, out var intVal))
                            {
                                condition = $"qr.{query.Filter} = @{paramName}";
                                parameters.Add(new NpgsqlParameter(paramName, intVal));
                            }
                            else if (DateTime.TryParse(query.Value, out var dateVal))
                            {
                                var startOfDay = dateVal.Date;
                                var startOfNextDay = dateVal.Date.AddDays(1);
                                var paramName2 = $"p{parameters.Count + 1}";
                                condition = $"qr.{query.Filter} >= @{paramName} AND qr.{query.Filter} < @{paramName2}";
                                parameters.Add(new NpgsqlParameter(paramName, startOfDay));
                                parameters.Add(new NpgsqlParameter(paramName2, startOfNextDay));
                            }
                            else
                            {
                                condition = $"qr.{query.Filter} = @{paramName}";
                                parameters.Add(new NpgsqlParameter(paramName, query.Value));
                            }
                        }
                    }
                    else if (query.Operator == ">")
                    {
                        condition = $"qr.{query.Filter} > @{paramName}";
                        if (DateTime.TryParse(query.Value, out var dateVal))
                            parameters.Add(new NpgsqlParameter(paramName, dateVal));
                        else
                            parameters.Add(new NpgsqlParameter(paramName, query.Value));
                    }
                    else if (query.Operator == "<")
                    {
                        condition = $"qr.{query.Filter} < @{paramName}";
                        if (DateTime.TryParse(query.Value, out var dateVal))
                            parameters.Add(new NpgsqlParameter(paramName, dateVal));
                        else
                            parameters.Add(new NpgsqlParameter(paramName, query.Value));
                    }
                    else
                    {
                        throw new ArgumentException("Invalid operator in query.");
                    }

                    if (!string.IsNullOrEmpty(condition))
                        conditions.Add(condition);
                }
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
                var processedQuery = string.Join(" & ",
                    textSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(word => word.Trim() + ":*"));
                parameters.Add(new NpgsqlParameter("processedQuery", processedQuery));
                parameters.Add(new NpgsqlParameter("originalQuery", textSearch));

                sql += @"
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
                            ) @@ to_tsquery('english', @processedQuery)
                        OR qr.name ILIKE '%' || @originalQuery || '%'
                        OR qr.description ILIKE '%' || @originalQuery || '%'
                        OR qr.original_id ILIKE '%' || @originalQuery || '%'
                        OR qr.data_source_name ILIKE '%' || @originalQuery || '%'
                        OR qr.project_name ILIKE '%' || @originalQuery || '%'
                        OR qr.class_name ILIKE '%' || @originalQuery || '%'
                    )";
            }

            sql += " ORDER BY qr.id, qr.last_updated_at DESC";

            var queryRecordResults = _context.QueryRecords.FromSqlRaw(sql, parameters.ToArray());

            var isUriAuthorized = await ExposeUriHelper.GetQueryRecordUriExposer(
                _sensitivityLabelService,
                currentUserId,
                organizationId,
                authorizedProjectIds.ToArray(),
                isSysAdmin || isOrgAdmin || isProjectAdmin);

            return await Paginator.Paginate(paginated, queryRecordResults, r => QueryRecordToResponse(r, isUriAuthorized(r)));
        }
        catch (PostgresException ex) when (ex.SqlState == "42703")
        {
            throw new ArgumentException(
                "Invalid column name in query. Please check your filter fields against the query_records view structure.",
                ex);
        }
        catch (PostgresException ex) when (ex.SqlState == "42601")
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
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>A list of record response dtos from the query view that match provided query parameters</returns>
    public async Task<IEnumerable<QueryRecordViewResponseDto>> Search(
        long currentUserId, string userQuery, long organizationId, long[] projectIds,
        bool hideArchived = true, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
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
                    FROM deeplynx.record_labels rl
                    WHERE rl.record_id = qr.id
                )
                OR
                NOT EXISTS (
                    SELECT 1
                    FROM deeplynx.record_labels rl2
                    WHERE rl2.record_id = qr.id
                    AND rl2.label_id != ALL(@authorized_label_ids)
                )
            )" : "";

        var hideArchivedFilter = hideArchived ? @"
            AND qr.is_archived = false
            " : "";

        var sql = $@"
            SELECT
            qr.*,
            qr.class_id as ClassId,
            qr.class_name as ClassName,
            qr.original_id as OriginalId,
            qr.data_source_name as DataSourceName,
            qr.data_source_id as DataSourceId,
            qr.project_name as ProjectName,
            qr.project_id as ProjectId,
            qr.last_updated_at as LastUpdatedAt,
            qr.last_updated_by as LastUpdatedBy,
            qr.object_storage_name as ObjectStorageName,
            qr.object_storage_id as ObjectStorageId,
            qr.id as RecordId,
            qr.is_archived as IsArchived
        FROM deeplynx.query_records qr
        WHERE qr.project_id = ANY(@project_ids)
        AND qr.organization_id = @organization_id
        {hideArchivedFilter}
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
            OR qr.name ILIKE '%' || @original_query || '%'
            OR qr.description ILIKE '%' || @original_query || '%'
            OR qr.original_id ILIKE '%' || @original_query || '%'
            OR qr.data_source_name ILIKE '%' || @original_query || '%'
            OR qr.project_name ILIKE '%' || @original_query || '%'
            OR qr.class_name ILIKE '%' || @original_query || '%'
        )
        ORDER BY qr.id, qr.last_updated_at DESC";

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

        var queryRecordsResults =
            _context.QueryRecords.FromSqlRaw(sql, parameters.ToArray());

        var isUriAuthorized = await ExposeUriHelper.GetQueryRecordUriExposer(
            _sensitivityLabelService,
            currentUserId,
            organizationId,
            projectIds,
            isSysAdmin || isOrgAdmin || isProjectAdmin);

        return await queryRecordsResults.Select(r => QueryRecordToResponse(r, isUriAuthorized(r))).ToListAsync();
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
    public async Task<IEnumerable<QueryRecordViewResponseDto>> GetRecentlyAddedRecords(
        long currentUserId, long organizationId, long[] projectIds,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        if (projectIds.Length == 0)
            return new List<QueryRecordViewResponseDto>();

        var query = _context.QueryRecords
            .Where(r => r.OrganizationId == organizationId && !r.IsArchived)
            .Where(r => projectIds.Contains(r.ProjectId));

        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var authorizedLabelIds = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectIds, "read record");

            var authorizedRecordIds = _context.Records
                .Where(rec => rec.OrganizationId == organizationId && projectIds.Contains(rec.ProjectId))
                .WithAuthorizedLabels(authorizedLabelIds)
                .Select(rec => rec.Id);

            query = query.Where(r => authorizedRecordIds.Contains(r.Id));
        }

        var records = await query.ToListAsync();

        var isUriAuthorized = await ExposeUriHelper.GetQueryRecordUriExposer(
            _sensitivityLabelService,
            currentUserId,
            organizationId,
            projectIds,
            isSysAdmin || isOrgAdmin || isProjectAdmin);

        return records.Select(r => QueryRecordToResponse(r, isUriAuthorized(r)));
    }

    /// <summary>
    ///     Retrieves current paginated records for projects, ordered as specified
    /// </summary>
    /// <param name="currentUserId">The ID of current user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">An array of project ids</param>
    /// <param name="sortBy">The sorting method</param>
    /// <param name="paginated">Pagination details</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <returns>Paginated records</returns>
    public async Task<PaginatedResponse<QueryRecordViewResponseDto>> GetRecordsPaginated(
        long currentUserId, long organizationId, SortRecordsRequestDto sortBy, PaginatedRequestDto paginated,
        long[] projectId, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        if (projectId.Length == 0)
            return new PaginatedResponse<QueryRecordViewResponseDto> { Items = [], PageSize = paginated.PageSize, PageNumber = paginated.PageNumber };

        var query = _context.QueryRecords
            .Where(r => r.OrganizationId == organizationId && !r.IsArchived)
            .Where(r => projectId.Contains(r.ProjectId));

        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var authorizedLabelIds = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectId, "read record");

            var authorizedRecordIds = _context.Records
                .Where(rec => rec.OrganizationId == organizationId && projectId.Contains(rec.ProjectId))
                .WithAuthorizedLabels(authorizedLabelIds)
                .Select(rec => rec.Id);

            query = query.Where(r => authorizedRecordIds.Contains(r.Id));
        }

        query = sortBy switch
        {
            SortRecordsRequestDto.NameAZ => query.OrderBy(r => (r.Name ?? "").ToLower()).ThenBy(r => r.Id),
            SortRecordsRequestDto.NameZA => query.OrderByDescending(r => (r.Name ?? "").ToLower()).ThenBy(r => r.Id),
            SortRecordsRequestDto.DateNew => query.OrderByDescending(r => r.LastUpdatedAt).ThenBy(r => r.Id),
            SortRecordsRequestDto.DateOld => query.OrderBy(r => r.LastUpdatedAt).ThenBy(r => r.Id),
            _ => query.OrderByDescending(r => r.LastUpdatedAt).ThenBy(r => r.Id), // Sorts date by default
        };


        var isUriAuthorized = await ExposeUriHelper.GetQueryRecordUriExposer(
            _sensitivityLabelService,
            currentUserId,
            organizationId,
            projectId,
            isSysAdmin || isOrgAdmin || isProjectAdmin);

        return await Paginator.Paginate(paginated, query, r => QueryRecordToResponse(r, isUriAuthorized(r)));
    }

    static private QueryRecordViewResponseDto QueryRecordToResponse(
        QueryRecord r, bool exposeUri)
    {
        return new QueryRecordViewResponseDto
        {
            Id = r.Id,
            Uri = exposeUri ? r.Uri : null,
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
            Labels = r.Labels,
            Description = r.Description,
            LastUpdatedBy = r.LastUpdatedBy,
            IsArchived = r.IsArchived,
            LastUpdatedAt = r.LastUpdatedAt
        };
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
    public async Task<IEnumerable<QueryRecordViewResponseDto>> GetMultiProjectRecords(
        long currentUserId, long organizationId, long[] projects, bool hideArchived,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        if (projects.Length == 0)
            return new List<QueryRecordViewResponseDto>();

        var projectSet = new HashSet<long>(projects);

        var recordQuery = _context.QueryRecords
            .Where(r => projectSet.Contains(r.ProjectId) && r.OrganizationId == organizationId);

        if (hideArchived) recordQuery = recordQuery.Where(r => !r.IsArchived);

        if (!isSysAdmin && !isOrgAdmin && !isProjectAdmin)
        {
            var authorizedLabelIds = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projects, "read record");

            var authorizedRecordIds = _context.Records
                .Where(rec => rec.OrganizationId == organizationId && projects.Contains(rec.ProjectId))
                .WithAuthorizedLabels(authorizedLabelIds)
                .Select(rec => rec.Id);

            recordQuery = recordQuery.Where(r => authorizedRecordIds.Contains(r.Id));
        }

        var records = await recordQuery.ToListAsync();

        var isUriAuthorized = await ExposeUriHelper.GetQueryRecordUriExposer(
            _sensitivityLabelService,
            currentUserId,
            organizationId,
            projects,
            isSysAdmin || isOrgAdmin || isProjectAdmin);

        return records.Select(r => QueryRecordToResponse(r, isUriAuthorized(r)));
    }
}