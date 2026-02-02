using deeplynx.datalayer.Models;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.helpers
{
    public static class ExistenceHelper
    {
        public static async Task EnsureUserExistsAsync(DeeplynxContext context, long userId, bool hideArchived = true)
        {
            var userExists = hideArchived
                ? await context.Users.AnyAsync(u => u.Id == userId && u.IsArchived == false)
                : await context.Users.AnyAsync(u => u.Id == userId);

            if (!userExists)
                throw new KeyNotFoundException($"User with id {userId} does not exist");
        }

        /// <summary>
        /// Check if an organization exists
        /// </summary>
        /// <param name="context">DB context</param>
        /// <param name="organizationId">Org ID to check existence for</param>
        /// <param name="hideArchived">Boolean indicating whether to hide archived orgs</param>
        /// <exception cref="KeyNotFoundException">Returned if org doesn't exist</exception>
        public static async Task EnsureOrganizationExistsAsync(
            DeeplynxContext context,
            long organizationId,
            bool hideArchived = true)
        {
            var organizationExists = hideArchived
                ? await context.Organizations.AnyAsync(o => o.Id == organizationId && o.IsArchived == false)
                : await context.Organizations.AnyAsync(o => o.Id == organizationId);

            if (!organizationExists)
                throw new KeyNotFoundException($"Organization with id {organizationId} does not exist");
        }

        public static async Task<ProjectResponseDto> EnsureProjectExistsAsync(
            DeeplynxContext context,
            long projectId,
            bool hideArchived = true)
        {
            // Try to get the cached list of projects
            var projectResponseList = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>("projects");

            if (projectResponseList == null || projectResponseList.Count == 0)
            {
                // Cache is empty, so populate it
                var projectList = await context.Projects.ToListAsync();

                projectResponseList = projectList.Select(p => new ProjectResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Abbreviation = p.Abbreviation,
                    IsArchived = p.IsArchived,
                    LastUpdatedAt = p.LastUpdatedAt,
                    LastUpdatedBy = p.LastUpdatedBy,
                    OrganizationId = p.OrganizationId
                }).ToList();

                // Store the list in the cache
                await CacheService.Instance.SetAsync("projects", projectResponseList, TimeSpan.FromHours(1));
            }

            // Find the project by ID from the list
            var project = projectResponseList.FirstOrDefault(p => p.Id == projectId);

            if (project == null || hideArchived && project.IsArchived)
            {

                throw new KeyNotFoundException($"Project with id {projectId} not found.");
            }

            return project;
        }

        public static async Task EnsureDataSourceExistsForProjectAsync(DeeplynxContext context, long dataSourceId, long projectId, bool hideArchived = true)
        {
            var dataSourceExists = hideArchived
                ? await context.DataSources.AnyAsync(ds => ds.ProjectId == projectId && ds.Id == dataSourceId && ds.IsArchived == false)
                : await context.DataSources.AnyAsync(ds => ds.ProjectId == projectId && ds.Id == dataSourceId);

            if (!dataSourceExists)
            {
                throw new KeyNotFoundException($"DataSource with id {dataSourceId} not found in project with id {projectId}");
            }
        }
    }
}