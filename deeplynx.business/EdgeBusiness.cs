using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace deeplynx.business;

public class EdgeBusiness : IEdgeBusiness
{
    private readonly IBulkCopyUpsertExecutor _bulkCopyUpsertExecutor;
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EdgeBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the edge operations.</param>
    /// <param name="eventBusiness">Used for logging events during create, update, and delete Operations.</param>
    public EdgeBusiness(
        DeeplynxContext context, IEventBusiness eventBusiness,
        IBulkCopyUpsertExecutor bulkCopyUpsertExecutor)
    {
        _context = context;
        _eventBusiness = eventBusiness;
        _bulkCopyUpsertExecutor = bulkCopyUpsertExecutor;
    }

    /// <summary>
    ///     Retrieves all edges for a specific project and (optionally) datasource
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose edges are to be retrieved</param>
    /// <param name="dataSourceId">(Optional) The ID of the datasource by which to filter edges</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived edges from the result</param>
    /// <returns>A list of edges based on the applied filters.</returns>
    public async Task<List<EdgeResponseDto>> GetAllEdges(
        long organizationId,
        long projectId,
        long? dataSourceId,
        bool hideArchived)
    {
        var edgeQuery = _context.Edges
            .Where(e => e.ProjectId == projectId && e.OrganizationId == organizationId);

        if (hideArchived) edgeQuery = edgeQuery.Where(e => e.IsArchived == false);

        var edges = await edgeQuery.ToListAsync();

        return edges
            .Select(e => new EdgeResponseDto
            {
                Id = e.Id,
                OriginId = e.OriginId,
                DestinationId = e.DestinationId,
                RelationshipId = e.RelationshipId,
                DataSourceId = e.DataSourceId,
                ProjectId = e.ProjectId,
                OrganizationId = e.OrganizationId,
                LastUpdatedAt = e.LastUpdatedAt,
                LastUpdatedBy = e.LastUpdatedBy,
                IsArchived = e.IsArchived
            }).ToList();
    }

    /// <summary>
    ///     Retrieves a specific edge by its origin and destination IDs
    ///     OR Retrieves an edge by its id
    /// </summary>
    /// <param name="projectId">The project of the edge to retrieve</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="edgeId">The id whereby to fetch the edge</param>
    /// <param name="originId">the origin ID by which to fetch the edge if no ID</param>
    /// <param name="destinationId">the destination ID by which to fetch the edge if no ID</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived edges from the result</param>
    /// <returns>The edge associated with the given id or origin/destination combo</returns>
    /// <exception cref="KeyNotFoundException">Returned if edge not found or is archived</exception>
    public async Task<EdgeResponseDto> GetEdge(
        long organizationId,
        long projectId,
        long? edgeId,
        long? originId,
        long? destinationId,
        bool hideArchived)
    {
        var edge = await FindEdge(organizationId, edgeId, originId, destinationId);

        if (edge == null) throw new KeyNotFoundException($"Edge with id {edgeId} not found");

        if (edge.ProjectId != projectId)
            throw new KeyNotFoundException($"Edge with id {edgeId} not found in project {projectId}");

        if (hideArchived && edge.IsArchived) throw new KeyNotFoundException($"Edge with id {edgeId} is archived");

        return new EdgeResponseDto
        {
            Id = edge.Id,
            OriginId = edge.OriginId,
            DestinationId = edge.DestinationId,
            RelationshipId = edge.RelationshipId,
            DataSourceId = edge.DataSourceId,
            ProjectId = edge.ProjectId,
            OrganizationId = edge.OrganizationId,
            LastUpdatedAt = edge.LastUpdatedAt,
            LastUpdatedBy = edge.LastUpdatedBy,
            IsArchived = edge.IsArchived
        };
    }

