using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers;

public interface IOrganizationService
{
    Task<long> CheckExistence(long? projectId, long? organizationId, bool includeArchived = false);
}

public class OrganizationService : IOrganizationService
{
    private readonly DeeplynxContext _dbContext;
    private readonly ILogger<SysAdminService> _logger;

    public OrganizationService(
        DeeplynxContext dbContext,
        ILogger<SysAdminService> logger)
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

            // Include archived projects if requested
            if (!includeArchived) projectQuery = projectQuery.Where(p => !p.IsArchived);

            var project = await projectQuery.FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null) throw new KeyNotFoundException($"Project with ID {projectId} not found");

            if (organizationId.HasValue && project.OrganizationId != organizationId.Value)
                throw new InvalidOperationException(
                    $"Project {projectId} does not belong to organization {organizationId.Value}");

            _logger.LogInformation("Organization found - Organization: {OrganizationId}", project.OrganizationId);

            // Return the organizationId
            return project.OrganizationId;
        }

        return organizationId.GetValueOrDefault();
    }
}