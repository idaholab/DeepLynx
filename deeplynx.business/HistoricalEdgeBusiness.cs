using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class HistoricalEdgeBusiness : IHistoricalEdgeBusiness
{
    private readonly DeeplynxContext _context;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HistoricalEdgeBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the edge operations.</param>
    public HistoricalEdgeBusiness(DeeplynxContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Retrieves all Historical Edges for a specific project and datasource
    /// </summary>
    /// <param name="projectId">The ID of the project whose edges are to be retrieved</param>
    /// <param name="dataSourceId">(Optional) The ID of the datasource by which to filter edges</param>
    /// <param name="pointInTime">(Optional) Find the most current edges that existed before this point in time</param>
    /// <param name="hideArchived">(Optional) Flag indicating whether to hide archived edges from the result.</param>
    /// <returns>An array of edges</returns>
    /// TODO: create an endpoint for this
    public async Task<IEnumerable<HistoricalEdgeResponseDto>> GetAllHistoricalEdges(
        long projectId,
        long? dataSourceId = null,
        DateTime? pointInTime = null,
        bool hideArchived = true)
    {
        var edgeQuery = _context.HistoricalEdges
            .Where(e => e.ProjectId == projectId);

        if (dataSourceId.HasValue) edgeQuery = edgeQuery.Where(e => e.DataSourceId == dataSourceId);

        // specification for "current" should override any supplied pointInTime
        if (pointInTime.HasValue)
        {
            // convert the point in time to timestamp without timezone
            var unspecifiedPointInTime = DateTime.SpecifyKind(pointInTime.Value, DateTimeKind.Unspecified);

            // compare the timestamp to the most recent update
            edgeQuery = edgeQuery
                .Where(r => r.LastUpdatedAt <= unspecifiedPointInTime)
                .OrderByDescending(r => r.LastUpdatedAt);
        }

        var edges = await edgeQuery
            .GroupBy(e => e.EdgeId)
            .Select(g => g.OrderByDescending(e => e.LastUpdatedAt).FirstOrDefault())
            .ToListAsync();

        // Need to check for ArchivedAt after DB retrieval since filtering archived results before querying could
        // result in inaccurate "most recent" results if an edge has been archived
        if (hideArchived && edges.Count > 0) edges = edges.Where(e => !e.IsArchived).ToList();

        return edges
            .Select(e => new HistoricalEdgeResponseDto
            {
                Id = e.EdgeId,
                OriginId = e.OriginId,
                DestinationId = e.DestinationId,
                RelationshipId = e.RelationshipId,
                RelationshipName = e.RelationshipName,
                DataSourceId = e.DataSourceId,
                DataSourceName = e.DataSourceName ?? string.Empty,
                ProjectId = e.ProjectId,
                ProjectName = e.ProjectName ?? string.Empty,
                LastUpdatedAt = e.LastUpdatedAt,
                LastUpdatedBy = e.LastUpdatedBy,
                IsArchived = e.IsArchived
            });
    }

    /// <summary>
    ///     Show the historical updates of a specific edge
    /// </summary>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="edgeId">The ID of the edge to list history for</param>
    /// <param name="originId">the origin ID by which to fetch the edge if no ID</param>
    /// <param name="destinationId">the destination ID by which to fetch the edge if no ID</param>
    /// <returns>An array of edge instances for the given edge</returns>
    /// TODO: create an endpoint for this
    public async Task<IEnumerable<HistoricalEdgeResponseDto>> GetHistoryForEdge(
        long organizationId,
        long? edgeId,
        long? originId,
        long? destinationId)
    {
        var foundEdge = await FindEdge(organizationId, edgeId, originId, destinationId);
        var foundEdgeId = foundEdge.EdgeId;

        return await _context.HistoricalEdges
            .Where(e => e.EdgeId == foundEdgeId)
            .OrderByDescending(e => e.LastUpdatedAt)
            .Select(e => new HistoricalEdgeResponseDto
            {
                Id = e.EdgeId,
                OriginId = e.OriginId,
                DestinationId = e.DestinationId,
                RelationshipId = e.RelationshipId,
                RelationshipName = e.RelationshipName,
                DataSourceId = e.DataSourceId,
                DataSourceName = e.DataSourceName ?? string.Empty,
                ProjectId = e.ProjectId,
                ProjectName = e.ProjectName ?? string.Empty,
                LastUpdatedBy = e.LastUpdatedBy,
                IsArchived = e.IsArchived,
                LastUpdatedAt = e.LastUpdatedAt
            })
            .ToListAsync();
    }

    /// <summary>
    ///     Find an edge at a given point in time
    /// </summary>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="edgeId">The ID of the edge to retrieve</param>
    /// <param name="originId">the origin ID by which to fetch the edge if no ID</param>
    /// <param name="destinationId">the destination ID by which to fetch the edge if no ID</param>
    /// <param name="pointInTime">(Optional) Find the most current edge that existed before this point in time</param>
    /// <param name="hideArchived">(Optional) Flag indicating whether to hide archived edges from the result.</param>
    /// <returns>An edge that matches the applied filters.</returns>
    /// <exception cref="KeyNotFoundException">Returned if edge not found</exception>
    /// /// TODO: create an endpoint for this
    public async Task<HistoricalEdgeResponseDto> GetHistoricalEdge(
        long organizationId,
        long? edgeId,
        long? originId,
        long? destinationId,
        DateTime? pointInTime,
        bool hideArchived = true)
    {
        var foundEdge = await FindEdge(organizationId, edgeId, originId, destinationId);
        var foundEdgeId = foundEdge.EdgeId;

        var edgeQuery = _context.HistoricalEdges
            .Where(e => e.EdgeId == foundEdgeId)
            .OrderByDescending(e => e.LastUpdatedAt);

        // specification for "current" should override any supplied pointInTime
        if (pointInTime.HasValue)
        {
            // convert the point in time to timestamp without timezone
            var unspecifiedPointInTime = DateTime.SpecifyKind(pointInTime.Value, DateTimeKind.Unspecified);

            // compare the timestamp to the most recent update
            edgeQuery = edgeQuery
                .Where(r => r.LastUpdatedAt <= unspecifiedPointInTime)
                .OrderByDescending(r => r.LastUpdatedAt);
        }

        var edge = await edgeQuery.FirstOrDefaultAsync();

        if (edge == null)
            throw new KeyNotFoundException($"Edge with id {foundEdgeId} not found at point in time {pointInTime}.");

        if (hideArchived && edge.IsArchived)
            throw new KeyNotFoundException($"Edge with id {foundEdgeId} not found or archived.");

        return new HistoricalEdgeResponseDto
        {
            Id = edge.EdgeId,
            OriginId = edge.OriginId,
            DestinationId = edge.DestinationId,
            RelationshipId = edge.RelationshipId,
            RelationshipName = edge.RelationshipName,
            DataSourceId = edge.DataSourceId,
            DataSourceName = edge.DataSourceName ?? string.Empty,
            ProjectId = edge.ProjectId,
            ProjectName = edge.ProjectName ?? string.Empty,
            LastUpdatedBy = edge.LastUpdatedBy,
            IsArchived = edge.IsArchived,
            LastUpdatedAt = edge.LastUpdatedAt
        };
    }

    /// <summary>
    ///     Private method to facilitate boilerplate code for finding edges by ID or origindestination
    /// </summary>
    /// <param name="organizationId">The ID of the organization under which project exists</param>
    /// <param name="edgeId">The id whereby to fetch the edge</param>
    /// <param name="originId">The origin ID by which to fetch the edge if no ID</param>
    /// <param name="destinationId">The destination ID by which to fetch the edge if no ID</param>
    /// <param name="historical">Boolean indicating whether to look for a historical edge</param>
    /// <returns>The edge associated with the given id or origin/destination combo</returns>
    /// <exception cref="KeyNotFoundException">Returned if edge not found or if ids missing</exception>
    private async Task<HistoricalEdge> FindEdge(
        long organizationId,
        long? edgeId,
        long? originId,
        long? destinationId
    )
    {
        if (edgeId == null && (originId == null || destinationId == null))
            throw new KeyNotFoundException("Please supply either an edgeID or an originID and destinationID");

        HistoricalEdge edge = null;

        // search for edge either by id or origin + destination
        if (edgeId != null)
            edge = await _context.HistoricalEdges
                .Where(e => e.EdgeId == edgeId && e.OrganizationId == organizationId)
                .OrderByDescending(e => e.LastUpdatedAt)
                .FirstOrDefaultAsync();
        else
            edge = await _context.HistoricalEdges
                .Where(e => e.OriginId == originId && e.DestinationId == destinationId &&
                            e.OrganizationId == organizationId)
                .OrderByDescending(e => e.LastUpdatedAt)
                .FirstOrDefaultAsync();

        // throw an error if edge not found
        if (edge == null)
        {
            if (edgeId != null) throw new KeyNotFoundException($"Historical edge with id {edgeId} not found");

            throw new KeyNotFoundException(
                $"Historical edge with origin {originId} and destination {destinationId} not found");
        }

        return edge;
    }
}
