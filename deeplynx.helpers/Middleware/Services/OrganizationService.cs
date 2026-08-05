using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers;

public interface IOrganizationService
{
    /// <summary>
    /// Checks existence and validity of organization and optionally a single project.
    /// </summary>
    Task<long> CheckExistence(long? projectId, long? organizationId, bool includeArchived = false);

    /// <summary>
    /// Resolves and validates organization ID from multiple project IDs, ensuring all projects belong to the same org.
    /// Throws if multiple orgs found or mismatch with provided org ID.
    /// </summary>
    Task<long> ResolveOrganizationIdFromProjectsAsync(IEnumerable<long> projectIds, long? organizationId, bool includeArchived = false);
}

public class OrganizationService : IOrganizationService
{
    private readonly DeeplynxContext _dbContext;
    private readonly ILogger<AdminService> _logger;

    public OrganizationService(
        DeeplynxContext dbContext,
        ILogger<AdminService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<long> CheckExistence(long? projectId, long? organizationId, bool includeArchived = false)
    {
        if (organizationId.HasValue)
            await ExistenceHelper.EnsureOrganizationExistsAsync(
                _dbContext,
                organizationId.Value,
                !includeArchived
            );

        if (projectId.HasValue)
        {
            await ExistenceHelper.EnsureProjectExistsAsync(
                _dbContext,
                projectId.Value,
                !includeArchived
            );

            var projectQuery = _dbContext.Projects.AsQueryable();

            if (!includeArchived) projectQuery = projectQuery.Where(p => !p.IsArchived);

            var project = await projectQuery.FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null) throw new KeyNotFoundException($"Project with ID {projectId} not found");

            if (organizationId.HasValue && project.OrganizationId != organizationId.Value)
                throw new InvalidOperationException(
                    $"Project {projectId} does not belong to organization {organizationId.Value}");

            _logger.LogInformation("Organization found - Organization: {OrganizationId}", project.OrganizationId);

            return project.OrganizationId;
        }

        return organizationId.GetValueOrDefault();
    }

    public async Task<long> ResolveOrganizationIdFromProjectsAsync(IEnumerable<long> projectIds, long? organizationId, bool includeArchived = false)
    {
        if (organizationId.HasValue)
            await ExistenceHelper.EnsureOrganizationExistsAsync(
                _dbContext,
                organizationId.Value,
                !includeArchived
            );

        var projectQuery = _dbContext.Projects.AsQueryable();

        if (!includeArchived)
            projectQuery = projectQuery.Where(p => !p.IsArchived);

        var projects = await projectQuery
            .Where(p => projectIds.Contains(p.Id))
            .ToListAsync();

        if (projects.Count == 0)
            throw new KeyNotFoundException("No projects found for the provided project IDs.");

        var distinctOrgIds = projects.Select(p => p.OrganizationId).Distinct().ToList();

        if (distinctOrgIds.Count > 1)
            throw new InvalidOperationException("Projects belong to multiple organizations.");

        var projectOrgId = distinctOrgIds[0];

        if (organizationId.HasValue && organizationId.Value != projectOrgId)
            throw new InvalidOperationException(
                $"Organization ID {organizationId.Value} does not match the organization of the projects.");

        _logger.LogInformation("Organization found - Organization: {OrganizationId}", projectOrgId);

        return projectOrgId;
    }
}