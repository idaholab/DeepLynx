using System.ComponentModel.DataAnnotations;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;
using deeplynx.helpers;
namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class UserModelTokenBusinessTests : IntegrationTestBase
{
    private UserModelTokenBusiness _userModelTokenBusiness = null!;
    private EncryptionHelper _encryptionHelper = null!;

    public long uid;
    public long uid2;
    public long oid;
    public long mcid1;
    public long mcid2;
    public long tid1;
    public long tid2;
    public long tid3;

    public UserModelTokenBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _encryptionHelper = new EncryptionHelper();
        _userModelTokenBusiness = new UserModelTokenBusiness(Context, _encryptionHelper);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // EncryptionHelper is safe to construct here because TestSuiteFixture
        // already set ENCRYPTION_KEY and ENCRYPTION_IV before any test runs
        var encryptionHelper = new EncryptionHelper();

        var user1 = new User { Name = "Test User 1", Email = "user1@test.com", Password = "test_password", IsArchived = false };
        var user2 = new User { Name = "Test User 2", Email = "user2@test.com", Password = "test_password", IsArchived = false };
        Context.Users.AddRange(user1, user2);
        await Context.SaveChangesAsync();
        uid = user1.Id;
        uid2 = user2.Id;

        var org = new Organization
        {
            Name = "Test Org",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();
        oid = org.Id;

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

        // Tokens must be stored encrypted, exactly as the business layer would store them
        var token1 = new UserModelToken
        {
            UserId = uid,
            AiModelConfigId = mcid1,
            Token = encryptionHelper.Encrypt("sk-token-user1-config1"),
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        var token2 = new UserModelToken
        {
            UserId = uid,
            AiModelConfigId = mcid2,
            Token = encryptionHelper.Encrypt("sk-token-user1-config2"),
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        var token3 = new UserModelToken
        {
            UserId = uid2,
            AiModelConfigId = mcid1,
            Token = encryptionHelper.Encrypt("sk-token-user2-config1"),
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
        var result = await _userModelTokenBusiness.GetUserTokens(uid);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == tid1);
        Assert.Contains(result, t => t.Id == tid2);
    }

    [Fact]
    public async Task GetUserTokens_DoesNotReturnOtherUsersTokens()
    {
        var result = await _userModelTokenBusiness.GetUserTokens(uid);

        Assert.DoesNotContain(result, t => t.Id == tid3);
    }

    [Fact]
    public async Task GetUserTokens_FilteredByAiModelConfigId_ReturnsCorrectToken()
    {
        var result = await _userModelTokenBusiness.GetUserTokens(uid, mcid1);

        Assert.Single(result);
        Assert.Equal(tid1, result[0].Id);
        Assert.Equal(mcid1, result[0].AiModelConfigId);
    }

    [Fact]
    public async Task GetUserTokens_FilteredByAiModelConfigId_ReturnsEmpty_WhenNoMatch()
    {
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

        var result = await _userModelTokenBusiness.GetUserTokens(uid, config3.Id);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUserTokens_ReturnsEmpty_WhenUserHasNoTokens()
    {
        var userWithNoTokens = new User
        {
            Name = "Tokenless User",
            Email = "notokens@test.com",
            Password = "password",
            IsArchived = false
        };
        Context.Users.Add(userWithNoTokens);
        await Context.SaveChangesAsync();

        var result = await _userModelTokenBusiness.GetUserTokens(userWithNoTokens.Id);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUserTokens_ReturnsAllProperties_Correctly()
    {
        var result = await _userModelTokenBusiness.GetUserTokens(uid);
        var token = result.First(t => t.Id == tid1);

        Assert.Equal(tid1, token.Id);
        Assert.Equal(uid, token.UserId);
        Assert.Equal(mcid1, token.AiModelConfigId);
        // GetUserTokens decrypts then passes through MapToDto which masks
        Assert.Equal(TokenHelper.MaskToken("sk-token-user1-config1"), token.Token);
        Assert.True(token.LastUpdatedAt > DateTime.MinValue);
    }

    #endregion

    #region GetTokenById Tests

    [Fact]
    public async Task GetTokenById_Success_WhenExists()
    {
        var result = await _userModelTokenBusiness.GetTokenById(uid, tid1);

        Assert.Equal(tid1, result.Id);
        Assert.Equal(uid, result.UserId);
        Assert.Equal(mcid1, result.AiModelConfigId);
        // GetTokenById decrypts then passes through MapToDto which masks
        Assert.Equal(TokenHelper.MaskToken("sk-token-user1-config1"), result.Token);
    }

    [Fact]
    public async Task GetTokenById_Fails_IfTokenNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userModelTokenBusiness.GetTokenById(uid, 99999));
    }

    [Fact]
    public async Task GetTokenById_Fails_IfTokenBelongsToAnotherUser()
    {
        // The user ID is baked into the query predicate, so wrong-user is indistinguishable from not found
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userModelTokenBusiness.GetTokenById(uid, tid3));
    }

    #endregion

    #region CreateUserModelToken Tests

    [Fact]
    public async Task CreateUserModelToken_Success_ReturnsCorrectValues()
    {
        // Arrange - create a fresh config so uid has no pre-existing token for it
        var now = DateTime.UtcNow;
        var freshConfig = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = null,
            ServerUrl = "https://api.openai.com",
            ModelProvider = "openai",
            ModelName = "gpt-4o-mini",
            ModelType = "llm",
            RequiresToken = true,
            Default = false,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(freshConfig);
        await Context.SaveChangesAsync();

        var dto = new CreateUserModelTokenRequestDto
        {
            AiModelConfigId = freshConfig.Id,
            Token = "sk-brand-new-token-123"
        };

        var result = await _userModelTokenBusiness.CreateUserModelToken(uid, dto);

        Assert.True(result.Id > 0);
        Assert.Equal(uid, result.UserId);
        Assert.Equal(freshConfig.Id, result.AiModelConfigId);
        // CreateUserModelToken decrypts then passes through MapToDto which masks
        Assert.Equal(TokenHelper.MaskToken("sk-brand-new-token-123"), result.Token);
        Assert.True(result.LastUpdatedAt >= now);
    }

    [Fact]
    public async Task CreateUserModelToken_Success_IsPersisted()
    {
        var freshConfig = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = null,
            ServerUrl = "https://api.openai.com",
            ModelProvider = "openai",
            ModelName = "gpt-4o-mini",
            ModelType = "embedding",
            RequiresToken = true,
            Default = false,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(freshConfig);
        await Context.SaveChangesAsync();

        var dto = new CreateUserModelTokenRequestDto
        {
            AiModelConfigId = freshConfig.Id,
            Token = "sk-persisted-token-456"
        };

        var result = await _userModelTokenBusiness.CreateUserModelToken(uid, dto);

        // Clear the change tracker so EF fetches the actual persisted value from the DB
        Context.ChangeTracker.Clear();

        var saved = await Context.UserModelTokens.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal("sk-persisted-token-456", _encryptionHelper.Decrypt(saved.Token));
        Assert.Equal(uid, saved.UserId);
    }

    [Fact]
    public async Task CreateUserModelToken_Fails_IfDtoIsInvalid()
    {
        var dto = new CreateUserModelTokenRequestDto
        {
            AiModelConfigId = mcid1,
            Token = null
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _userModelTokenBusiness.CreateUserModelToken(uid, dto));
    }

    [Fact]
    public async Task CreateUserModelToken_Fails_IfTokenAlreadyExistsForConfig()
    {
        // uid already has tid1 for mcid1 from seed data
        var dto = new CreateUserModelTokenRequestDto
        {
            AiModelConfigId = mcid1,
            Token = "sk-duplicate-token-attempt"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _userModelTokenBusiness.CreateUserModelToken(uid, dto));
    }

    #endregion

    #region DeleteUserModelToken Tests

    [Fact]
    public async Task DeleteUserModelToken_Success_WhenExists()
    {
        var result = await _userModelTokenBusiness.DeleteUserModelToken(uid, tid1);

        Assert.True(result);

        var deleted = await Context.UserModelTokens.FindAsync(tid1);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteUserModelToken_Fails_IfNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userModelTokenBusiness.DeleteUserModelToken(uid, 99999));
    }

    [Fact]
    public async Task DeleteUserModelToken_DoesNotDeleteOtherUsersTokens()
    {
        // uid2 trying to delete tid1 which belongs to uid — wrong-user is indistinguishable from not found
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _userModelTokenBusiness.DeleteUserModelToken(uid2, tid1));

        var tid1Exists = await Context.UserModelTokens.FindAsync(tid1);
        Assert.NotNull(tid1Exists);
    }

    #endregion
}