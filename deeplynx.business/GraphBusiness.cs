using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class GraphBusiness : IGraphBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;
    private readonly ISensitivityLabelService _sensitivityLabelService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GraphBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for the Graph operations.</param>
    /// <param name="eventBusiness">Used for logging events during create, update, and delete Operations.</param>
    /// <param name="sensitivityLabelService">Used checking record sensitivity label authorization.</param>
    public GraphBusiness(
        DeeplynxContext context,
        IEventBusiness eventBusiness,
        ISensitivityLabelService sensitivityLabelService)
    {
        _context = context;
        _eventBusiness = eventBusiness;
        _sensitivityLabelService = sensitivityLabelService;
    }

    /// <summary>
    ///     Retrieves all edges for a specific project and (optionally) datasource
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user</param>
    /// <param name="organizationId">The ID of the organization to which the record belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record by which to filter edges</param>
    /// <param name="isOrigin">Indicates whether to find where recordId is origin or not</param>
    /// <param name="page">The ID of the record by which to filter edges</param>
    /// <param name="pageSize">Max size of list to return</param>
    /// <returns>A list of edges based on the applied filters.</returns>
    public async Task<List<RelatedRecordsResponseDto>> GetEdgesByRecord(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        bool isOrigin,
        int page,
        int pageSize)
    {
        if (page < 1) throw new ArgumentException("Page must be greater than 0");
        if (pageSize < 1 || pageSize > 100) throw new ArgumentException("Page size must be between 1 and 100");

        var sourceRecord = await _context.Records
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == recordId
                                    && r.OrganizationId == organizationId
                                    && r.ProjectId == projectId
                                    && !r.IsArchived);

        if (sourceRecord == null) throw new KeyNotFoundException($"Record with id {recordId} not found");

        var isSysAdmin = await AdminHelper.IsSysAdmin(_context, currentUserId);
        var isAdmin = isSysAdmin || await AdminHelper.IsAnyAdmin(_context, currentUserId, organizationId, projectId);

        IQueryable<Edge> edgeQuery = _context.Edges
            .Include(e => e.Relationship)
            .Where(e => !e.IsArchived
                && e.OrganizationId == organizationId
                && e.ProjectId == projectId);

        if (!isSysAdmin)
        {
            var hasProjectAccess = await _context.ProjectMembers.AnyAsync(pm =>
                pm.ProjectId == projectId &&
                (pm.UserId == currentUserId ||
                (pm.GroupId.HasValue && pm.Group != null && pm.Group.Users.Any(u => u.Id == currentUserId))));

            if (!hasProjectAccess && !isAdmin)
                throw new AccessViolationException($"You do not have access to project with id {projectId}");
        }

        if (!isAdmin)
        {
            var userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                currentUserId, organizationId, projectId, "read record");

            if (sourceRecord.Labels.Any() && !sourceRecord.Labels.All(l => userAuthorizedLabels.Contains(l.Id)))
                throw new UnauthorizedAccessException($"Access Denied: You do not have access to all the sensitivity labels on record: {recordId}");

            if (isOrigin)
                edgeQuery = edgeQuery
                    .Include(e => e.Destination).ThenInclude(r => r.Labels)
                    .Where(e => e.OriginId == recordId
                        && !e.Destination.IsArchived
                        && (!e.Destination.Labels.Any()
                            || e.Destination.Labels.All(l => userAuthorizedLabels.Contains(l.Id))));
            else
                edgeQuery = edgeQuery
                    .Include(e => e.Origin).ThenInclude(r => r.Labels)
                    .Where(e => e.DestinationId == recordId
                        && !e.Origin.IsArchived
                        && (!e.Origin.Labels.Any()
                            || e.Origin.Labels.All(l => userAuthorizedLabels.Contains(l.Id))));
        }
        else
        {
            if (isOrigin)
                edgeQuery = edgeQuery
                    .Include(e => e.Destination)
                    .Where(e => e.OriginId == recordId && !e.Destination.IsArchived);
            else
                edgeQuery = edgeQuery
                    .Include(e => e.Origin)
                    .Where(e => e.DestinationId == recordId && !e.Origin.IsArchived);
        }

        return await edgeQuery
            .OrderBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new RelatedRecordsResponseDto
            {
                RelatedRecordName = isOrigin ? e.Destination.Name : e.Origin.Name,
                RelatedRecordId = isOrigin ? e.Destination.Id : e.Origin.Id,
                RelatedRecordProjectId = isOrigin ? e.Destination.ProjectId : e.Origin.ProjectId,
                RelationshipName = e.Relationship != null ? e.Relationship.Name : null
            }).ToListAsync();
    }

    /// <summary>
    ///     Gets related records up to 3 levels deep. Traversal is strictly scoped to
    ///     the root record's own project (and therefore its own organization) — edges
    ///     pointing to records in other projects/orgs are never followed or returned,
    ///     regardless of what the user otherwise has access to.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the record belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The record Id to start</param>
    /// <param name="userId">The user accessing this info</param>
    /// <param name="depth">How many relationships away the user wants</param>
    /// <param name="isAdmin">Whether the requesting user is an admin (sys, org, or project level — resolved upstream by middleware). Admins skip label filtering; project membership/scoping still applies.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<GraphResponse> GetGraphDataForRecord(
        long organizationId,
        long projectId,
        long recordId,
        long userId,
        int depth,
        bool isAdmin = false)
    {
        if (depth > 3) throw new ArgumentException("Depth must be no more than 3");

        var rootRecord = await _context.Records
            .Include(r => r.Class)
            .Include(r => r.Labels)
            .Where(r => r.OrganizationId == organizationId && r.ProjectId == projectId)
            .FirstOrDefaultAsync(r => r.Id == recordId && !r.IsArchived);

        if (rootRecord == null) throw new KeyNotFoundException($"Record with id {recordId} not found");

        List<long> userAuthorizedLabels = new();

        if (!isAdmin)
        {
            userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                userId, organizationId, projectId, "read record");

            if (rootRecord.Labels.Any() &&
                !rootRecord.Labels.All(l => userAuthorizedLabels.Contains(l.Id)))
            {
                throw new UnauthorizedAccessException(
                    $"Access Denied: You do not have access to all the sensitivity labels on record: {recordId}");
            }
        }

        var nodes = new Dictionary<long, GraphNode>();
        var links = new List<GraphLink>();
        var visitedEdges = new HashSet<long>();
        var visitedRecords = new HashSet<long>();

        nodes[recordId] = new GraphNode
        {
            Id = recordId,
            Label = rootRecord.Name,
            Type = "root",
            ClassId = rootRecord.Class?.Id,
            ClassName = rootRecord.Class?.Name
        };

        var currentLevelRecordIds = new List<long> { recordId };

        for (var currentDepth = 0; currentDepth < depth; currentDepth++)
        {
            var nextLevelRecordIds = new List<long>();

            foreach (var currentLevelRecordId in currentLevelRecordIds)
            {
                if (visitedRecords.Contains(currentLevelRecordId)) continue;

                visitedRecords.Add(currentLevelRecordId);

                var outgoingEdges = await GetGraphEdges(currentLevelRecordId, projectId, userAuthorizedLabels, isAdmin, isOutgoing: true);
                var incomingEdges = await GetGraphEdges(currentLevelRecordId, projectId, userAuthorizedLabels, isAdmin, isOutgoing: false);

                ProcessEdges(outgoingEdges, nodes, links, visitedEdges, nextLevelRecordIds, true);
                ProcessEdges(incomingEdges, nodes, links, visitedEdges, nextLevelRecordIds, false);
            }

            currentLevelRecordIds = nextLevelRecordIds;
        }

        return new GraphResponse
        {
            Nodes = nodes.Values.ToList(),
            Links = links
        };
    }


    /// <summary>
    ///     Gets all edges connected to a specific record from the database. Only
    ///     edges whose origin AND destination both live in `projectId` are returned —
    ///     this is what keeps the whole traversal from ever leaving the root's project/org.
    /// </summary>
    /// <param name="recordId">The ID of the record to get edges for</param>
    /// <param name="projectId">The single project this traversal is scoped to (the root record's project)</param>
    /// <param name="userAuthorizedLabels">Sensitivity label IDs the user may read (ignored for admins)</param>
    /// <param name="isAdmin">Admins skip label filtering (project scoping still applies)</param>
    /// <param name="isOutgoing">True for edges going OUT from this record, false for edges coming IN</param>
    private async Task<List<Edge>> GetGraphEdges(
        long recordId,
        long projectId,
        List<long> userAuthorizedLabels,
        bool isAdmin,
        bool isOutgoing)
    {
        var query = _context.Edges
            .Include(e => e.Origin).ThenInclude(r => r.Labels)
            .Include(e => e.Origin).ThenInclude(r => r.Class)
            .Include(e => e.Destination).ThenInclude(r => r.Labels)
            .Include(e => e.Destination).ThenInclude(r => r.Class)
            .Include(e => e.Relationship)
            .Where(e => !e.IsArchived && !e.Origin.IsArchived && !e.Destination.IsArchived);

        // Hard project boundary — every edge and both endpoints must belong to the
        // root's project. This alone is what prevents cross-project/org leakage,
        // regardless of admin status.
        query = query.Where(e =>
            e.ProjectId == projectId &&
            e.Origin.ProjectId == projectId &&
            e.Destination.ProjectId == projectId);

        if (!isAdmin)
        {
            query = query.Where(e =>
                (!e.Origin.Labels.Any() ||
                e.Origin.Labels.All(l => userAuthorizedLabels.Contains(l.Id))) &&
                (!e.Destination.Labels.Any() ||
                e.Destination.Labels.All(l => userAuthorizedLabels.Contains(l.Id))));
        }

        query = isOutgoing
            ? query.Where(e => e.OriginId == recordId)
            : query.Where(e => e.DestinationId == recordId);

        return await query.ToListAsync();
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
                    Type = "node",
                    ClassId = connectedRecord.Class?.Id,
                    ClassName = connectedRecord.Class?.Name
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
}