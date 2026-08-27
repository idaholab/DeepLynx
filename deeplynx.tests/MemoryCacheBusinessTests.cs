using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;
using deeplynx.helpers;

namespace deeplynx.tests
{
    [Collection("Test Suite Collection")]
    public class MemoryCacheBusinessTests : IntegrationTestBase
    {
        private long organizationId;
        
        public MemoryCacheBusinessTests(TestSuiteFixture fixture) : base(fixture)
        {
        }
        
        public override async Task InitializeAsync()
        {
            SwitchCacheType("memory");
            await base.InitializeAsync();
        }
        
        [Fact]
        public async Task ConfirmTestingCorrectCacheType()
        {
            var type = CacheService.Instance.CacheType;
            Assert.True(type == "memory");
        }


        [Fact]
        public async Task SetAndGetCache_Success()
        {
            // Arrange
            var key = "projects";
            var value = new List<ProjectResponseDto>
            {
                new ProjectResponseDto { Id = 1, Name = "Project 1", IsArchived = false, OrganizationId = organizationId },
                new ProjectResponseDto { Id = 2, Name = "Project 2", IsArchived = true, OrganizationId = organizationId }
            };

            // Act
            await CacheService.Instance.SetAsync(key, value, (TimeSpan?)null);
            var cachedValue = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(key);

            // Assert
            Assert.Equivalent(value, cachedValue);
        }

        [Fact]
        public async Task DeleteCache_Success()
        {
            // Arrange
            var key = "projects";
            var value = new List<ProjectResponseDto>
            {
                new ProjectResponseDto { Id = 1, Name = "Project 1", IsArchived = false, OrganizationId = organizationId },
                new ProjectResponseDto { Id = 2, Name = "Project 2", IsArchived = true, OrganizationId = organizationId }
            };

            await CacheService.Instance.SetAsync(key, value, (TimeSpan?)null);

            // Act
            await CacheService.Instance.DeleteAsync(key);
            var cachedValue = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(key);

            // Assert
            Assert.Null(cachedValue);
        }

        [Fact]
        public async Task DeleteByPrefixAsync_DeletesOnlyMatchingKeys()
        {
            // Arrange
            var testId = Guid.NewGuid();
            var prefix = $"memory-prefix-test:{testId}:user:1:";

            var matchingKey1 = $"{prefix}orgadmin";
            var matchingKey2 = $"{prefix}projectadmin";
            var nonMatchingKey = $"memory-prefix-test:{testId}:user:10:orgadmin";

            await CacheService.Instance.SetAsync(
                matchingKey1,
                true,
                TimeSpan.FromMinutes(2));

            await CacheService.Instance.SetAsync(
                matchingKey2,
                false,
                TimeSpan.FromMinutes(2));

            await CacheService.Instance.SetAsync(
                nonMatchingKey,
                true,
                TimeSpan.FromMinutes(2));

            // Act
            var result = await CacheService.Instance.DeleteByPrefixAsync(prefix);

            // Assert
            Assert.True(result);
            Assert.Null(await CacheService.Instance.GetAsync<bool?>(matchingKey1));
            Assert.Null(await CacheService.Instance.GetAsync<bool?>(matchingKey2));
            Assert.True(await CacheService.Instance.GetAsync<bool>(nonMatchingKey));
        }

        [Fact]
        public async Task FlushCache_Success()
        {
            // Arrange
            var key1 = "projects-key1";
            var key2 = "projects-key2";
            var value = new List<ProjectResponseDto>
            {
                new ProjectResponseDto { Id = 1, Name = "Project 1", IsArchived = false, OrganizationId = organizationId },
                new ProjectResponseDto { Id = 2, Name = "Project 2", IsArchived = true, OrganizationId = organizationId }
            };

            await CacheService.Instance.SetAsync(key1, value, (TimeSpan?)null);
            await CacheService.Instance.SetAsync(key2, value, (TimeSpan?)null);

            // Act
            await CacheService.Instance.FlushAsync();
            var cachedValue1 = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(key1);
            var cachedValue2 = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>(key2);

            // Assert
            Assert.Null(cachedValue1);
            Assert.Null(cachedValue2);
        }

        protected override async Task SeedTestDataAsync()
        {
            await base.SeedTestDataAsync();
            var organization = new Organization { Name = "Test Organization" };
            Context.Organizations.Add(organization);
            await Context.SaveChangesAsync();
            organizationId = organization.Id;
        }
    }
}