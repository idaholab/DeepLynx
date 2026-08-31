using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Cache;
using deeplynx.models;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class AiModelConfigBusinessTests : IntegrationTestBase
{
    private AiModelConfigBusiness _aiModelConfigBusiness = null!;

    private EncryptionHelper _encryptionHelper = null!;

    public long uid;  // user ID
    public long oid;  // organization ID
    public long oid2; // second organization ID
    public long pid;  // project ID
    public long pid2; // second project ID
    public long mcid1; // model config IDs
    public long mcid2;
    public long mcid3;
    public long mcid4;

    public AiModelConfigBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ENCRYPTION_KEY")))
        {
            var (key, iv) = EncryptionHelper.GenerateKeyAndIV();
            Environment.SetEnvironmentVariable("ENCRYPTION_KEY", key);
            Environment.SetEnvironmentVariable("ENCRYPTION_IV", iv);
        }

        _encryptionHelper = new EncryptionHelper();
        _aiModelConfigBusiness = new AiModelConfigBusiness(Context, _encryptionHelper);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Create user
        var user = new User
        {
            Name = "Test User",
            Email = "test.user@test.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        // Create organizations
        var org = new Organization
        {
            Name = "Test Org",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var org2 = new Organization
        {
            Name = "Test Org 2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Organizations.AddRange(org, org2);
        await Context.SaveChangesAsync();
        oid = org.Id;
        oid2 = org2.Id;

        // Create projects
        var project1 = new Project
        {
            Name = "Project 1",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var project2 = new Project
        {
            Name = "Project 2",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Projects.AddRange(project1, project2);
        await Context.SaveChangesAsync();
        pid = project1.Id;
        pid2 = project2.Id;

        // Create model configs

        var config1 = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = pid,
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

        // Org-level default config
        var config2 = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = null,
            ServerUrl = "https://api.anthropic.com",
            ModelProvider = "anthropic",
            ModelName = "claude-opus-4-6",
            ModelType = "llm",
            RequiresToken = true,
            Default = true,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        // Archived project-level config
        var config3 = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = pid,
            ServerUrl = "https://hpc.example.com",
            ModelProvider = "hpc",
            ModelName = "hpc-embed",
            ModelType = "embedding",
            RequiresToken = false,
            Default = false,
            IsArchived = true,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        // Config belonging to pid2
        var config4 = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = pid2,
            ServerUrl = "https://api.openai.com",
            ModelProvider = "open ai",
            ModelName = "text-embedding-3-large",
            ModelType = "embedding",
            RequiresToken = true,
            Default = false,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.AddRange(config1, config2, config3, config4);
        await Context.SaveChangesAsync();
        mcid1 = config1.Id;
        mcid2 = config2.Id;
        mcid3 = config3.Id;
        mcid4 = config4.Id;
    }

    #region GetAllAiModelConfigs Tests

    [Fact]
    public async Task GetAllAiModelConfigs_ReturnsOnlyForProject()
    {
        // Act
        var result = await _aiModelConfigBusiness.GetAllAiModelConfigs(oid, pid, hideArchived: true);

        // Assert - should return config1 & config2 (Org Level) (config3 is archived, config4 is pid2)
        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Id == mcid1);
    }

    [Fact]
    public async Task GetAllAiModelConfigs_ReturnsOrgLevelConfigs_WhenNoProjectId()
    {
        // Act
        var result = await _aiModelConfigBusiness.GetAllAiModelConfigs(oid, null, hideArchived: true);

        // Assert - should return config2 (org-level, not archived)
        Assert.Single(result);
        Assert.Contains(result, c => c.Id == mcid2);
    }

    [Fact]
    public async Task GetAllAiModelConfigs_HideArchivedFalse_IncludesArchived()
    {
        // Act
        var result = await _aiModelConfigBusiness.GetAllAiModelConfigs(oid, pid, hideArchived: false);

        // Assert - should include config1 and config3 (archived) + config2 (Org Level) 
        Assert.Equal(3, result.Count);
        Assert.Contains(result, c => c.Id == mcid1);
        Assert.Contains(result, c => c.Id == mcid3 && c.IsArchived);
    }

    [Fact]
    public async Task GetAllAiModelConfigs_HideArchivedTrue_ExcludesArchived()
    {
        // Act
        var result = await _aiModelConfigBusiness.GetAllAiModelConfigs(oid, pid, hideArchived: true);

        // Assert
        Assert.DoesNotContain(result, c => c.Id == mcid3);
        Assert.All(result, c => Assert.False(c.IsArchived));
    }

    [Fact]
    public async Task GetAllAiModelConfigs_DoesNotReturnConfigsFromOtherOrgs()
    {
        // Act
        var result = await _aiModelConfigBusiness.GetAllAiModelConfigs(oid2, null, hideArchived: false);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAiModelConfigs_ReturnsAllProperties_Correctly()
    {
        // Act
        var result = await _aiModelConfigBusiness.GetAllAiModelConfigs(oid, pid, hideArchived: false);
        var config = result.First(c => c.Id == mcid1);

        // Assert
        Assert.Equal(mcid1, config.Id);
        Assert.Equal(oid, config.OrganizationId);
        Assert.Equal(pid, config.ProjectId);
        Assert.Equal("https://api.openai.com", config.ServerUrl);
        Assert.Equal("open ai", config.ModelProvider);
        Assert.Equal("gpt-4o", config.ModelName);
        Assert.Equal("llm", config.ModelType);
        Assert.True(config.RequiresToken);
        Assert.True(config.Default);
        Assert.False(config.IsArchived);
        Assert.Equal(uid, config.LastUpdatedBy);
    }

    #endregion

    #region GetAiModelConfig Tests

    [Fact]
    public async Task GetAiModelConfig_Success_WhenExists()
    {
        // Act
        var result = await _aiModelConfigBusiness.GetAiModelConfig(oid, pid, mcid1, hideArchived: true);

        // Assert
        Assert.Equal(mcid1, result.Id);
        Assert.Equal("gpt-4o", result.ModelName);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task GetAiModelConfig_Success_OrgLevel()
    {
        // Act
        var result = await _aiModelConfigBusiness.GetAiModelConfig(oid, null, mcid2, hideArchived: true);

        // Assert
        Assert.Equal(mcid2, result.Id);
        Assert.Null(result.ProjectId);
    }

    [Fact]
    public async Task GetAiModelConfig_Fails_IfNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.GetAiModelConfig(oid, pid, 99999, hideArchived: true));
    }

    [Fact]
    public async Task GetAiModelConfig_Fails_IfWrongProject()
    {
        // Act & Assert - config4 belongs to pid2
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.GetAiModelConfig(oid, pid, mcid4, hideArchived: true));
    }

    [Fact]
    public async Task GetAiModelConfig_Fails_IfArchived_AndHideArchivedTrue()
    {
        // Act & Assert - config3 is archived
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.GetAiModelConfig(oid, pid, mcid3, hideArchived: true));
    }

    [Fact]
    public async Task GetAiModelConfig_Success_IfArchived_AndHideArchivedFalse()
    {
        // Act
        var result = await _aiModelConfigBusiness.GetAiModelConfig(oid, pid, mcid3, hideArchived: false);

        // Assert
        Assert.Equal(mcid3, result.Id);
        Assert.True(result.IsArchived);
    }

    [Fact]
    public async Task GetAiModelConfig_Fails_IfWrongOrganization()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.GetAiModelConfig(oid2, pid, mcid1, hideArchived: false));
    }

    #endregion

    #region GetDefaultAiModelConfig Tests

    [Fact]
    public async Task GetDefaultAiModelConfig_Success_ReturnsProjectLevelDefault()
    {
        // Act - config1 is the default llm for pid
        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(oid, pid, "llm");

        // Assert
        Assert.Equal(mcid1, result.Id);
        Assert.Equal(pid, result.ProjectId);
        Assert.True(result.Default);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_Success_ReturnsOrgLevelDefault_WhenNoProjectId()
    {
        // Act - config2 is the default language model at org level
        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(oid, null, "llm");

        // Assert
        Assert.Equal(mcid2, result.Id);
        Assert.Null(result.ProjectId);
        Assert.True(result.Default);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_Success_FallsBackToOrgDefault_WhenNoProjectLevelDefault()
    {
        // Act - pid2 has no project-level default llm, should fall back to org-level default (config2)
        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(oid, pid2, "llm");

        // Assert
        Assert.Equal(mcid2, result.Id);
        Assert.Null(result.ProjectId);
        Assert.True(result.Default);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_Fails_WhenNoDefaultExistsForModelType()
    {
        // Act & Assert - no default embedding config exists at org or project level
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.GetDefaultAiModelConfig(oid, pid, "embedding"));
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_Fails_WhenWrongOrganization()
    {
        // Act & Assert - oid2 has no configs at all
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.GetDefaultAiModelConfig(oid2, null, "llm"));
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_DoesNotReturn_ArchivedConfig()
    {
        // Arrange - archive both defaults so no non-archived default remains
        var config1 = await Context.AiModelConfigs.FindAsync(mcid1);
        var config2 = await Context.AiModelConfigs.FindAsync(mcid2);
        config1!.IsArchived = true;
        config2!.IsArchived = true;
        await Context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.GetDefaultAiModelConfig(oid, pid, "llm"));
    }

    #endregion

    #region GetDefaultAiModelConfigWithToken Tests

    [Fact]
    public async Task GetDefaultAiModelConfigWithToken_ReturnsToken_WhenModelRequiresToken()
    {
        var userToken = new UserModelToken
        {
            UserId = uid,
            AiModelConfigId = mcid1,
            Token = _encryptionHelper.Encrypt("test-token-abc123")
        };
        Context.UserModelTokens.Add(userToken);
        await Context.SaveChangesAsync();

        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfigWithToken(uid, oid, pid, "llm");

        Assert.Equal(mcid1, result.Id);
        Assert.Equal("test-token-abc123", result.Token);
    }

    [Fact]
    public async Task GetDefaultAiModelConfigWithToken_ReturnsNullToken_WhenModelDoesNotRequireToken()
    {
        // Arrange - create a default embedding config that doesn't require a token
        var noTokenConfig = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = null,
            ServerUrl = "https://hpc.example.com",
            ModelProvider = "hpc",
            ModelName = "hpc-embed",
            ModelType = "embedding",
            RequiresToken = false,
            Default = true,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(noTokenConfig);
        await Context.SaveChangesAsync();

        // Act
        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfigWithToken(uid, oid, null, "embedding");

        // Assert
        Assert.Equal(noTokenConfig.Id, result.Id);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task GetDefaultAiModelConfigWithToken_Throws_WhenModelRequiresToken_ButNoneStoredForUser()
    {
        // Act & Assert - config1 requires a token but no UserModelToken exists for uid
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _aiModelConfigBusiness.GetDefaultAiModelConfigWithToken(uid, oid, pid, "llm"));
    }

    [Fact]
    public async Task GetDefaultAiModelConfigWithToken_ReturnsCorrectToken_ForCorrectUser()
    {
        var otherUser = new User
        {
            Name = "Other User",
            Email = "other.user@test.com",
            Password = "other_password",
            IsArchived = false
        };
        Context.Users.Add(otherUser);
        await Context.SaveChangesAsync();

        Context.UserModelTokens.AddRange(
            new UserModelToken { UserId = uid, AiModelConfigId = mcid1, Token = _encryptionHelper.Encrypt("token-for-uid") },
            new UserModelToken { UserId = otherUser.Id, AiModelConfigId = mcid1, Token = _encryptionHelper.Encrypt("token-for-other-user") }
        );
        await Context.SaveChangesAsync();

        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfigWithToken(uid, oid, pid, "llm");

        Assert.Equal("token-for-uid", result.Token);
    }

    #endregion

    #region CreateAiModelConfig Tests

    [Fact]
    public async Task CreateAiModelConfig_Success_ReturnsCorrectValues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "https://new.api.com",
            ModelProvider = "anthropic",
            ModelName = "claude-sonnet-4-6",
            ModelType = "llm",
            RequiresToken = true,
            Default = false
        };

        // Act
        var result = await _aiModelConfigBusiness.CreateAiModelConfig(uid, oid, pid, dto);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Equal(oid, result.OrganizationId);
        Assert.Equal(pid, result.ProjectId);
        Assert.Equal("https://new.api.com", result.ServerUrl);
        Assert.Equal("anthropic", result.ModelProvider);
        Assert.Equal("claude-sonnet-4-6", result.ModelName);
        Assert.Equal("llm", result.ModelType);
        Assert.True(result.RequiresToken);
        Assert.False(result.Default);
        Assert.False(result.IsArchived);
        Assert.Equal(uid, result.LastUpdatedBy);
        Assert.True(result.LastUpdatedAt >= now);
    }

    [Fact]
    public async Task CreateAiModelConfig_Success_OrgLevel()
    {
        // Arrange
        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "https://org.api.com",
            ModelProvider = "hpc",
            ModelName = "hpc-llm",
            ModelType = "llm",
            RequiresToken = false,
            Default = false
        };

        // Act
        var result = await _aiModelConfigBusiness.CreateAiModelConfig(uid, oid, null, dto);

        // Assert
        Assert.Equal(oid, result.OrganizationId);
        Assert.Null(result.ProjectId);
    }

    [Fact]
    public async Task CreateAiModelConfig_Success_AsDefault_ResetsOtherProjectDefaults()
    {
        // Arrange - config1 is currently the default for pid
        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "https://new-default.api.com",
            ModelProvider = "anthropic",
            ModelName = "new-default-model",
            ModelType = "llm",
            RequiresToken = true,
            Default = true
        };

        // Act
        var result = await _aiModelConfigBusiness.CreateAiModelConfig(uid, oid, pid, dto);

        // Assert
        Assert.True(result.Default);

        // Previous default (config1) should no longer be default
        Context.ChangeTracker.Clear();
        var previousDefault = await Context.AiModelConfigs.FindAsync(mcid1);
        Assert.False(previousDefault.Default);
    }

    [Fact]
    public async Task CreateAiModelConfig_Success_AsDefault_ResetsOtherOrgDefaults()
    {
        // Arrange - config2 is currently the default at org level
        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "https://new-org-default.api.com",
            ModelProvider = "anthropic",
            ModelName = "new-org-default-model",
            ModelType = "llm",
            RequiresToken = true,
            Default = true
        };

        // Act
        var result = await _aiModelConfigBusiness.CreateAiModelConfig(uid, oid, null, dto);

        // Assert
        Assert.True(result.Default);

        Context.ChangeTracker.Clear();
        var previousDefault = await Context.AiModelConfigs.FindAsync(mcid2);
        Assert.False(previousDefault.Default);
    }

    [Fact]
    public async Task CreateAiModelConfig_Fails_WithUnknownModelProvider()
    {
        // Arrange
        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "https://api.example.com",
            ModelProvider = "not-a-real-provider",
            ModelName = "some-model",
            ModelType = "llm",
            RequiresToken = false,
            Default = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _aiModelConfigBusiness.CreateAiModelConfig(uid, oid, pid, dto));
    }

    [Fact]
    public async Task CreateAiModelConfig_Fails_WithUnknownModelType()
    {
        // Arrange
        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "https://api.example.com",
            ModelProvider = "anthropic",
            ModelName = "some-model",
            ModelType = "not-a-real-type",
            RequiresToken = false,
            Default = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _aiModelConfigBusiness.CreateAiModelConfig(uid, oid, pid, dto));
    }

    [Fact]
    public async Task CreateAiModelConfig_Success_ModelProviderIsCaseInsensitive()
    {
        // Arrange
        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "https://api.anthropic.com",
            ModelProvider = "Anthropic", // mixed case
            ModelName = "claude-haiku-4-5",
            ModelType = "Llm", // mixed case
            RequiresToken = true,
            Default = false
        };

        // Act & Assert - should not throw
        var result = await _aiModelConfigBusiness.CreateAiModelConfig(uid, oid, pid, dto);
        Assert.NotNull(result);
    }

    #endregion

    #region UpdateAiModelConfig Tests

    [Fact]
    public async Task UpdateAiModelConfig_Success_ReturnsCorrectValues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new UpdateAiModelConfigDto
        {
            ModelName = "gpt-4o-mini",
            ServerUrl = "https://updated.openai.com"
        };

        // Act
        var result = await _aiModelConfigBusiness.UpdateAiModelConfig(uid, oid, pid, mcid1, dto);

        // Assert
        Assert.Equal(mcid1, result.Id);
        Assert.Equal("gpt-4o-mini", result.ModelName);
        Assert.Equal("https://updated.openai.com", result.ServerUrl);
        Assert.Equal(uid, result.LastUpdatedBy);
        Assert.True(result.LastUpdatedAt >= now);
    }

    [Fact]
    public async Task UpdateAiModelConfig_PartialUpdate_PreservesUnchangedFields()
    {
        // Arrange
        var dto = new UpdateAiModelConfigDto
        {
            ModelName = "gpt-4-turbo"
            // All other fields null -> should be unchanged
        };

        // Act
        var result = await _aiModelConfigBusiness.UpdateAiModelConfig(uid, oid, pid, mcid1, dto);

        // Assert
        Assert.Equal("gpt-4-turbo", result.ModelName);
        Assert.Equal("https://api.openai.com", result.ServerUrl); // unchanged
        Assert.Equal("llm", result.ModelType);                    // unchanged
        Assert.True(result.RequiresToken);                        // unchanged
        Assert.True(result.Default);                              // unchanged
    }

    [Fact]
    public async Task UpdateAiModelConfig_Fails_IfNotFound()
    {
        // Arrange
        var dto = new UpdateAiModelConfigDto { ModelName = "updated" };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.UpdateAiModelConfig(uid, oid, pid, 99999, dto));
    }

    [Fact]
    public async Task UpdateAiModelConfig_Fails_IfArchived()
    {
        // Arrange - config3 is archived
        var dto = new UpdateAiModelConfigDto { ModelName = "updated" };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.UpdateAiModelConfig(uid, oid, pid, mcid3, dto));
    }

    [Fact]
    public async Task UpdateAiModelConfig_Fails_IfWrongProject()
    {
        // Arrange - config4 belongs to pid2
        var dto = new UpdateAiModelConfigDto { ModelName = "updated" };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.UpdateAiModelConfig(uid, oid, pid, mcid4, dto));
    }

    [Fact]
    public async Task UpdateAiModelConfig_Fails_IfUnassigningDefault_WithoutNewDefault()
    {
        // Arrange - config1 is currently the default; trying to set Default = false should fail
        var dto = new UpdateAiModelConfigDto { Default = false };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _aiModelConfigBusiness.UpdateAiModelConfig(uid, oid, pid, mcid1, dto));
    }

    [Fact]
    public async Task UpdateAiModelConfig_Success_PromotingToDefault_ResetsOtherProjectDefaults()
    {
        // Arrange - create a non-default config to promote
        var newConfig = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = pid,
            ServerUrl = "https://hpc.example.com",
            ModelProvider = "hpc",
            ModelName = "hpc-model",
            ModelType = "llm",
            RequiresToken = false,
            Default = false,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(newConfig);
        await Context.SaveChangesAsync();

        var dto = new UpdateAiModelConfigDto { Default = true };

        // Act
        var result = await _aiModelConfigBusiness.UpdateAiModelConfig(uid, oid, pid, newConfig.Id, dto);

        // Assert
        Assert.True(result.Default);

        Context.ChangeTracker.Clear();
        var previousDefault = await Context.AiModelConfigs.FindAsync(mcid1);
        Assert.False(previousDefault.Default);
    }

    [Fact]
    public async Task UpdateAiModelConfig_Fails_IfWrongOrganization()
    {
        // Arrange
        var dto = new UpdateAiModelConfigDto { ModelName = "updated" };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.UpdateAiModelConfig(uid, oid2, pid, mcid1, dto));
    }

    #endregion

    #region DeleteAiModelConfig Tests

    [Fact]
    public async Task DeleteAiModelConfig_Success_WhenExists()
    {
        // Arrange - create a non-default, non-archived config to delete
        var deletableConfig = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = pid,
            ServerUrl = "https://deleteme.api.com",
            ModelProvider = "anthropic",
            ModelName = "deletable-model",
            ModelType = "llm",
            RequiresToken = false,
            Default = false,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(deletableConfig);
        await Context.SaveChangesAsync();

        // Act
        var result = await _aiModelConfigBusiness.DeleteAiModelConfig(oid, pid, deletableConfig.Id);

        // Assert
        Assert.True(result);

        var deleted = await Context.AiModelConfigs.FindAsync(deletableConfig.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAiModelConfig_Fails_IfNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.DeleteAiModelConfig(oid, pid, 99999));
    }

    [Fact]
    public async Task DeleteAiModelConfig_Fails_IfDefault()
    {
        // Act & Assert - config1 is the default for pid
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _aiModelConfigBusiness.DeleteAiModelConfig(oid, pid, mcid1));
    }

    [Fact]
    public async Task DeleteAiModelConfig_Fails_IfWrongProject()
    {
        // Act & Assert - config4 belongs to pid2
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.DeleteAiModelConfig(oid, pid, mcid4));
    }

    [Fact]
    public async Task DeleteAiModelConfig_Fails_IfWrongOrganization()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.DeleteAiModelConfig(oid2, pid, mcid1));
    }

    #endregion

    #region ArchiveAiModelConfig Tests

    [Fact]
    public async Task ArchiveAiModelConfig_Success_WhenExists()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var archivableConfig = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = pid,
            ServerUrl = "https://archiveme.api.com",
            ModelProvider = "anthropic",
            ModelName = "archivable-model",
            ModelType = "llm",
            RequiresToken = false,
            Default = false,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(archivableConfig);
        await Context.SaveChangesAsync();

        // Act
        var result = await _aiModelConfigBusiness.ArchiveAiModelConfig(uid, oid, pid, archivableConfig.Id);

        // Assert
        Assert.True(result);

        Context.ChangeTracker.Clear();
        var archived = await Context.AiModelConfigs.FindAsync(archivableConfig.Id);
        Assert.NotNull(archived);
        Assert.True(archived.IsArchived);
        Assert.True(archived.LastUpdatedAt >= now);
        Assert.Equal(uid, archived.LastUpdatedBy);
    }

    [Fact]
    public async Task ArchiveAiModelConfig_Fails_IfNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.ArchiveAiModelConfig(uid, oid, pid, 99999));
    }

    [Fact]
    public async Task ArchiveAiModelConfig_Fails_IfAlreadyArchived()
    {
        // Act & Assert - config3 is already archived
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _aiModelConfigBusiness.ArchiveAiModelConfig(uid, oid, pid, mcid3));
    }

    [Fact]
    public async Task ArchiveAiModelConfig_Fails_IfDefault()
    {
        // Act & Assert - config1 is the default
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _aiModelConfigBusiness.ArchiveAiModelConfig(uid, oid, pid, mcid1));
    }

    [Fact]
    public async Task ArchiveAiModelConfig_Fails_IfWrongProject()
    {
        // Act & Assert - config4 belongs to pid2
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.ArchiveAiModelConfig(uid, oid, pid, mcid4));
    }

    [Fact]
    public async Task ArchiveAiModelConfig_Fails_IfWrongOrganization()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.ArchiveAiModelConfig(uid, oid2, pid, mcid1));
    }

    [Fact]
    public async Task ArchiveAiModelConfig_OrgLevel_Success()
    {
        // Arrange - first remove the default flag so we can archive config2
        var config2 = await Context.AiModelConfigs.FindAsync(mcid2);
        config2.Default = false;
        await Context.SaveChangesAsync();

        // Act
        var result = await _aiModelConfigBusiness.ArchiveAiModelConfig(uid, oid, null, mcid2);

        // Assert
        Assert.True(result);

        Context.ChangeTracker.Clear();
        var archived = await Context.AiModelConfigs.FindAsync(mcid2);
        Assert.True(archived.IsArchived);
    }

    #endregion

    #region DefaultAiModelConfig Caching Tests

    [Fact]
    public async Task Create_WithDefaultTrue_InOrganization_CachesId()
    {
        // Arrange - no org-level default embedding config exists yet
        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "https://cache-test-org.api.com",
            ModelProvider = "hpc",
            ModelName = "cache-test-org-embed",
            ModelType = "embedding",
            RequiresToken = false,
            Default = true
        };

        // Act
        var created = await _aiModelConfigBusiness.CreateAiModelConfig(uid, oid, null, dto);

        // Assert
        var cacheKey = CacheKeys.OrganizationDefaultAiModelConfig(oid, "embedding");
        var cached = await CacheService.Instance.GetAsync<long?>(cacheKey);
        Assert.NotNull(cached);
        Assert.Equal(created.Id, cached.Value);
    }

    [Fact]
    public async Task Create_FirstOfType_InOrganization_AutoDefaults_AndCachesId()
    {
        // Arrange - dto explicitly says Default = false, but this is the first-ever
        // config of this type at the org level, so ShouldAutoDefault should force it
        // to true and the cache should reflect the persisted state, not the request.
        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "https://auto-default.api.com",
            ModelProvider = "hpc",
            ModelName = "auto-default-vlm",
            ModelType = "vlm",
            RequiresToken = false,
            Default = false
        };

        // Act
        var created = await _aiModelConfigBusiness.CreateAiModelConfig(uid, oid, null, dto);

        // Assert
        Assert.True(created.Default);
        var cacheKey = CacheKeys.OrganizationDefaultAiModelConfig(oid, created.ModelType);
        var cached = await CacheService.Instance.GetAsync<long?>(cacheKey);
        Assert.Equal(created.Id, cached);
    }

    [Fact]
    public async Task Create_FirstOfType_InProject_WithExistingOrgDefault_DoesNotAutoDefault_OrCache()
    {
        // Arrange - mcid2 is already the org-level "llm" default. Creating the first
        // project-level "llm" config with Default = false should NOT auto-promote,
        // since the org default is already reachable via fallback.
        var cacheKey = CacheKeys.ProjectDefaultAiModelConfig(pid2, "llm");
        await CacheService.Instance.DeleteAsync(cacheKey);

        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "https://no-auto-default.api.com",
            ModelProvider = "hpc",
            ModelName = "no-auto-default-llm",
            ModelType = "llm",
            RequiresToken = false,
            Default = false
        };

        try
        {
            // Act
            var created = await _aiModelConfigBusiness.CreateAiModelConfig(uid, oid, pid2, dto);

            // Assert
            Assert.False(created.Default);
            Assert.Null(await CacheService.Instance.GetAsync<long?>(cacheKey));
        }
        finally
        {
            await CacheService.Instance.DeleteAsync(cacheKey);
        }
    }

    [Fact]
    public async Task Create_WithDefaultFalse_AndExistingDefaultOfType_DoesNotWriteCache()
    {
        // Arrange - config2 (mcid2) is already the org-level "llm" default, so this
        // create should not be auto-promoted and must not touch the cache.
        var cacheKey = CacheKeys.OrganizationDefaultAiModelConfig(oid, "llm");
        await CacheService.Instance.DeleteAsync(cacheKey);

        var dto = new CreateAiModelConfigDto
        {
            ServerUrl = "https://nondefault.api.com",
            ModelProvider = "anthropic",
            ModelName = "nondefault-llm",
            ModelType = "llm",
            RequiresToken = true,
            Default = false
        };

        try
        {
            // Act
            await _aiModelConfigBusiness.CreateAiModelConfig(uid, oid, null, dto);

            // Assert
            Assert.Null(await CacheService.Instance.GetAsync<long?>(cacheKey));
        }
        finally
        {
            await CacheService.Instance.DeleteAsync(cacheKey);
        }
    }

    [Fact]
    public async Task GetDefault_InProject_CacheMiss_PopulatesCache()
    {
        // Arrange - config1 (mcid1) is the DB default llm for pid
        var cacheKey = CacheKeys.ProjectDefaultAiModelConfig(pid, "llm");
        await CacheService.Instance.DeleteAsync(cacheKey);

        try
        {
            // Act
            var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(oid, pid, "llm");

            // Assert
            var cached = await CacheService.Instance.GetAsync<long?>(cacheKey);
            Assert.Equal(mcid1, result.Id);
            Assert.Equal(mcid1, cached);
        }
        finally
        {
            await CacheService.Instance.DeleteAsync(cacheKey);
        }
    }

    [Fact]
    public async Task GetDefault_InOrganization_CacheMiss_PopulatesCache()
    {
        // Arrange - config2 (mcid2) is the DB default llm at the org level
        var cacheKey = CacheKeys.OrganizationDefaultAiModelConfig(oid, "llm");
        await CacheService.Instance.DeleteAsync(cacheKey);

        try
        {
            // Act
            var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(oid, null, "llm");

            // Assert
            var cached = await CacheService.Instance.GetAsync<long?>(cacheKey);
            Assert.Equal(mcid2, result.Id);
            Assert.Equal(mcid2, cached);
        }
        finally
        {
            await CacheService.Instance.DeleteAsync(cacheKey);
        }
    }

    [Fact]
    public async Task GetDefault_InProject_StaleCacheEntry_SelfHeals()
    {
        // Arrange - config1 (mcid1) is the real DB default llm for pid. Create a
        // second, non-default llm config and deliberately cache its id, simulating
        // a stale entry left over after the real default changed. The read path
        // must reject it (it is no longer Default == true) and fall back to the DB.
        var staleConfig = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = pid,
            ServerUrl = "https://stale.api.com",
            ModelProvider = "anthropic",
            ModelName = "stale-llm",
            ModelType = "llm",
            RequiresToken = false,
            Default = false,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(staleConfig);
        await Context.SaveChangesAsync();

        var cacheKey = CacheKeys.ProjectDefaultAiModelConfig(pid, "llm");
        await CacheService.Instance.SetAsync(cacheKey, staleConfig.Id, TimeSpan.FromMinutes(2));

        try
        {
            // Act
            var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(oid, pid, "llm");

            // Assert - the stale, non-default id is rejected; the real default wins
            // and the cache is repopulated to point at it
            Assert.Equal(mcid1, result.Id);
            var repopulated = await CacheService.Instance.GetAsync<long?>(cacheKey);
            Assert.Equal(mcid1, repopulated);
        }
        finally
        {
            await CacheService.Instance.DeleteAsync(cacheKey);
        }
    }

    [Fact]
    public async Task GetDefault_FallsBackToOrganizationCache_WhenNoProjectLevelDefault()
    {
        // Arrange - create an org-level default embedding config; pid has no
        // project-level default embedding config
        var orgDefaultEmbedding = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = null,
            ServerUrl = "https://org-embed.api.com",
            ModelProvider = "hpc",
            ModelName = "org-default-embed",
            ModelType = "embedding",
            RequiresToken = false,
            Default = true,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(orgDefaultEmbedding);
        await Context.SaveChangesAsync();

        var projectCacheKey = CacheKeys.ProjectDefaultAiModelConfig(pid, "embedding");
        var organizationCacheKey = CacheKeys.OrganizationDefaultAiModelConfig(oid, "embedding");
        await CacheService.Instance.DeleteAsync(projectCacheKey);
        await CacheService.Instance.DeleteAsync(organizationCacheKey);

        try
        {
            // Act - request scoped to pid, which has no project-level embedding default
            var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(oid, pid, "embedding");

            // Assert - falls back to, and caches, the org-level default
            Assert.Equal(orgDefaultEmbedding.Id, result.Id);
            var cachedOrganizationId = await CacheService.Instance.GetAsync<long?>(organizationCacheKey);
            Assert.Equal(orgDefaultEmbedding.Id, cachedOrganizationId);

            // The project-level cache key should remain unpopulated - the fallback
            // was resolved and cached at the org level, not the project level
            Assert.Null(await CacheService.Instance.GetAsync<long?>(projectCacheKey));
        }
        finally
        {
            await CacheService.Instance.DeleteAsync(projectCacheKey);
            await CacheService.Instance.DeleteAsync(organizationCacheKey);
        }
    }

    [Fact]
    public async Task Update_PromotingToDefault_InProject_CachesId()
    {
        // Arrange - create a non-default llm config in pid2 (which has no llm default at all yet)
        var newConfig = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = pid2,
            ServerUrl = "https://promote-me.api.com",
            ModelProvider = "hpc",
            ModelName = "promote-me-llm",
            ModelType = "llm",
            RequiresToken = false,
            Default = false,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(newConfig);
        await Context.SaveChangesAsync();

        var cacheKey = CacheKeys.ProjectDefaultAiModelConfig(pid2, "llm");

        try
        {
            // Act
            var dto = new UpdateAiModelConfigDto { Default = true };
            var result = await _aiModelConfigBusiness.UpdateAiModelConfig(uid, oid, pid2, newConfig.Id, dto);

            // Assert
            Assert.True(result.Default);
            var cached = await CacheService.Instance.GetAsync<long?>(cacheKey);
            Assert.Equal(newConfig.Id, cached);
        }
        finally
        {
            await CacheService.Instance.DeleteAsync(cacheKey);
        }
    }

    [Fact]
    public async Task Update_PromotingAnotherConfig_SupersedesStaleCacheEntry()
    {
        // Arrange - config2 (mcid2) is the current org "llm" default and is cached.
        // Create a second llm config, then promote IT to default instead. The
        // old cache entry (pointing at mcid2) should be superseded by the new
        // write - no explicit invalidation needed, since the write overwrites
        // the single (scope, type) key, and even if it didn't, mcid2 no longer
        // satisfies Default == true and would self-heal on next read.
        await _aiModelConfigBusiness.GetDefaultAiModelConfig(oid, null, "llm"); // warm cache -> mcid2
        var cacheKey = CacheKeys.OrganizationDefaultAiModelConfig(oid, "llm");
        Assert.Equal(mcid2, await CacheService.Instance.GetAsync<long?>(cacheKey));

        var challenger = new AiModelConfig
        {
            OrganizationId = oid,
            ProjectId = null,
            ServerUrl = "https://challenger.api.com",
            ModelProvider = "anthropic",
            ModelName = "challenger-llm",
            ModelType = "llm",
            RequiresToken = false,
            Default = false,
            IsArchived = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.AiModelConfigs.Add(challenger);
        await Context.SaveChangesAsync();

        try
        {
            // Act
            var dto = new UpdateAiModelConfigDto { Default = true };
            var result = await _aiModelConfigBusiness.UpdateAiModelConfig(uid, oid, null, challenger.Id, dto);

            // Assert - cache now points at the challenger, not mcid2
            Assert.True(result.Default);
            var cached = await CacheService.Instance.GetAsync<long?>(cacheKey);
            Assert.Equal(challenger.Id, cached);
        }
        finally
        {
            await CacheService.Instance.DeleteAsync(cacheKey);
            // restore mcid2 as default for other tests relying on fixture state
            var org = await Context.AiModelConfigs.FindAsync(mcid2);
            var chal = await Context.AiModelConfigs.FindAsync(challenger.Id);
            if (org != null) org.Default = true;
            if (chal != null) chal.Default = false;
            await Context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Update_AlreadyDefault_ChangingModelType_ThrowsAndDoesNotTouchCache()
    {
        // Arrange - config2 (mcid2) is the org "llm" default. Attempting to change
        // its ModelType while it remains the default is rejected by the business
        // layer (the caller must reassign the default first), so the cache under
        // the original key must be untouched and no key should be created for "vlm".
        var oldCacheKey = CacheKeys.OrganizationDefaultAiModelConfig(oid, "llm");
        var newCacheKey = CacheKeys.OrganizationDefaultAiModelConfig(oid, "vlm");
        await CacheService.Instance.DeleteAsync(oldCacheKey);
        await CacheService.Instance.DeleteAsync(newCacheKey);

        await _aiModelConfigBusiness.GetDefaultAiModelConfig(oid, null, "llm"); // warm cache
        Assert.Equal(mcid2, await CacheService.Instance.GetAsync<long?>(oldCacheKey));

        try
        {
            // Act / Assert
            var dto = new UpdateAiModelConfigDto { ModelType = "vlm" };
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _aiModelConfigBusiness.UpdateAiModelConfig(uid, oid, null, mcid2, dto));

            // The original cache entry is untouched, and no new key was created
            Assert.Equal(mcid2, await CacheService.Instance.GetAsync<long?>(oldCacheKey));
            Assert.Null(await CacheService.Instance.GetAsync<long?>(newCacheKey));
        }
        finally
        {
            await CacheService.Instance.DeleteAsync(oldCacheKey);
            await CacheService.Instance.DeleteAsync(newCacheKey);
        }
    }

    #endregion
}
