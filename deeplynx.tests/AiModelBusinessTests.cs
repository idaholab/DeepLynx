using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class AiModelConfigBusinessTests : IntegrationTestBase
{
    private AiModelConfigBusiness _aiModelConfigBusiness = null!;

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
        _aiModelConfigBusiness = new AiModelConfigBusiness(Context);
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
            ModelType = "language",
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
            ModelType = "language",
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
        Assert.Equal("language", config.ModelType);
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
        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(uid, oid, pid, "language");

        // Assert
        Assert.Equal(mcid1, result.Id);
        Assert.Equal(pid, result.ProjectId);
        Assert.True(result.Default);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_Success_ReturnsOrgLevelDefault_WhenNoProjectId()
    {
        // Act - config2 is the default language model at org level
        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(uid, oid, null, "language");

        // Assert
        Assert.Equal(mcid2, result.Id);
        Assert.Null(result.ProjectId);
        Assert.True(result.Default);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_Success_FallsBackToOrgDefault_WhenNoProjectLevelDefault()
    {
        // Act - pid has no default embedding config, should fall back to org level
        // pid2 has no project-level default language model, so should fall back to org-level default (config2)
        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(uid, oid, pid2, "language");

        // Assert - should have fallen back to org-level default (config2)
        Assert.Equal(mcid2, result.Id);
        Assert.Null(result.ProjectId);
        Assert.True(result.Default);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_Fails_WhenNoDefaultExistsForModelType()
    {
        // Act & Assert - no default embedding config exists at org or project level
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.GetDefaultAiModelConfig(uid, oid, pid, "embedding"));
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_Fails_WhenWrongOrganization()
    {
        // Act & Assert - oid2 has no configs at all
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.GetDefaultAiModelConfig(uid, oid2, null, "language"));
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_DoesNotReturn_ArchivedConfig()
    {
        // Arrange - archive config1 (project-level default) and config2 (org-level default)
        // so the only remaining llm default would be archived
        var config1 = await Context.AiModelConfigs.FindAsync(mcid1);
        var config2 = await Context.AiModelConfigs.FindAsync(mcid2);
        config1!.IsArchived = true;
        config2!.IsArchived = true;
        await Context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _aiModelConfigBusiness.GetDefaultAiModelConfig(uid, oid, pid, "language"));
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_ReturnsToken_WhenModelRequiresToken()
    {
        // Arrange - add a token for uid against config1
        var userToken = new UserModelToken
        {
            UserId = uid,
            AiModelConfigId = mcid1,
            Token = "test-token-abc123"
        };
        Context.UserModelTokens.Add(userToken);
        await Context.SaveChangesAsync();

        // Act
        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(uid, oid, pid, "language");

        // Assert
        Assert.Equal(mcid1, result.Id);
        Assert.Equal("test-token-abc123", result.Token);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_ReturnsNullToken_WhenModelDoesNotRequireToken()
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
        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(uid, oid, null, "embedding");

        // Assert
        Assert.Equal(noTokenConfig.Id, result.Id);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_ReturnsNullToken_WhenModelRequiresToken_ButNoneStoredForUser()
    {
        // Act - config1 requires a token but no UserModelToken exists for uid
        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(uid, oid, pid, "language");

        // Assert
        Assert.Equal(mcid1, result.Id);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task GetDefaultAiModelConfig_ReturnsCorrectToken_ForCorrectUser()
    {
        // Arrange - add tokens for two different users against config1
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
            new UserModelToken { UserId = uid, AiModelConfigId = mcid1, Token = "token-for-uid" },
            new UserModelToken { UserId = otherUser.Id, AiModelConfigId = mcid1, Token = "token-for-other-user" }
        );
        await Context.SaveChangesAsync();

        // Act
        var result = await _aiModelConfigBusiness.GetDefaultAiModelConfig(uid, oid, pid, "language");

        // Assert - should only return the token belonging to uid
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
            ModelType = "language",
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
        Assert.Equal("language", result.ModelType);
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
            ModelType = "language",
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
            ModelType = "language",
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
            ModelType = "language",
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
            ModelType = "language",
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
            ModelType = "Language", // mixed case
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
        Assert.Equal("language", result.ModelType);                    // unchanged
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
            ModelType = "language",
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
            ModelType = "language",
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
            ModelType = "language",
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
}