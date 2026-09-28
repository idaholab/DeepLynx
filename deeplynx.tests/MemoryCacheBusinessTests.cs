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

        // ---------------------------------------------------------------
        // New tests: SizeLimit / eviction behavior (96 MB hard cap)
        // ---------------------------------------------------------------

        [Fact]
        public async Task Cache_EvictsOldestEntries_WhenSizeLimitExceeded()
        {
            // Arrange: each entry is ~2MB serialized, so ~60 entries (~120MB) comfortably
            // exceeds the 96MB SizeLimit and forces compaction to kick in.
            var testId = Guid.NewGuid();
            var payload = new string('x', 2_000_000); // ~2MB string
            var keys = new List<string>();

            // Act
            for (int i = 0; i < 60; i++)
            {
                var key = $"sizelimit-test:{testId}:{i}";
                keys.Add(key);
                await CacheService.Instance.SetAsync(key, payload, (TimeSpan?)null);
            }

            // Assert: the earliest entries should have been evicted by compaction,
            // while the most recently written entries should still be present.
            var earliestStillPresent = await CacheService.Instance.GetAsync<string>(keys[0]);
            var latestStillPresent = await CacheService.Instance.GetAsync<string>(keys[^1]);

            Assert.Null(earliestStillPresent);
            Assert.NotNull(latestStillPresent);
        }

        [Fact]
        public async Task DeleteByPrefixAsync_HandlesEntriesEvictedBySizeLimit()
        {
            // Arrange: force enough entries under one prefix to exceed the size limit and
            // trigger eviction of some of them, then verify DeleteByPrefixAsync doesn't
            // error or leave stale keys behind for entries that were already evicted.
            var testId = Guid.NewGuid();
            var prefix = $"sizelimit-prefix-test:{testId}:";
            var payload = new string('x', 2_000_000); // ~2MB string

            for (int i = 0; i < 60; i++)
            {
                await CacheService.Instance.SetAsync($"{prefix}{i}", payload, (TimeSpan?)null);
            }

            // Act
            var result = await CacheService.Instance.DeleteByPrefixAsync(prefix);

            // Assert: call succeeds cleanly regardless of how many entries were already
            // evicted by the size limit before this ran.
            Assert.True(result);

            // Nothing under the prefix should be retrievable afterward, evicted or not.
            for (int i = 0; i < 60; i++)
            {
                var value = await CacheService.Instance.GetAsync<string>($"{prefix}{i}");
                Assert.Null(value);
            }
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