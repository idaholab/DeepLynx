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
    ///     Gets related records up to 3 levels deep
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the record belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The record Id to start</param>
    /// <param name="userId">The user accessing this info</param>
    /// <param name="depth">How many relationships away the user wants</param>
    /// <param name="isSysAdmin">Optional Boolean determining if the requesting user is a sysAdmin</param>
    /// <param name="isOrgAdmin">Optional Boolean determining if the requesting user is an org Admin</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<GraphResponse> GetGraphDataForRecord(
        long organizationId,
        long projectId,
        long recordId,
        long userId,
        int depth,
        bool isSysAdmin = false,
        bool isOrgAdmin = false)
    {
        if (depth > 3) throw new ArgumentException("Depth must be no more than 3");

        var rootRecord = await _context.Records
            .Include(r => r.Class)
            .Include(r => r.Labels)
            .FirstOrDefaultAsync(r => r.Id == recordId);

        if (rootRecord == null) throw new KeyNotFoundException($"Record with id {recordId} not found");

        List<long> userProjectIds = new();
        List<long> userAuthorizedLabels = new();
        List<long> projectAdminIds = new();

        // Sys and org admins bypass all sensitivity label checks.
        // Regular members bypass them only for projects they are project admin of (per-project).
        bool bypassAllLabels = isSysAdmin || isOrgAdmin;

        if (isSysAdmin)
        {
            // Leave userProjectIds null. SysAdmins get a free pass here and in GetGraphEdges.
        }
        else if (isOrgAdmin)
        {
            userProjectIds = await _context.Projects
                .Where(p => p.OrganizationId == organizationId)
                .Select(p => p.Id)
                .ToListAsync();

            if (!userProjectIds.Contains(rootRecord.ProjectId))
                throw new UnauthorizedAccessException(
                    $"You do not have access to view record with id {recordId}");
        }
        else
        {
            userProjectIds = await _context.Projects
                .Where(p => p.ProjectMembers.Any(pm =>
                    pm.UserId == userId ||
                    (pm.GroupId.HasValue && pm.Group != null && pm.Group.Users.Any(u => u.Id == userId))))
                .Select(p => p.Id)
                .ToListAsync();

            if (!userProjectIds.Contains(rootRecord.ProjectId))
                throw new UnauthorizedAccessException(
                    $"You do not have access to view record with id {recordId}");

            // label checks are skipped for records in projects where the user is sysAdmin
            projectAdminIds = await AdminHelper.GetAdminProjectIds(
                _context, userId, organizationId, userProjectIds);

            userAuthorizedLabels = await _sensitivityLabelService.GetAuthorizedSensitivityLabels(
                userId, organizationId, userProjectIds.ToArray(), "read record");

            // Root record label check — skipped if user is project admin of the root's project
            if (!projectAdminIds.Contains(rootRecord.ProjectId) &&
                rootRecord.Labels.Any() &&
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

                var outgoingEdges = await GetGraphEdges(currentLevelRecordId, userProjectIds, userAuthorizedLabels, projectAdminIds, isSysAdmin, isOrgAdmin, isOutgoing: true);
                var incomingEdges = await GetGraphEdges(currentLevelRecordId, userProjectIds, userAuthorizedLabels, projectAdminIds, isSysAdmin, isOrgAdmin, isOutgoing: false);

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
    ///     Gets all edges connected to a specific record from the database
    /// </summary>
    /// <param name="recordId">The ID of the record to get edges for</param>
    /// <param name="userProjectIds">The IDs of the projects the user has access to (ignored for sys admins)</param>
    /// <param name="userAuthorizedLabels">Sensitivity label IDs the user may read (ignored for sys/org admins)</param>
    /// <param name="projectAdminIds">Projects where the user is a project admin; label checks are skipped for records in these projects</param>
    /// <param name="isSysAdmin">Sys admins skip project and label filtering</param>
    /// <param name="isOrgAdmin">Org admins skip label filtering (project scoping still applies)</param>
    /// <param name="isOutgoing">True for edges going OUT from this record, false for edges coming IN</param>
    private async Task<List<Edge>> GetGraphEdges(
        long recordId,
        List<long> userProjectIds,
        List<long> userAuthorizedLabels,
        List<long> projectAdminIds,
        bool isSysAdmin,
        bool isOrgAdmin,
        bool isOutgoing)
    {
        var query = _context.Edges
            .Include(e => e.Origin).ThenInclude(r => r.Labels)
            .Include(e => e.Origin).ThenInclude(r => r.Class)
            .Include(e => e.Destination).ThenInclude(r => r.Labels)
            .Include(e => e.Destination).ThenInclude(r => r.Class)
            .Include(e => e.Relationship)
            .Where(e => !e.IsArchived);

        if (!isSysAdmin)
        {
            query = query.Where(e =>
                userProjectIds.Contains(e.ProjectId) &&
                userProjectIds.Contains(e.Origin.ProjectId) &&
                userProjectIds.Contains(e.Destination.ProjectId));
        }

        if (!isSysAdmin && !isOrgAdmin)
        {
            query = query.Where(e =>
                (projectAdminIds.Contains(e.Origin.ProjectId) ||
                !e.Origin.Labels.Any() ||
                e.Origin.Labels.All(l => userAuthorizedLabels.Contains(l.Id))) &&
                (projectAdminIds.Contains(e.Destination.ProjectId) ||
                !e.Destination.Labels.Any() ||
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