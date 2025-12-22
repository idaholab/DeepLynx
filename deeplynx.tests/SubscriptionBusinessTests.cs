using System.ComponentModel.DataAnnotations;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Action = deeplynx.datalayer.Models.Action;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class SubscriptionBusinessTests : IntegrationTestBase
{
    private readonly DateTime now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    private SubscriptionBusiness _subscriptionBusiness = null!;

    public long aid; // Action ID
    public long did; // Datasource ID
    public long oid; // Organization ID
    public long pid; // Project ID
    public long uid; // User ID

    public SubscriptionBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _subscriptionBusiness = new SubscriptionBusiness(Context);
        await CacheService.Instance.SetAsync("projects", null, TimeSpan.FromSeconds(1));
    }

    #region BulkUpdateSubscriptions Tests

    [Fact]
    public async Task BulkUpdateSubscriptions_Success_UpdatesSubscriptions()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "create",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1
            },
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "update",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1
            },
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "delete",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1
            }
        };
        Context.Subscriptions.AddRange(subscriptions);
        await Context.SaveChangesAsync();

        var dtos = new List<UpdateSubscriptionRequestDto>
        {
            new()
            {
                Id = subscriptions[0].Id,
                ActionId = aid,
                Operation = "delete",
                DataSourceId = did,
                EntityType = "data_source",
                EntityId = 2
            },
            new()
            {
                Id = subscriptions[1].Id,
                ActionId = aid,
                Operation = "delete",
                DataSourceId = did,
                EntityType = "edge",
                EntityId = 2
            }
        };

        // Act
        await _subscriptionBusiness.BulkUpdateSubscriptions(uid, oid, pid, dtos);

        // Assert
        var subscriptionList = await Context.Subscriptions
            .Where(s => s.Operation == "delete" && s.LastUpdatedBy == uid)
            .ToListAsync();

        Assert.Equal(2, subscriptionList.Count);
    }

    #endregion

    #region BulkDeleteSubSubscriptions Tests

    [Fact]
    public async Task BulkDeleteSubscriptions_Success_DeletesSubscriptions()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "create",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1
            },
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "update",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1
            },
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "delete",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1
            }
        };
        Context.Subscriptions.AddRange(subscriptions);
        await Context.SaveChangesAsync();

        // Delete 2/3 Subscriptions to ensure deletion is done selectively by ID
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _subscriptionBusiness.BulkDeleteSubscriptions(uid, pid, new List<long>
            {
                subscriptions[0].Id,
                subscriptions[1].Id,
                5 // Include a non-existing subscriptionID
            }));

        Assert.Contains("Some subscriptions were not deleted because they do not exist.", exception.Message);

        var subscriptionList = await Context.Subscriptions.ToListAsync();
        Assert.Single(subscriptionList);
    }

    #endregion

    #region BulkArchiveSubscriptions Tests

    [Fact]
    public async Task BulkArchiveSubscriptions_Success_ArchivesSubscriptions()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "create",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1,
                LastUpdatedBy = uid,
                LastUpdatedAt = now
            },
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "update",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1,
                LastUpdatedBy = uid,
                LastUpdatedAt = now
            },
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "delete",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1,
                LastUpdatedBy = uid,
                LastUpdatedAt = now
            }
        };
        Context.Subscriptions.AddRange(subscriptions);
        await Context.SaveChangesAsync();

        // Archive 2/3 subscriptions to ensure it selectively archives by ID
        // Act
        var result = await _subscriptionBusiness.BulkArchiveSubscriptions(uid, pid,
            new List<long> { subscriptions[0].Id, subscriptions[1].Id });

        // Assert
        Assert.True(result);

        var subscriptionList = await Context.Subscriptions
            .Where(s => s.IsArchived)
            .ToListAsync();

        Assert.Equal(2, subscriptionList.Count);
        Assert.True(subscriptionList.All(s => s.LastUpdatedBy == uid));
    }

    #endregion

    #region BulkUnarchiveSubscriptions Tests

    [Fact]
    public async Task BulkUnarchiveSubscriptions_Success_UnarchivesSpecificSubscriptions()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "create",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1,
                IsArchived = true
            },
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "update",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1,
                IsArchived = true
            },
            new()
            {
                UserId = uid,
                ProjectId = pid,
                OrganizationId = oid,
                ActionId = aid,
                Operation = "delete",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1,
                IsArchived = true
            }
        };
        Context.Subscriptions.AddRange(subscriptions);
        await Context.SaveChangesAsync();

        // Unarchive 2/3 subscriptions to ensure it selectively Unarchives by ID
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _subscriptionBusiness.BulkUnarchiveSubscriptions(uid,
            pid, new List<long>
            {
                subscriptions[0].Id,
                subscriptions[1].Id,
                5 // Include non-existing ID
            }));

        Context.ChangeTracker.Clear();

        var subscriptionList = await Context.Subscriptions
            .Where(s => !s.IsArchived)
            .ToListAsync();

        Assert.Equal(2, subscriptionList.Count);
        Assert.True(subscriptionList.All(s => s.LastUpdatedBy == uid));
    }

    #endregion

    #region SubscriptionResponseDto Tests

    [Fact]
    public void SubscriptionResponseDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new SubscriptionResponseDto
        {
            Id = 1,
            UserId = uid,
            ProjectId = pid,
            ActionId = 100,
            Operation = "create",
            DataSourceId = 200,
            EntityType = "record",
            EntityId = 300,
            LastUpdatedAt = now,
            LastUpdatedBy = uid,
            IsArchived = false
        };

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal(uid, dto.UserId);
        Assert.Equal(pid, dto.ProjectId);
        Assert.Equal(100, dto.ActionId);
        Assert.Equal("create", dto.Operation);
        Assert.Equal(200, dto.DataSourceId);
        Assert.Equal("record", dto.EntityType);
        Assert.Equal(300, dto.EntityId);
        Assert.Equal(now, dto.LastUpdatedAt);
        Assert.Equal(uid, dto.LastUpdatedBy);
        Assert.False(dto.IsArchived);
    }

    #endregion

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User
        {
            Name = "test_user",
            Email = "Fake@gmail.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        oid = organization.Id;

        var project = new Project
        {
            Name = "Project 1",
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        pid = project.Id;

        // Add action
        var action = new Action
        {
            Name = "Action1",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedBy = uid,
            LastUpdatedAt = now
        };
        Context.Actions.Add(action);
        await Context.SaveChangesAsync();
        aid = action.Id;

        // Add datasource
        var dataSource = new DataSource
        {
            Name = "DataSource2",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedBy = uid,
            LastUpdatedAt = now
        };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();
        did = dataSource.Id;
    }

    #region GetAllSubscriptions Tests

    [Fact]
    public async Task GetAllSubscriptions_Success_ReturnsSubscriptions()
    {
        // Arrange
        var subscription = new Subscription
        {
            UserId = uid,
            ProjectId = pid,
            OrganizationId = oid,
            ActionId = aid,
            Operation = "create",
            DataSourceId = did,
            EntityType = "record",
            EntityId = 1
        };
        Context.Subscriptions.Add(subscription);
        await Context.SaveChangesAsync();

        // Act
        var result = await _subscriptionBusiness.GetAllSubscriptions(uid, pid, false);

        // Assert
        Assert.Single(result);

        var actualSubscription = result.First();

        Assert.Equal(uid, actualSubscription.UserId);
        Assert.Equal(pid, actualSubscription.ProjectId);
        Assert.Equal(aid, actualSubscription.ActionId);
        Assert.Equal("create", actualSubscription.Operation);
        Assert.Equal(did, actualSubscription.DataSourceId);
        Assert.Equal("record", actualSubscription.EntityType);
        Assert.Equal(1, actualSubscription.EntityId);
    }

    [Fact]
    public async Task GetSubscription_Success_ReturnsSubscription()
    {
        // Arrange
        var subscription = new Subscription
        {
            UserId = uid,
            ProjectId = pid,
            OrganizationId = oid,
            ActionId = aid,
            Operation = "delete",
            DataSourceId = null,
            EntityType = "record",
            EntityId = 1
        };
        Context.Subscriptions.Add(subscription);
        await Context.SaveChangesAsync();

        // Act
        var result = await _subscriptionBusiness.GetSubscription(uid, pid, subscription.Id, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(uid, result.UserId);
        Assert.Equal(pid, result.ProjectId);
        Assert.Equal(aid, result.ActionId);
        Assert.Equal("delete", result.Operation);
        Assert.Equal("record", result.EntityType);
        Assert.Equal(1, result.EntityId);
    }

    #endregion

    #region BulkCreateSubscriptions Tests

    [Fact]
    public async Task BulkCreateSubscriptions_Success_CreatesSubscriptions()
    {
        // Arrange
        var dtos = new List<CreateSubscriptionRequestDto>
        {
            new()
            {
                UserId = uid,
                OrganizationId = oid,
                ProjectId = pid,
                ActionId = aid,
                Operation = "delete",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1
            },
            new()
            {
                UserId = uid,
                OrganizationId = oid,
                ProjectId = pid,
                ActionId = aid,
                Operation = "update",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 3
            }
        };

        // Act
        var result = await _subscriptionBusiness.BulkCreateSubscriptions(uid, oid, pid, dtos);

        // Assert
        Assert.Equal(2, result.Count);

        var firstSubscription = result.First();
        Assert.Equal(uid, firstSubscription.UserId);
        Assert.Equal(uid, firstSubscription.LastUpdatedBy);
        Assert.Equal(pid, firstSubscription.ProjectId);
        Assert.Equal(aid, firstSubscription.ActionId);
        Assert.Equal("delete", firstSubscription.Operation);
        Assert.Equal("record", firstSubscription.EntityType);
        Assert.Equal(1, firstSubscription.EntityId);

        var lastSubscription = result.Last();
        Assert.Equal(uid, lastSubscription.UserId);
        Assert.Equal(uid, firstSubscription.LastUpdatedBy);
        Assert.Equal(pid, lastSubscription.ProjectId);
        Assert.Equal(aid, lastSubscription.ActionId);
        Assert.Equal("update", lastSubscription.Operation);
        Assert.Equal(did, lastSubscription.DataSourceId);
        Assert.Equal("record", lastSubscription.EntityType);
        Assert.Equal(3, lastSubscription.EntityId);
    }

    [Fact]
    public async Task BulkCreateSubscriptions_Fails_IfNoUserID()
    {
        // Arrange
        var dtos = new List<CreateSubscriptionRequestDto>
        {
            new()
            {
                OrganizationId = oid,
                ProjectId = pid,
                ActionId = aid,
                Operation = "delete",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _subscriptionBusiness.BulkCreateSubscriptions(uid, oid, pid, dtos));

        var subscriptionList = await Context.Subscriptions.ToListAsync();
        Assert.Empty(subscriptionList);
    }

    [Fact]
    public async Task BulkCreateSubscriptions_Fails_IfNoProjectID()
    {
        // Arrange
        var dtos = new List<CreateSubscriptionRequestDto>
        {
            new()
            {
                UserId = uid,
                ActionId = aid,
                Operation = "delete",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _subscriptionBusiness.BulkCreateSubscriptions(uid, oid, pid, dtos));

        var subscriptionList = await Context.Subscriptions.ToListAsync();
        Assert.Empty(subscriptionList);
    }

    [Fact]
    public async Task BulkCreateSubscriptions_Fails_IfNoActionID()
    {
        // Arrange
        var dtos = new List<CreateSubscriptionRequestDto>
        {
            new()
            {
                UserId = uid,
                OrganizationId = oid,
                ProjectId = pid,
                Operation = "delete",
                DataSourceId = did,
                EntityType = "record",
                EntityId = 1
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _subscriptionBusiness.BulkCreateSubscriptions(uid, oid, pid, dtos));

        var subscriptionList = await Context.Subscriptions.ToListAsync();
        Assert.Empty(subscriptionList);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateSubscription_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testSubscription = new Subscription
        {
            UserId = uid,
            ProjectId = pid,
            OrganizationId = oid,
            ActionId = aid,
            Operation = "create",
            DataSourceId = did,
            EntityType = "record",
            EntityId = 100,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        // Act
        Context.Subscriptions.Add(testSubscription);
        await Context.SaveChangesAsync();

        // Assert
        var savedSubscription = await Context.Subscriptions.FindAsync(testSubscription.Id);
        Assert.NotNull(savedSubscription);
        Assert.Equal(uid, savedSubscription.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateSubscription_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testSubscription = new Subscription
        {
            UserId = uid,
            ProjectId = pid,
            OrganizationId = oid,
            ActionId = aid,
            Operation = "update",
            DataSourceId = did,
            EntityType = "record",
            EntityId = 101,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        Context.Subscriptions.Add(testSubscription);
        await Context.SaveChangesAsync();

        // Act
        var subscriptionWithUser = await Context.Subscriptions
            .Include(s => s.LastUpdatedByUser)
            .FirstAsync(s => s.Id == testSubscription.Id);

        // Assert
        Assert.NotNull(subscriptionWithUser.LastUpdatedByUser);
        Assert.Equal("test_user", subscriptionWithUser.LastUpdatedByUser.Name);
        Assert.Equal("Fake@gmail.com", subscriptionWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, subscriptionWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateSubscription_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testSubscription = new Subscription
        {
            UserId = uid,
            ProjectId = pid,
            OrganizationId = oid,
            ActionId = aid,
            Operation = "delete",
            DataSourceId = did,
            EntityType = "record",
            EntityId = 102,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };

        // Act
        Context.Subscriptions.Add(testSubscription);
        await Context.SaveChangesAsync();

        // Assert
        var savedSubscription = await Context.Subscriptions.FindAsync(testSubscription.Id);
        Assert.NotNull(savedSubscription);
        Assert.Null(savedSubscription.LastUpdatedBy);

        var subscriptionWithUser = await Context.Subscriptions
            .Include(s => s.LastUpdatedByUser)
            .FirstAsync(s => s.Id == testSubscription.Id);

        Assert.Null(subscriptionWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateSubscription_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var testSubscription = new Subscription
        {
            UserId = uid,
            ProjectId = pid,
            OrganizationId = oid,
            ActionId = aid,
            Operation = "create",
            DataSourceId = did,
            EntityType = "record",
            EntityId = 103,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Subscriptions.Add(testSubscription);
        await Context.SaveChangesAsync();

        // Act
        testSubscription.LastUpdatedBy = uid;
        testSubscription.Operation = "update";
        testSubscription.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.Subscriptions.Update(testSubscription);
        await Context.SaveChangesAsync();

        // Assert
        var updatedSubscription = await Context.Subscriptions
            .Include(s => s.LastUpdatedByUser)
            .FirstAsync(s => s.Id == testSubscription.Id);

        Assert.Equal(uid, updatedSubscription.LastUpdatedBy);
        Assert.NotNull(updatedSubscription.LastUpdatedByUser);
        Assert.Equal("test_user", updatedSubscription.LastUpdatedByUser.Name);
        Assert.Equal("update", updatedSubscription.Operation);
    }

    #endregion
}