using System.ComponentModel.DataAnnotations;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class UserModelTokenBusinessTests : IntegrationTestBase
{
    private UserModelTokenBusiness _userModelTokenBusiness = null!;

    public long uid;   // primary user ID
    public long uid2;  // secondary user ID
    public long oid;   // organization ID
    public long mcid1; // model config IDs
    public long mcid2;
    public long tid1;  // token IDs
    public long tid2;
    public long tid3;

    public UserModelTokenBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userModelTokenBusiness = new UserModelTokenBusiness(Context);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Create users
        var user1 = new User
        {
            Name = "Test User 1",
            Email = "user1@test.com",
            Password = "test_password",
            IsArchived = false
        };
        var user2 = new User
        {
            Name = "Test User 2",
            Email = "user2@test.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.AddRange(user1, user2);
        await Context.SaveChangesAsync();
        uid = user1.Id;
        uid2 = user2.Id;

        // Create organization
        var org = new Organization
        {
            Name = "Test Org",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();
        oid = org.Id;

        // Create AI model configs
        var config1 = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = null,
            ServerUrl = "https://api.openai.com",
            ModelProvider = "open ai",
            ModelName = "gpt-4o",
            ModelType = "llm",
            RequiresToken = true,
            Default = true,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var config2 = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = null,
            ServerUrl = "https://api.anthropic.com",
            ModelProvider = "anthropic",
            ModelName = "claude-sonnet-4-6",
            ModelType = "llm",
            RequiresToken = true,
            Default = false,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.AddRange(config1, config2);
        await Context.SaveChangesAsync();
        mcid1 = config1.Id;
        mcid2 = config2.Id;

        // Create user model tokens
        // tid1 - user1, config1
        var token1 = new UserModelToken
        {
            UserId = uid,
            AiModelConfigId = mcid1,
            Token = "sk-token-user1-config1",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        // tid2 - user1, config2
        var token2 = new UserModelToken
        {
            UserId = uid,
            AiModelConfigId = mcid2,
            Token = "sk-token-user1-config2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        // tid3 - user2, config1
        var token3 = new UserModelToken
        {
            UserId = uid2,
            AiModelConfigId = mcid1,
            Token = "sk-token-user2-config1",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.UserModelTokens.AddRange(token1, token2, token3);
        await Context.SaveChangesAsync();
        tid1 = token1.Id;
        tid2 = token2.Id;
        tid3 = token3.Id;
    }

    #region GetUserTokens Tests

    [Fact]
    public async Task GetUserTokens_ReturnsAllTokensForUser()
    {
        // Act
        var result = await _userModelTokenBusiness.GetUserTokens(uid);

        // Assert - user1 has tid1 and tid2
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == tid1);
        Assert.Contains(result, t => t.Id == tid2);
    }

    [Fact]
    public async Task GetUserTokens_DoesNotReturnOtherUsersTokens()
    {
        // Act
        var result = await _userModelTokenBusiness.GetUserTokens(uid);

        // Assert - tid3 belongs to user2
        Assert.DoesNotContain(result, t => t.Id == tid3);
    }

    [Fact]
    public async Task GetUserTokens_FilteredByAiModelConfigId_ReturnsCorrectToken()
    {
        // Act
        var result = await _userModelTokenBusiness.GetUserTokens(uid, mcid1);

        // Assert - user1 only has tid1 for config1
        Assert.Single(result);
        Assert.Equal(tid1, result[0].Id);
        Assert.Equal(mcid1, result[0].AiModelConfigId);
    }

    [Fact]
    public async Task GetUserTokens_FilteredByAiModelConfigId_ReturnsEmpty_WhenNoMatch()
    {
        // Arrange - create a config that user1 has no token for
        var config3 = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = null,
            ServerUrl = "https://hpc.example.com",
            ModelProvider = "hpc",
            ModelName = "hpc-model",
            ModelType = "embedding",
            RequiresToken = true,
            Default = false,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(config3);
        await Context.SaveChangesAsync();

        // Act
        var result = await _userModelTokenBusiness.GetUserTokens(uid, config3.Id);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUserTokens_ReturnsEmpty_WhenUserHasNoTokens()
    {
        // Arrange - create a user with no tokens
        var userWithNoTokens = new User
        {
            Name = "Tokenless User",
            Email = "notokens@test.com",
            Password = "password",
            IsArchived = false
        };
        Context.Users.Add(userWithNoTokens);
        await Context.SaveChangesAsync();

        // Act
        var result = await _userModelTokenBusiness.GetUserTokens(userWithNoTokens.Id);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUserTokens_ReturnsAllProperties_Correctly()
    {
        // Act
        var result = await _userModelTokenBusiness.GetUserTokens(uid);
        var token = result.First(t => t.Id == tid1);

        // Assert
        Assert.Equal(tid1, token.Id);
        Assert.Equal(uid, token.UserId);
        Assert.Equal(mcid1, token.AiModelConfigId);
        Assert.Equal("sk-token-user1-config1", token.Token);
        Assert.True(token.LastUpdatedAt > DateTime.MinValue);
    }

    #endregion

    #region GetTokenById Tests

    [Fact]
    public async Task GetTokenById_Success_WhenExists()
    {
        // Act
        var result = await _userModelTokenBusiness.GetTokenById(uid, tid1);

        // Assert
        Assert.Equal(tid1, result.Id);
        Assert.Equal(uid, result.UserId);
        Assert.Equal(mcid1, result.AiModelConfigId);
        Assert.Equal("sk-token-user1-config1", result.Token);
    }

    [Fact]
    public async Task GetTokenById_Fails_IfTokenNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userModelTokenBusiness.GetTokenById(uid, 99999));
    }

    [Fact]
    public async Task GetTokenById_Fails_IfTokenBelongsToAnotherUser()
    {
        // Act & Assert - tid3 belongs to user2, but user1 is requesting
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _userModelTokenBusiness.GetTokenById(uid, tid3));
    }

    #endregion

    #region CreateUserModelToken Tests

    [Fact]
    public async Task CreateUserModelToken_Success_ReturnsCorrectValues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new CreateUserModelTokenRequestDto
        {
            UserId = uid,
            AiModelConfigId = mcid1,
            Token = "sk-brand-new-token"
        };

        // Act
        var result = await _userModelTokenBusiness.CreateUserModelToken(uid, dto);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Equal(uid, result.UserId);
        Assert.Equal(mcid1, result.AiModelConfigId);
        Assert.Equal("sk-brand-new-token", result.Token);
        Assert.True(result.LastUpdatedAt >= now);
    }

    [Fact]
    public async Task CreateUserModelToken_Success_IsPersisted()
    {
        // Arrange
        var dto = new CreateUserModelTokenRequestDto
        {
            UserId = uid,
            AiModelConfigId = mcid2,
            Token = "sk-persisted-token"
        };

        // Act
        var result = await _userModelTokenBusiness.CreateUserModelToken(uid, dto);

        // Assert - verify it's actually in the database
        var saved = await Context.UserModelTokens.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal("sk-persisted-token", saved.Token);
        Assert.Equal(uid, saved.UserId);
    }

    [Fact]
    public async Task CreateUserModelToken_Fails_IfCurrentUserDoesNotMatchDtoUserId()
    {
        // Arrange - user1 is trying to create a token on behalf of user2
        var dto = new CreateUserModelTokenRequestDto
        {
            UserId = uid2,
            AiModelConfigId = mcid1,
            Token = "sk-sneaky-token"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _userModelTokenBusiness.CreateUserModelToken(uid, dto));
    }

    [Fact]
    public async Task CreateUserModelToken_Fails_IfDtoIsInvalid()
    {
        // Arrange - missing required fields (Token null/empty, no UserId, etc.)
        var dto = new CreateUserModelTokenRequestDto
        {
            UserId = uid,
            AiModelConfigId = mcid1,
            Token = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _userModelTokenBusiness.CreateUserModelToken(uid, dto));
    }

    #endregion

    #region DeleteUserModelToken Tests

    [Fact]
    public async Task DeleteUserModelToken_Success_WhenExists()
    {
        // Act
        var result = await _userModelTokenBusiness.DeleteUserModelToken(uid, tid1);

        // Assert
        Assert.True(result);

        var deleted = await Context.UserModelTokens.FindAsync(tid1);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteUserModelToken_Fails_IfNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userModelTokenBusiness.DeleteUserModelToken(uid, 99999));
    }

    [Fact]
    public async Task DeleteUserModelToken_Fails_IfTokenBelongsToAnotherUser()
    {
        // Act & Assert - tid3 belongs to user2; user1 should not be able to delete it
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userModelTokenBusiness.DeleteUserModelToken(uid, tid3));

        // Verify token still exists
        var stillExists = await Context.UserModelTokens.FindAsync(tid3);
        Assert.NotNull(stillExists);
    }

    [Fact]
    public async Task DeleteUserModelToken_DoesNotDeleteOtherTokens()
    {
        // Act
        await _userModelTokenBusiness.DeleteUserModelToken(uid, tid1);

        // Assert - tid2 (also owned by user1) and tid3 (owned by user2) should be unaffected
        var tid2Exists = await Context.UserModelTokens.FindAsync(tid2);
        var tid3Exists = await Context.UserModelTokens.FindAsync(tid3);

        Assert.NotNull(tid2Exists);
        Assert.NotNull(tid3Exists);
    }

    #endregion
}