using System.ComponentModel.DataAnnotations;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class OauthApplicationBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private Mock<ILogger<OauthApplicationBusiness>> _mockOauthLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private OauthApplicationBusiness _oauthApplicationBusiness;
    public long appid1; // oauth application IDs
    public long appid2;
    public long appid3;

    public long uid; // user ID

    public OauthApplicationBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _mockOauthLogger = new Mock<ILogger<OauthApplicationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _eventBusiness = new EventBusiness(Context, _notificationBusiness);
        _oauthApplicationBusiness = new OauthApplicationBusiness(Context, _mockOauthLogger.Object, _eventBusiness);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // create user
        var user = new User
        {
            Name = "Test User",
            Email = "test@test.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        // create test oauth applications
        var app1 = new OauthApplication
        {
            Name = "App 1",
            Description = "Description 1",
            ClientId = "test-client-id-1",
            ClientSecretHash = "hashed-secret-1",
            CallbackUrl = "https://app1.com/callback",
            BaseUrl = "https://app1.com",
            AppOwnerEmail = "owner1@example.com",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var app2 = new OauthApplication
        {
            Name = "App 2",
            Description = "Description 2",
            ClientId = "test-client-id-2",
            ClientSecretHash = "hashed-secret-2",
            CallbackUrl = "https://app2.com/callback",
            IsArchived = true,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var app3 = new OauthApplication
        {
            Name = "App 3",
            Description = "Description 3",
            ClientId = "test-client-id-3",
            ClientSecretHash = "hashed-secret-3",
            CallbackUrl = "https://app3.com/callback",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        Context.OauthApplications.AddRange(app1, app2, app3);
        await Context.SaveChangesAsync();
        appid1 = app1.Id;
        appid2 = app2.Id;
        appid3 = app3.Id;

        // delete app 3
        Context.OauthApplications.Remove(app3);
        await Context.SaveChangesAsync();
    }

    #region CreateOauthApplication Tests

    [Fact]
    public async Task CreateOauthApplication_Succeeds_WithValidData()
    {
        // Arrange
        var dto = new CreateOauthApplicationRequestDto
        {
            Name = "Test App",
            Description = "Test Description",
            CallbackUrl = "https://example.com/callback",
            BaseUrl = "https://example.com",
            AppOwnerEmail = "owner@example.com"
        };

        // Act
        var result = await _oauthApplicationBusiness.CreateOauthApplication(dto, uid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test App", result.Name);
        Assert.NotNull(result.ClientId);
        Assert.NotNull(result.ClientSecretRaw);
        Assert.NotEmpty(result.ClientId);
        Assert.NotEmpty(result.ClientSecretRaw);

        // Verify it was actually saved to DB
        var savedApp = await Context.OauthApplications
            .FirstOrDefaultAsync(a => a.ClientId == result.ClientId);
        Assert.NotNull(savedApp);
        Assert.Equal("Test App", savedApp.Name);
        Assert.Equal("Test Description", savedApp.Description);
        Assert.Equal("https://example.com/callback", savedApp.CallbackUrl);
        Assert.Equal(uid, savedApp.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateOauthApplication_Succeeds_WithMinimalData()
    {
        // Arrange
        var dto = new CreateOauthApplicationRequestDto
        {
            Name = "Minimal App",
            CallbackUrl = "https://example.com/callback"
        };

        // Act
        var result = await _oauthApplicationBusiness.CreateOauthApplication(dto, uid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Minimal App", result.Name);
        Assert.NotNull(result.ClientId);
        Assert.NotNull(result.ClientSecretRaw);
    }

    [Fact]
    public async Task CreateOauthApplication_Success()
    {
        // Arrange
        var dto = new CreateOauthApplicationRequestDto
        {
            Name = "Event Test App",
            CallbackUrl = "https://example.com/callback"
        };

        // Act
        var result = await _oauthApplicationBusiness.CreateOauthApplication(dto, uid);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CreateOauthApplication_Fails_IfNoName()
    {
        // Arrange
        var dto = new CreateOauthApplicationRequestDto
        {
            CallbackUrl = "https://example.com/callback"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _oauthApplicationBusiness.CreateOauthApplication(dto, uid));
    }

    [Fact]
    public async Task CreateOauthApplication_Fails_IfNoCallbackUrl()
    {
        // Arrange
        var dto = new CreateOauthApplicationRequestDto
        {
            Name = "No Callback App"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _oauthApplicationBusiness.CreateOauthApplication(dto, uid));
    }

    [Fact]
    public async Task CreateOauthApplication_GeneratesUniqueClientId()
    {
        // Arrange
        var dto1 = new CreateOauthApplicationRequestDto
        {
            Name = "App 1",
            CallbackUrl = "https://example.com/callback1"
        };
        var dto2 = new CreateOauthApplicationRequestDto
        {
            Name = "App 2",
            CallbackUrl = "https://example.com/callback2"
        };

        // Act
        var result1 = await _oauthApplicationBusiness.CreateOauthApplication(dto1, uid);
        var result2 = await _oauthApplicationBusiness.CreateOauthApplication(dto2, uid);

        // Assert
        Assert.NotEqual(result1.ClientId, result2.ClientId);
        Assert.NotEqual(result1.ClientSecretRaw, result2.ClientSecretRaw);
    }

    [Fact]
    public async Task CreateOauthApplication_ClientSecretIsUrlSafe()
    {
        // Arrange
        var dto = new CreateOauthApplicationRequestDto
        {
            Name = "URL Safe Test",
            CallbackUrl = "https://example.com/callback"
        };

        // Act
        var result = await _oauthApplicationBusiness.CreateOauthApplication(dto, uid);

        // Assert - should not contain URL-unsafe characters
        Assert.DoesNotContain("+", result.ClientId);
        Assert.DoesNotContain("/", result.ClientId);
        Assert.DoesNotContain("=", result.ClientId);
        Assert.DoesNotContain("+", result.ClientSecretRaw);
        Assert.DoesNotContain("/", result.ClientSecretRaw);
        Assert.DoesNotContain("=", result.ClientSecretRaw);
    }

    [Fact]
    public async Task CreateOauthApplication_HashesClientSecret()
    {
        // Arrange
        var dto = new CreateOauthApplicationRequestDto
        {
            Name = "Hash Test App",
            CallbackUrl = "https://example.com/callback"
        };

        // Act
        var result = await _oauthApplicationBusiness.CreateOauthApplication(dto, uid);

        // Assert
        var savedApp = await Context.OauthApplications
            .FirstOrDefaultAsync(a => a.ClientId == result.ClientId);
        Assert.NotNull(savedApp);

        // The stored hash should be different from the returned plain secret
        Assert.NotEqual(result.ClientSecretRaw, savedApp.ClientSecretHash);

        // The stored hash should be a valid BCrypt hash (starts with $2a$, $2b$, or $2y$)
        Assert.Matches(@"^\$2[aby]\$\d{2}\$.{53}$", savedApp.ClientSecretHash);

        // Verify that the plain secret can be verified against the stored hash
        Assert.True(BCrypt.Net.BCrypt.Verify(result.ClientSecretRaw, savedApp.ClientSecretHash));
    }

    #endregion

    #region GetAllOauthApplications Tests

    [Fact]
    public async Task GetAllOauthApplications_ExcludesArchived()
    {
        // Act
        var result = await _oauthApplicationBusiness.GetAllOauthApplications();
        var applications = result.ToList();

        // Assert
        Assert.Equal(1, applications.Count);
        Assert.All(applications, a => Assert.Equal(false, a.IsArchived));
        Assert.Contains(applications, a => a.Id == appid1);
        Assert.DoesNotContain(applications, a => a.Id == appid2);
    }

    [Fact]
    public async Task GetAllOauthApplications_IncludesArchived_WhenHideArchivedFalse()
    {
        // Act
        var result = await _oauthApplicationBusiness.GetAllOauthApplications(false);
        var applications = result.ToList();

        // Assert
        Assert.Equal(2, applications.Count);
        Assert.Contains(applications, a => a.Id == appid1 && !a.IsArchived);
        Assert.Contains(applications, a => a.Id == appid2 && a.IsArchived);
    }

    [Fact]
    public async Task GetAllOauthApplications_ReturnsEmptyList_WhenNoApplications()
    {
        // clear database for emptied list of apps
        await base.SeedTestDataAsync();

        // Act
        var result = await _oauthApplicationBusiness.GetAllOauthApplications();
        var applications = result.ToList();

        // Assert
        Assert.Empty(applications);
    }

    #endregion

    #region GetOauthApplication Tests

    [Fact]
    public async Task GetOauthApplication_Succeeds_WhenExists()
    {
        // Act
        var result = await _oauthApplicationBusiness.GetOauthApplication(appid1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(appid1, result.Id);
        Assert.Equal("App 1", result.Name);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task GetOauthApplication_Succeeds_IfArchived_AndHideArchivedFalse()
    {
        // Act
        var result = await _oauthApplicationBusiness.GetOauthApplication(appid2, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(appid2, result.Id);
        Assert.Equal("App 2", result.Name);
        Assert.True(result.IsArchived);
    }

    [Fact]
    public async Task GetOauthApplication_Fails_IfArchived_AndHideArchivedTrue()
    {
        // Act
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _oauthApplicationBusiness.GetOauthApplication(appid2));

        // Assert
        Assert.Contains($"Oauth application with id {appid2} is archived", exception.Message);
    }

    [Fact]
    public async Task GetOauthApplication_Fails_IfDeleted()
    {
        // Act
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _oauthApplicationBusiness.GetOauthApplication(appid3));

        // Assert
        Assert.Contains($"Oauth application with id {appid3} not found", exception.Message);
    }

    [Fact]
    public async Task GetOauthApplication_DoesNotExposeClientSecretHash()
    {
        // Act
        var result = await _oauthApplicationBusiness.GetOauthApplication(appid1);

        // Assert
        Assert.NotNull(result);
        // Verify that the DTO doesn't have a ClientSecretHash property exposed
        var properties = result.GetType().GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "ClientSecretHash");
    }

    #endregion

    #region UpdateOauthApplication Tests

    [Fact]
    public async Task UpdateOauthApplication_Success_ReturnsUpdatedApplication()
    {
        // Arrange
        var dto = new UpdateOauthApplicationRequestDto
        {
            Name = "Updated App",
            Description = "Updated Description",
            CallbackUrl = "https://updated.com/callback",
            BaseUrl = "https://updated.com",
            AppOwnerEmail = "updated@example.com"
        };

        // Act
        var result = await _oauthApplicationBusiness.UpdateOauthApplication(appid1, dto, uid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(appid1, result.Id);
        Assert.Equal("Updated App", result.Name);
        Assert.Equal("Updated Description", result.Description);
        Assert.Equal("https://updated.com/callback", result.CallbackUrl);
        Assert.Equal("https://updated.com", result.BaseUrl);
        Assert.Equal("updated@example.com", result.AppOwnerEmail);

        // Verify it was actually saved to DB
        var savedApp = await Context.OauthApplications.FindAsync(appid1);
        Assert.NotNull(savedApp);
        Assert.Equal("Updated App", savedApp.Name);
        Assert.Equal(uid, savedApp.LastUpdatedBy);
    }

    [Fact]
    public async Task UpdateOauthApplication_Success_PartialUpdate()
    {
        // Arrange
        var dto = new UpdateOauthApplicationRequestDto
        {
            Name = "Partially Updated"
        };

        // Act
        var result = await _oauthApplicationBusiness.UpdateOauthApplication(appid1, dto, uid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Partially Updated", result.Name);
        // Other fields should remain unchanged
        Assert.Equal("Description 1", result.Description);
        Assert.Equal("https://app1.com/callback", result.CallbackUrl);
    }

    [Fact]
    public async Task UpdateOauthApplication_Success()
    {
        // Arrange
        var dto = new UpdateOauthApplicationRequestDto
        {
            Name = "Event Update App"
        };

        // Act
        var result = await _oauthApplicationBusiness.UpdateOauthApplication(appid1, dto, uid);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateOauthApplication_Fails_IfNotFound()
    {
        // Arrange
        var dto = new UpdateOauthApplicationRequestDto
        {
            Name = "Updated App"
        };

        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _oauthApplicationBusiness.UpdateOauthApplication(appid3, dto, uid));

        // Assert
        Assert.Contains($"Oauth application with id {appid3} not found", exception.Message);
    }

    [Fact]
    public async Task UpdateOauthApplication_Fails_IfArchived()
    {
        // Arrange
        var dto = new UpdateOauthApplicationRequestDto
        {
            Name = "Updated Archived App"
        };

        // Act
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _oauthApplicationBusiness.UpdateOauthApplication(appid2, dto, uid));

        // Assert
        Assert.Contains($"Oauth application with id {appid2} not found", exception.Message);
    }

    #endregion

    #region ArchiveOauthApplication Tests

    [Fact]
    public async Task ArchiveOauthApplication_Succeeds_IfNotArchived()
    {
        // Act
        var result = await _oauthApplicationBusiness.ArchiveOauthApplication(appid1, uid);

        // Assert
        Assert.True(result);

        // Force EF to sync with database
        Context.ChangeTracker.Clear();

        // Verify it was actually saved to DB
        var savedApp = await Context.OauthApplications.FindAsync(appid1);
        Assert.NotNull(savedApp);
        Assert.True(savedApp.IsArchived);
        Assert.Equal(uid, savedApp.LastUpdatedBy);
    }

    [Fact]
    public async Task ArchiveOauthApplication_Fails_IfArchived()
    {
        // Act
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _oauthApplicationBusiness.ArchiveOauthApplication(appid2, uid));

        // Assert
        Assert.Contains($"Oauth application with id {appid2} not found", exception.Message);
    }

    [Fact]
    public async Task ArchiveOauthApplication_Fails_IfNotFound()
    {
        // Act
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _oauthApplicationBusiness.ArchiveOauthApplication(appid3, uid));

        // Assert
        Assert.Contains($"Oauth application with id {appid3} not found", exception.Message);
    }

    #endregion

    #region UnarchiveOauthApplication Tests

    [Fact]
    public async Task UnarchiveOauthApplication_Succeeds_IfArchived()
    {
        // Act
        var result = await _oauthApplicationBusiness.UnarchiveOauthApplication(appid2, uid);

        // Assert
        Assert.True(result);

        // Verify it was actually saved to DB
        var savedApp = await Context.OauthApplications.FindAsync(appid2);
        Assert.NotNull(savedApp);
        Assert.False(savedApp.IsArchived);
        Assert.Equal(uid, savedApp.LastUpdatedBy);
    }

    [Fact]
    public async Task UnarchiveOauthApplication_Fails_IfNotArchived()
    {
        // Act
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _oauthApplicationBusiness.UnarchiveOauthApplication(appid1, uid));

        // Assert
        Assert.Contains($"Oauth application with id {appid1} not found or is not archived", exception.Message);
    }

    [Fact]
    public async Task UnarchiveOauthApplication_Fails_IfNotFound()
    {
        // Act
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _oauthApplicationBusiness.UnarchiveOauthApplication(appid3, uid));

        // Assert
        Assert.Contains($"Oauth application with id {appid3} not found or is not archived", exception.Message);
    }

    #endregion

    #region DeleteOauthApplication Tests

    [Fact]
    public async Task DeleteOauthApplication_Succeeds_WhenExists()
    {
        // Act
        var result = await _oauthApplicationBusiness.DeleteOauthApplication(appid1, uid);

        // Assert
        Assert.True(result);

        // Verify it was actually deleted from DB
        var deletedApp = await Context.OauthApplications.FindAsync(appid1);
        Assert.Null(deletedApp);
    }

    [Fact]
    public async Task DeleteOauthApplication_Fails_IfNotFound()
    {
        // Act
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _oauthApplicationBusiness.DeleteOauthApplication(appid3, uid));

        // Assert
        Assert.Contains($"Oauth application with id {appid3} not found", exception.Message);
    }

    [Fact]
    public async Task DeleteOauthApplication_Fails_IfArchived()
    {
        // Act
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _oauthApplicationBusiness.DeleteOauthApplication(appid2, uid));

        // Assert
        Assert.Contains($"Oauth application with id {appid2} not found", exception.Message);

        // Ensure that no event was logged
        var eventList = Context.Events.ToList();
        Assert.Empty(eventList);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateOauthApplication_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var dto = new CreateOauthApplicationRequestDto
        {
            Name = "LastUpdatedBy Test",
            CallbackUrl = "https://example.com/callback"
        };

        // Act
        var result = await _oauthApplicationBusiness.CreateOauthApplication(dto, uid);

        // Assert
        var savedApp = await Context.OauthApplications
            .FirstOrDefaultAsync(a => a.ClientId == result.ClientId);
        Assert.NotNull(savedApp);
        Assert.Equal(uid, savedApp.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateOauthApplication_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var dto = new CreateOauthApplicationRequestDto
        {
            Name = "Navigation Test",
            CallbackUrl = "https://example.com/callback"
        };

        var result = await _oauthApplicationBusiness.CreateOauthApplication(dto, uid);

        // Act
        var appWithUser = await Context.OauthApplications
            .Include(a => a.LastUpdatedByUser)
            .FirstAsync(a => a.ClientId == result.ClientId);

        // Assert
        Assert.NotNull(appWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", appWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test@test.com", appWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, appWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task UpdateOauthApplication_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var dto = new UpdateOauthApplicationRequestDto
        {
            Name = "Updated Name"
        };

        // Act
        var result = await _oauthApplicationBusiness.UpdateOauthApplication(appid1, dto, uid);

        // Assert
        var updatedApp = await Context.OauthApplications
            .Include(a => a.LastUpdatedByUser)
            .FirstAsync(a => a.Id == appid1);

        Assert.Equal(uid, updatedApp.LastUpdatedBy);
        Assert.NotNull(updatedApp.LastUpdatedByUser);
        Assert.Equal("Test User", updatedApp.LastUpdatedByUser.Name);
        Assert.Equal("Updated Name", updatedApp.Name);
    }

    #endregion
}