    /// <summary>
    ///     Asynchronously creates a new edge for a specified project.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the edge belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the edge belongs</param>
    /// <param name="dto">The edge request data transfer object containing edge details</param>
    /// <returns>The created edge response DTO with saved details.</returns>
    public async Task<EdgeResponseDto> CreateEdge(
        long currentUserId,
        long organizationId,
        long projectId,
        long dataSourceId,
        CreateEdgeRequestDto dto)
    {
        if (!dto.OriginId.HasValue || !dto.DestinationId.HasValue)
            throw new ValidationException("Origin and/or Destination IDs are missing or invalid.");

        if (dto.OriginId == dto.DestinationId)
            throw new ValidationException("Destination and origin IDs cannot be the same");

        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);

        var originRecordExists = _context.Records.Any(r => r.Id == dto.OriginId);
        if (!originRecordExists) throw new KeyNotFoundException($"Origin record with id {dto.OriginId} not found");

        var destinationRecordExists = _context.Records.Any(r => r.Id == dto.DestinationId);
        if (!destinationRecordExists)
            throw new KeyNotFoundException($"Destination record with id {dto.DestinationId} not found");

        var edge = new Edge
        {
            OriginId = dto.OriginId.Value,
            DestinationId = dto.DestinationId.Value,
            ProjectId = projectId,
            DataSourceId = dataSourceId,
            RelationshipId = dto.RelationshipId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = currentUserId,
            OrganizationId = organizationId
        };

        _context.Edges.Add(edge);
        await _context.SaveChangesAsync();

        // log edge create event
        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "edge",
                EntityId = edge.Id,
                DataSourceId = edge.DataSourceId,
                Properties = JsonSerializer.Serialize(new
                {
                    origin = edge.OriginId,
                    destination = edge.DestinationId
                }) // TODO: Determine the extent of data edge properties need
            });

        return new EdgeResponseDto
        {
            Id = edge.Id,
            OriginId = edge.OriginId,
            DestinationId = edge.DestinationId,
            RelationshipId = edge.RelationshipId,
            DataSourceId = edge.DataSourceId,
            ProjectId = edge.ProjectId,
            OrganizationId = edge.OrganizationId,
            LastUpdatedAt = edge.LastUpdatedAt,
            LastUpdatedBy = edge.LastUpdatedBy
        };
    }

    /// <summary>
    ///     Asynchronously creates new edges for a specified project.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the edge belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the edge belongs</param>
    /// <param name="edges">Enumerable list of edge request data transfer objects containing edge details</param>
    /// <returns>Enumerable list of created edges</returns>
   public async Task<List<EdgeResponseDto>> BulkCreateEdges(
    long currentUserId,
    long organizationId,
    long projectId,
    long dataSourceId,
    List<CreateEdgeRequestDto> edges)
{
    await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);
    var conn = (NpgsqlConnection)_context.Database.GetDbConnection();
    if (conn.State != ConnectionState.Open) await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    var createTempSql = @"
    CREATE TEMP TABLE tmp_edges
    (
        organization_id   BIGINT NOT NULL,
        project_id        BIGINT NOT NULL,
        data_source_id    BIGINT NOT NULL,
        origin_id         BIGINT NOT NULL,
        destination_id    BIGINT NOT NULL,
        relationship_id   BIGINT NULL,
        last_updated_at   TIMESTAMP WITHOUT TIME ZONE NOT NULL,
        is_archived       BOOLEAN NOT NULL
    ) ON COMMIT DROP;";

    var copyCmd = @"
    COPY tmp_edges (organization_id, project_id, data_source_id, origin_id, destination_id, relationship_id, last_updated_at, is_archived)
    FROM STDIN (FORMAT BINARY)";

    var upsertSql = @"
    INSERT INTO deeplynx.edges
    (organization_id, project_id, data_source_id, origin_id, destination_id, relationship_id, last_updated_at, is_archived)
    SELECT organization_id, project_id, data_source_id, origin_id, destination_id, relationship_id, last_updated_at, is_archived
    FROM tmp_edges
    ON CONFLICT (project_id, origin_id, destination_id) DO UPDATE
      SET relationship_id = COALESCE(EXCLUDED.relationship_id, edges.relationship_id),
          last_updated_at = EXCLUDED.last_updated_at
    RETURNING id, organization_id, project_id, data_source_id, origin_id, destination_id, relationship_id;";

    var result = await _bulkCopyUpsertExecutor.CopyUpsertAsync(
        conn, tx,
        createTempSql,
        copyCmd,
        edges,
        (w, e) =>
        {
            w.Write(organizationId, NpgsqlDbType.Bigint);
            w.Write(projectId, NpgsqlDbType.Bigint);
            w.Write(dataSourceId, NpgsqlDbType.Bigint);
            w.Write(e.OriginId!.Value, NpgsqlDbType.Bigint);
            w.Write(e.DestinationId!.Value, NpgsqlDbType.Bigint);
            if (e.RelationshipId.HasValue) w.Write(e.RelationshipId.Value, NpgsqlDbType.Bigint);
            else w.WriteNull();
            w.Write(now, NpgsqlDbType.Timestamp);
            w.Write(false, NpgsqlDbType.Boolean);
        },
        upsertSql,
        MapEdge
    );

        // events logging
        var createEvent = new CreateEventRequestDto
        {
            Operation = "create",
            EntityType = "edge",
            DataSourceId = dataSourceId
        };
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, createEvent, result.Count);

    await tx.CommitAsync();
    return result;
}

    /// <summary>
    ///     Updates an existing edge by its ID or origin/destination.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the edge belongs.</param>
    /// <param name="dto">The edge request data transfer object containing updated edge details.</param>
    /// <param name="edgeId">The ID of the edge to update</param>
    /// <param name="originId">The origin ID of the edge to update if edgeID is not present.</param>
    /// <param name="destinationId">The destination ID of the edge if edgeID is not present.</param>
    /// <returns>The updated edge response DTO with its details</returns>
    /// <exception cref="KeyNotFoundException">Returned if edge not found or if ids missing</exception>
    public async Task<EdgeResponseDto> UpdateEdge(
        long currentUserId,
        long organizationId,
        long projectId,
        UpdateEdgeRequestDto dto,
        long? edgeId,
        long? originId,
        long? destinationId
    )
    {
        var edge = await FindEdge(organizationId, edgeId, originId, destinationId);
        if (edge == null || edge.ProjectId != projectId || edge.IsArchived)
            throw new KeyNotFoundException("Edge may have been moved or deleted.");

        edge.OriginId = dto.OriginId ?? edge.OriginId;
        edge.DestinationId = dto.DestinationId ?? edge.DestinationId;
        edge.RelationshipId = dto.RelationshipId ?? edge.RelationshipId;
        edge.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        edge.LastUpdatedBy = currentUserId;

        if (edge.OriginId == edge.DestinationId)
            throw new ValidationException("Destination and origin Ids can not be the same.");

        _context.Edges.Update(edge);
        await _context.SaveChangesAsync();

        // log edge update event
        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "update",
                EntityType = "edge",
                EntityId = edge.Id,
                DataSourceId = edge.DataSourceId,
                Properties = "{}" // TODO: Determine the extent of data edge properties need
            });

        return new EdgeResponseDto
        {
            Id = edge.Id,
            OriginId = edge.OriginId,
            DestinationId = edge.DestinationId,
            RelationshipId = edge.RelationshipId,
            DataSourceId = edge.DataSourceId,
            ProjectId = edge.ProjectId,
            OrganizationId = edge.OrganizationId,
            LastUpdatedAt = edge.LastUpdatedAt,
            LastUpdatedBy = edge.LastUpdatedBy,
            IsArchived = edge.IsArchived
        };
    }

    /// <summary>
    ///     Deletes a specific edge by its ID or origin/destination.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the edge belongs.</param>
    /// <param name="edgeId">The ID of the edge to delete</param>
    /// <param name="originId">The origin ID of the edge to delete if edgeID is not present.</param>
    /// <param name="destinationId">The destination ID of the edge if edgeID is not present.</param>
    /// <exception cref="KeyNotFoundException">Returned if edge not found or if ids missing</exception>
    /// TODO: return warning that historical data will be entirely wiped with this action
    public async Task<long> DeleteEdge(
        long currentUserId,
        long organizationId,
        long projectId,
        long? edgeId,
        long? originId,
        long? destinationId)
    {
        var edge = await FindEdge(organizationId, edgeId, originId, destinationId);
        if (edge == null || edge.ProjectId != projectId)
            throw new KeyNotFoundException("Edge may have been moved or deleted.");

        var edgeDataSourceId = edge.DataSourceId;

        _context.Edges.Remove(edge);
        await _context.SaveChangesAsync();

        // log edge delete event
        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "delete",
            EntityType = "edge",
            EntityId = edgeId,
            DataSourceId = edgeDataSourceId,
            Properties = "{}" // TODO: Determine the extent of data edge properties need
        });

        return edge.Id;
    }

    /// <summary>
    ///     Archives a specific edge by its ID or origin/destination.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the edge belongs.</param>
    /// <param name="edgeId">The ID of the edge to archive</param>
    /// <param name="originId">The origin ID of the edge to archive if edgeID is not present.</param>
    /// <param name="destinationId">The destination ID of the edge if edgeID is not present.</param>
    /// <returns>The ID of the edge that was archived.</returns>
    /// <exception cref="KeyNotFoundException">Returned if edge not found or if ids missing</exception>
    public async Task<long> ArchiveEdge(
        long currentUserId,
        long organizationId,
        long projectId,
        long? edgeId,
        long? originId,
        long? destinationId)
    {
        // find edge and perform error handling if not found
        var edge = await FindEdge(organizationId, edgeId, originId, destinationId);
        if (edge == null || edge.ProjectId != projectId || edge.IsArchived)
            throw new KeyNotFoundException("Edge may have been moved, archived or deleted.");

        edge.IsArchived = true;
        edge.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        edge.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();

        // Log Edge soft Delete Event
        await _eventBusiness.CreateEvent(
            currentUserId,
            organizationId,
            projectId,
            new CreateEventRequestDto
            {
                Operation = "archive",
                EntityType = "edge",
                EntityId = edgeId,
                DataSourceId = edge.DataSourceId,
                Properties = "{}" // TODO: Determine the extent of data edge properties need
            });

        return edge.Id;
    }

    /// <summary>
    ///     Unarchives a specific edge by its ID or origin/destination.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the edge belongs.</param>
    /// <param name="edgeId">The ID of the edge to unarchive</param>
    /// <param name="originId">The origin ID of the edge to unarchive if edgeID is not present.</param>
    /// <param name="destinationId">The destination ID of the edge to unarchive if edgeID is not present.</param>
    /// <returns>The ID of the edge that was unarchived.</returns>
    /// <exception cref="KeyNotFoundException">Returned if edge not found or if ids missing</exception>
    public async Task<long> UnarchiveEdge(
        long currentUserId,
        long organizationId,
        long projectId,
        long? edgeId,
        long? originId,
        long? destinationId)
    {
        var edge = await FindEdge(organizationId, edgeId, originId, destinationId);
        if (edge == null || edge.ProjectId != projectId || !edge.IsArchived)
            throw new KeyNotFoundException("Edge to unarchive not found or is not archived.");

        edge.IsArchived = false;
        edge.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        edge.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();

        await _eventBusiness.CreateEvent(currentUserId, organizationId, projectId, new CreateEventRequestDto
        {
            Operation = "unarchive",
            EntityType = "edge",
            EntityId = edgeId,
            DataSourceId = edge.DataSourceId,
            Properties = "{}" // TODO: Determine the extent of data edge properties need
        });

        return edge.Id;
    }

    /// <summary>
    ///     Processes a list of edges, adding new nodes and links to our graph data structures
    /// </summary>
    /// <param name="edges">The edges to process</param>
    /// <param name="nodes">Dictionary of all nodes in the graph (we add to this)</param>
    /// <param name="links">List of all links in the graph (we add to this)</param>
    /// <param name="visitedEdges">Set of edge IDs we've already processed (prevents duplicates)</param>
    /// <param name="nextLevelRecords">List to add newly discovered node IDs to (for next depth level)</param>
    /// <param name="isOutgoing">True if these are outgoing edges, False if incoming</param>
    private void ProcessEdges(
        List<Edge> edges,
        Dictionary<long, GraphNode> nodes,
        List<GraphLink> links,
        HashSet<long> visitedEdges,
        List<long> nextLevelRecords,
        bool isOutgoing)
    {
        foreach (var edge in edges)
        {
            // Skip if edge already visited
            if (visitedEdges.Contains(edge.Id)) continue;

            visitedEdges.Add(edge.Id);

            // Figure out which record is on the other side of this edge
            var connectedRecordId = isOutgoing ? edge.DestinationId : edge.OriginId;
            var connectedRecord = isOutgoing ? edge.Destination : edge.Origin;

            // If this is a new node, add it to the graph
            if (!nodes.ContainsKey(connectedRecordId))
            {
                nodes[connectedRecordId] = new GraphNode
                {
                    Id = connectedRecordId,
                    Label = connectedRecord.Name,
                    Type = "node"
                };

                // Add this node to the list of nodes to explore in the next depth level
                nextLevelRecords.Add(connectedRecordId);
            }

            // Add the link between nodes to the graph
            // Note: we always store Source -> Target in the original edge direction
            links.Add(new GraphLink
            {
                Source = edge.OriginId,
                Target = edge.DestinationId,
                RelationshipId = edge.RelationshipId,
                RelationshipName = edge.Relationship?.Name,
                EdgeId = edge.Id
            });
        }
    }

    /// <summary>
    ///     Private method to facilitate boilerplate code for finding edges by ID or origin/destination
    /// </summary>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="edgeId">The id whereby to fetch the edge</param>
    /// <param name="originId">The origin ID by which to fetch the edge if no ID</param>
    /// <param name="destinationId">The destination ID by which to fetch the edge if no ID</param>
    /// <returns>The edge associated with the given id or origin/destination combo</returns>
    /// <exception cref="KeyNotFoundException">Returned if edge not found or if ids missing</exception>
    private async Task<Edge> FindEdge(
        long organizationId,
        long? edgeId,
        long? originId,
        long? destinationId
    )
    {
        if (edgeId == null && (originId == null || destinationId == null))
            throw new KeyNotFoundException("Please supply either an edgeID or an originID and destinationID");

        Edge edge = null;

        // search for edge either by id or origin + destination
        if (edgeId != null)
            edge = await _context.Edges
                .FirstOrDefaultAsync(e => e.Id == edgeId && e.OrganizationId == organizationId);
        else
            edge = await _context.Edges
                .FirstOrDefaultAsync(e =>
                    e.OriginId == originId && e.DestinationId == destinationId && e.OrganizationId == organizationId);

        // throw an error if edge not found
        if (edge == null)
        {
            if (edgeId != null) throw new KeyNotFoundException($"Edge with id {edgeId} not found");

            throw new KeyNotFoundException(
                $"Edge with origin {originId} and destination {destinationId} not found");
        }

        return edge;
    }


    /// <summary>
    ///     Map an NPGSQL data reader to a return DTO usually during high scale read operations
    /// </summary>
    /// <param name="r">NPGSQL reader object containing DTO params</param>
    /// <returns>A response data transfer object with fields mapped from the pg reader</returns>
    private static EdgeResponseDto MapEdge(NpgsqlDataReader r)
    {
        var iId = r.GetOrdinal("id");
        var iProj = r.GetOrdinal("project_id");
        var iDs = r.GetOrdinal("data_source_id");
        var iOrig = r.GetOrdinal("origin_id");
        var iDest = r.GetOrdinal("destination_id");
        var iRel = r.GetOrdinal("relationship_id");

        return new EdgeResponseDto
        {
            Id = r.GetInt64(iId),
            ProjectId = r.GetInt64(iProj),
            DataSourceId = r.GetInt64(iDs),
            OriginId = r.GetInt64(iOrig),
            DestinationId = r.GetInt64(iDest),
            RelationshipId = r.IsDBNull(iRel) ? null : r.GetInt64(iRel)
        };
    }
}