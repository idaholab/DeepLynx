using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Cache;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

public class AiModelConfigBusiness : IAiModelConfigBusiness
{
    private readonly DeeplynxContext _context;
    private readonly EncryptionHelper _encryptionHelper;
    private readonly ILogger<AiModelConfigBusiness>? _logger;
    private static readonly TimeSpan AiModelConfigCacheTtl = TimeSpan.FromHours(1);

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiModelConfigBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for operations.</param>
    /// <param name="logger">Optional logger used for cache read/write/invalidation warnings.</param>
    public AiModelConfigBusiness(DeeplynxContext context, EncryptionHelper encryptionHelper,
        ILogger<AiModelConfigBusiness>? logger = null)
    {
        _context = context;
        _encryptionHelper = encryptionHelper;
        _logger = logger;
    }

    private static readonly List<string> ModelProviderList = new List<string>
    {
        "openai",
        "anthropic",
        "hpc",
        "ollama"
    };

    private static readonly List<string> ModelTypeList = new List<string>
    {
        "llm",
        "vlm",
        "embedding"
    };

    /// <summary>
    ///     Retrieves all AI Model Configurations for a organization or optionally a specific project in an Organization
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose AI Model Configurations will be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived Model Configs from the result</param>
    /// <returns>A list of AI Model Configs based on the applied filters.</returns>
    public async Task<List<AiModelConfigResponseDto>> GetAllAiModelConfigs(
        long organizationId,
        long? projectId,
        bool hideArchived)
    {
        var query = _context.AiModelConfigs
            .Where(x => x.OrganizationId == organizationId)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(x => x.ProjectId == projectId || x.ProjectId == null);
        else
            query = query.Where(x => x.ProjectId == null);

        if (hideArchived)
            query = query.Where(x => !x.IsArchived);

        return await query
            .Select(x => new AiModelConfigResponseDto
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                ProjectId = x.ProjectId,
                ServerUrl = x.ServerUrl,
                ModelProvider = x.ModelProvider,
                ModelName = x.ModelName,
                ModelType = x.ModelType,
                RequiresToken = x.RequiresToken,
                Default = x.Default,
                IsArchived = x.IsArchived,
                LastUpdatedAt = x.LastUpdatedAt,
                LastUpdatedBy = x.LastUpdatedBy
            }).ToListAsync();
    }

    /// <summary>
    ///     Retrieves a single AI Model Configuration
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose AI Model Configurations will be retrieved</param>
    /// <param name="aiModelConfigId">The ID of the AI Model Configuration that will be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived Model Configs from the result</param>
    /// <returns> A single AI Model Config based on the provided filters.</returns>
    public async Task<AiModelConfigResponseDto> GetAiModelConfig(
        long organizationId,
        long? projectId,
        long aiModelConfigId,
        bool hideArchived)
    {
        var query = _context.AiModelConfigs
            .Where(x => x.Id == aiModelConfigId && x.OrganizationId == organizationId)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(x => x.ProjectId == projectId || x.ProjectId == null);

        var returnedAiModelConfig = await query.FirstOrDefaultAsync();
        if (returnedAiModelConfig is null)
            throw new KeyNotFoundException($"Ai Model Config with id {aiModelConfigId} not found");
        if (hideArchived && returnedAiModelConfig.IsArchived)
            throw new KeyNotFoundException($"Ai Model Config with id {aiModelConfigId} is archived");

        return new AiModelConfigResponseDto
        {
            Id = returnedAiModelConfig.Id,
            OrganizationId = returnedAiModelConfig.OrganizationId,
            ProjectId = returnedAiModelConfig.ProjectId,
            ServerUrl = returnedAiModelConfig.ServerUrl,
            ModelProvider = returnedAiModelConfig.ModelProvider,
            ModelName = returnedAiModelConfig.ModelName,
            ModelType = returnedAiModelConfig.ModelType,
            RequiresToken = returnedAiModelConfig.RequiresToken,
            Default = returnedAiModelConfig.Default,
            IsArchived = returnedAiModelConfig.IsArchived,
            LastUpdatedAt = returnedAiModelConfig.LastUpdatedAt,
            LastUpdatedBy = returnedAiModelConfig.LastUpdatedBy
        };
    }

    /// <summary>
    ///     Retrieves a specific AI Model Configuration by ID and resolves the user's API token if one is required.
    ///     This method is intended for internal use only and should never be called from API controllers.
    /// </summary>
    /// <param name="currentUserId">The ID of the user making the request. Used to look up a stored token if the model requires one.</param>
    /// <param name="organizationId">The ID of the organization to which the model config belongs.</param>
    /// <param name="projectId">
    ///     The ID of the project to scope the lookup to. If provided, the config must belong to that project
    ///     or have no project (org-level). If null, only org-level configs are considered.
    /// </param>
    /// <param name="aiModelConfigId">The ID of the AI Model Configuration to retrieve.</param>
    /// <returns>A <see cref="AiModelConfigResponseDto"/> containing the model config and optionally the resolved token.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no config is found for the given ID and scope, or when it is archived.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the model requires a token but none is found for the user.</exception>
    public async Task<AiModelConfigResponseDto.WithToken> GetAiModelConfigWithToken(
        long currentUserId,
        long organizationId,
        long? projectId,
        long aiModelConfigId)
    {
        var query = _context.AiModelConfigs
            .Where(x => x.Id == aiModelConfigId && x.OrganizationId == organizationId && !x.IsArchived)
            .AsQueryable();

        AiModelConfig? modelConfig;
        if (projectId.HasValue)
        {
            modelConfig = await query
                .Where(x => x.ProjectId == projectId)
                .FirstOrDefaultAsync();

            modelConfig ??= await _context.AiModelConfigs
                .Where(x => x.Id == aiModelConfigId && x.OrganizationId == organizationId && !x.IsArchived)
                .Where(x => x.ProjectId == null)
                .FirstOrDefaultAsync();
        }
        else
        {
            modelConfig = await query
                .Where(x => x.ProjectId == null)
                .FirstOrDefaultAsync();
        }

        if (modelConfig is null)
            throw new KeyNotFoundException($"AI Model Configuration with ID {aiModelConfigId} not found.");

        string? token = null;
        if (modelConfig.RequiresToken == true)
        {
            token = await _context.UserModelTokens
                .Where(t => t.UserId == currentUserId && t.AiModelConfigId == modelConfig.Id)
                .Select(t => t.Token)
                .FirstOrDefaultAsync();

            if (token == null)
                throw new InvalidOperationException(
                    $"The model configuration (ID: {modelConfig.Id}) requires an API token, " +
                    $"but none was found for user {currentUserId}. Please add a token for this model.");

            token = _encryptionHelper.Decrypt(token);
        }

        return new AiModelConfigResponseDto.WithToken
        {
            Id = modelConfig.Id,
            OrganizationId = modelConfig.OrganizationId,
            ProjectId = modelConfig.ProjectId,
            ServerUrl = modelConfig.ServerUrl,
            ModelProvider = modelConfig.ModelProvider,
            ModelName = modelConfig.ModelName,
            ModelType = modelConfig.ModelType,
            RequiresToken = modelConfig.RequiresToken,
            Default = modelConfig.Default,
            IsArchived = modelConfig.IsArchived,
            LastUpdatedAt = modelConfig.LastUpdatedAt,
            LastUpdatedBy = modelConfig.LastUpdatedBy,
            Token = token
        };
    }

    /// <summary>
    ///     Retrieves the default AI Model Configuration for the given model type, scoped to a project if provided,
    ///     otherwise falling back to the organization-level default. Optionally resolves the user's API token
    ///     for the returned model if one is required.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the model config belongs</param>
    /// <param name="projectId">
    ///     The ID of the project to scope the lookup to. If provided, project-level defaults are
    ///     preferred over organization-level defaults. If null, only organization-level defaults are considered.
    /// </param>
    /// <param name="modelType">The type of model to retrieve (e.g. "llm", "vlm" or "embedding")</param>
    /// <returns>A <see cref="AiModelConfigResponseDto"/> containing the model config and optionally the resolved token.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no default config is found for the given model type and scope.</exception>
    public async Task<AiModelConfigResponseDto> GetDefaultAiModelConfig(
        long organizationId,
        long? projectId,
        string modelType)
    {
        AiModelConfig? modelConfig = null;

        if (projectId.HasValue)
        {
            var projectCacheKey = CacheKeys.ProjectDefaultAiModelConfig(projectId.Value, modelType);

            long? cachedProjectId = null;
            try
            {
                cachedProjectId = await CacheService.Instance.GetAsync<long?>(projectCacheKey);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache read failed for default-ai-model-config key {CacheKey}", projectCacheKey);
            }

            // Check the cache before querying the db
            if (cachedProjectId.HasValue)
            {
                modelConfig = await _context.AiModelConfigs
                    .Where(c => c.Id == cachedProjectId.Value && c.ModelType == modelType && !c.IsArchived)
                    .FirstOrDefaultAsync();
            }

            if (modelConfig == null)
            {
                modelConfig = await _context.AiModelConfigs
                    .FirstOrDefaultAsync(c =>
                        c.OrganizationId == organizationId &&
                        c.ProjectId == projectId &&
                        c.ModelType == modelType &&
                        c.Default == true &&
                        !c.IsArchived);

                // Repopulate cache on miss for subsequent reads
                if (modelConfig != null)
                {
                    try
                    {
                        await CacheService.Instance.SetAsync(projectCacheKey, modelConfig.Id, AiModelConfigCacheTtl);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Cache population failed for default-ai-model-config key {CacheKey}", projectCacheKey);
                    }
                }
            }
        }

        // Fall back to org-level default if no project-level default was found
        if (modelConfig == null)
        {
            var organizationCacheKey = CacheKeys.OrganizationDefaultAiModelConfig(organizationId, modelType);

            long? cachedOrganizationId = null;
            try
            {
                cachedOrganizationId = await CacheService.Instance.GetAsync<long?>(organizationCacheKey);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache read failed for default-ai-model-config key {CacheKey}", organizationCacheKey);
            }

            // Check the cache before querying the db
            if (cachedOrganizationId.HasValue)
            {
                modelConfig = await _context.AiModelConfigs
                    .Where(c => c.Id == cachedOrganizationId.Value && c.ModelType == modelType && !c.IsArchived)
                    .FirstOrDefaultAsync();
            }

            if (modelConfig == null)
            {
                modelConfig = await _context.AiModelConfigs
                    .FirstOrDefaultAsync(c =>
                        c.OrganizationId == organizationId &&
                        c.ProjectId == null &&
                        c.ModelType == modelType &&
                        c.Default == true &&
                        !c.IsArchived);

                // Repopulate cache on miss for subsequent reads
                if (modelConfig != null)
                {
                    try
                    {
                        await CacheService.Instance.SetAsync(organizationCacheKey, modelConfig.Id, AiModelConfigCacheTtl);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Cache population failed for default-ai-model-config key {CacheKey}", organizationCacheKey);
                    }
                }
            }
        }

        if (modelConfig is null)
            throw new KeyNotFoundException(
                $"No default {modelType} model configuration found for organization {organizationId}.");

        return new AiModelConfigResponseDto
        {
            Id = modelConfig.Id,
            OrganizationId = modelConfig.OrganizationId,
            ProjectId = modelConfig.ProjectId,
            ServerUrl = modelConfig.ServerUrl,
            ModelProvider = modelConfig.ModelProvider,
            ModelName = modelConfig.ModelName,
            ModelType = modelConfig.ModelType,
            RequiresToken = modelConfig.RequiresToken,
            Default = modelConfig.Default,
            IsArchived = modelConfig.IsArchived,
            LastUpdatedAt = modelConfig.LastUpdatedAt,
            LastUpdatedBy = modelConfig.LastUpdatedBy,
        };
    }

    /// <summary>
    ///     Retrieves the default AI Model Configuration for the given model type, scoped to a project if provided,
    ///     otherwise falling back to the organization-level default. Resolves and decrypts the user's API token
    ///     if the model requires one.
    ///     This method is intended for internal use only and should never be called from API controllers.
    /// </summary>
    /// <param name="currentUserId">The ID of the user making the request. Used to look up and decrypt a stored token if the model requires one.</param>
    /// <param name="organizationId">The ID of the organization to which the model config belongs.</param>
    /// <param name="projectId">
    ///     The ID of the project to scope the lookup to. If provided, project-level defaults are
    ///     preferred over organization-level defaults. If null, only organization-level defaults are considered.
    /// </param>
    /// <param name="modelType">The type of model to retrieve (e.g. "llm", "vlm", or "embedding").</param>
    /// <returns>A <see cref="AiModelConfigWithTokenResponseDto"/> containing the model config and the decrypted token if applicable.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no default config is found for the given model type and scope, or when a required token is not found for the user.</exception>
    public async Task<AiModelConfigResponseDto.WithToken> GetDefaultAiModelConfigWithToken(
        long currentUserId,
        long organizationId,
        long? projectId,
        string modelType)
    {
        AiModelConfig? modelConfig = null;

        if (projectId.HasValue)
        {
            var projectCacheKey = CacheKeys.ProjectDefaultAiModelConfig(projectId.Value, modelType);

            long? cachedProjectId = null;
            try
            {
                cachedProjectId = await CacheService.Instance.GetAsync<long?>(projectCacheKey);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache read failed for default-ai-model-config key {CacheKey}", projectCacheKey);
            }

            // Check the cache before querying the db
            if (cachedProjectId.HasValue)
            {
                modelConfig = await _context.AiModelConfigs
                    .Where(c => c.Id == cachedProjectId.Value && c.ModelType == modelType && !c.IsArchived)
                    .FirstOrDefaultAsync();
            }

            if (modelConfig == null)
            {
                modelConfig = await _context.AiModelConfigs
                    .FirstOrDefaultAsync(c =>
                        c.OrganizationId == organizationId &&
                        c.ProjectId == projectId &&
                        c.ModelType == modelType &&
                        c.Default == true &&
                        !c.IsArchived);

                // Repopulate cache on miss for subsequent reads
                if (modelConfig != null)
                {
                    try
                    {
                        await CacheService.Instance.SetAsync(projectCacheKey, modelConfig.Id, AiModelConfigCacheTtl);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Cache population failed for default-ai-model-config key {CacheKey}", projectCacheKey);
                    }
                }
            }
        }

        if (modelConfig == null)
        {
            var organizationCacheKey = CacheKeys.OrganizationDefaultAiModelConfig(organizationId, modelType);

            long? cachedOrganizationId = null;
            try
            {
                cachedOrganizationId = await CacheService.Instance.GetAsync<long?>(organizationCacheKey);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache read failed for default-ai-model-config key {CacheKey}", organizationCacheKey);
            }

            // Check the cache before querying the db
            if (cachedOrganizationId.HasValue)
            {
                modelConfig = await _context.AiModelConfigs
                    .Where(c => c.Id == cachedOrganizationId.Value && c.ModelType == modelType && !c.IsArchived)
                    .FirstOrDefaultAsync();
            }

            if (modelConfig == null)
            {
                modelConfig = await _context.AiModelConfigs
                    .FirstOrDefaultAsync(c =>
                        c.OrganizationId == organizationId &&
                        c.ProjectId == null &&
                        c.ModelType == modelType &&
                        c.Default == true &&
                        !c.IsArchived);

                // Repopulate cache on miss for subsequent reads
                if (modelConfig != null)
                {
                    try
                    {
                        await CacheService.Instance.SetAsync(organizationCacheKey, modelConfig.Id, AiModelConfigCacheTtl);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Cache population failed for default-ai-model-config key {CacheKey}", organizationCacheKey);
                    }
                }
            }
        }

        if (modelConfig is null)
            throw new KeyNotFoundException(
                $"No default {modelType} model configuration found for organization {organizationId}.");

        string? token = null;
        if (modelConfig.RequiresToken == true)
        {
            var rawToken = await _context.UserModelTokens
                .Where(t => t.UserId == currentUserId && t.AiModelConfigId == modelConfig.Id)
                .Select(t => t.Token)
                .FirstOrDefaultAsync();

            if (rawToken is null)
                throw new InvalidOperationException(
                    $"The {modelType} model configuration (ID: {modelConfig.Id}) requires an API token, " +
                    $"but none was found for user {currentUserId}. Please add a token for this model.");

            token = _encryptionHelper.Decrypt(rawToken);
        }

        return new AiModelConfigResponseDto.WithToken
        {
            Id = modelConfig.Id,
            OrganizationId = modelConfig.OrganizationId,
            ProjectId = modelConfig.ProjectId,
            ServerUrl = modelConfig.ServerUrl,
            ModelProvider = modelConfig.ModelProvider,
            ModelName = modelConfig.ModelName,
            ModelType = modelConfig.ModelType,
            RequiresToken = modelConfig.RequiresToken,
            Default = modelConfig.Default,
            IsArchived = modelConfig.IsArchived,
            LastUpdatedAt = modelConfig.LastUpdatedAt,
            LastUpdatedBy = modelConfig.LastUpdatedBy,
            Token = token
        };
    }

    /// <summary>
    ///     Creates a single AI Model Configuration
    /// </summary>
    /// <param name="currentUserId">The ID of user making the request </param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the new Model Configuration will belong</param>
    /// <param name="dto">The DTO containing the new AI Model configuration to be created</param>
    /// <returns> The newly created AI Model Configuration</returns>
    public async Task<AiModelConfigResponseDto> CreateAiModelConfig(
        long currentUserId,
        long organizationId,
        long? projectId,
        CreateAiModelConfigDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        if (!ModelProviderList.Contains(dto.ModelProvider.ToLower()))
            throw new ArgumentException("Unknown ModelProvider");

        if (!ModelTypeList.Contains(dto.ModelType.ToLower()))
            throw new ArgumentException("Unknown ModelType");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var newConfig = new AiModelConfig
            {
                OrganizationId = organizationId,
                ProjectId = projectId,
                ServerUrl = dto.ServerUrl,
                ModelProvider = dto.ModelProvider.ToLower(),
                ModelName = dto.ModelName,
                ModelType = dto.ModelType.ToLower(),
                RequiresToken = dto.RequiresToken,
                Default = dto.Default,
                IsArchived = false,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = currentUserId
            };

            _context.AiModelConfigs.Add(newConfig);
            await _context.SaveChangesAsync();

            if (dto.Default)
            {
                if (projectId.HasValue)
                    await ResetProjectDefaults(projectId.Value, newConfig.Id, newConfig.ModelType);
                else
                    await ResetOrganizationDefaults(organizationId, newConfig.Id, newConfig.ModelType);
            }

            await transaction.CommitAsync();

            // Update cached default AI model config
            if (dto.Default)
            {
                await UpdateDefaultAiModelConfigCache(newConfig.Id, organizationId, projectId, newConfig.ModelType);
            }

            return new AiModelConfigResponseDto
            {
                Id = newConfig.Id,
                OrganizationId = newConfig.OrganizationId,
                ProjectId = newConfig.ProjectId,
                ServerUrl = newConfig.ServerUrl,
                ModelProvider = newConfig.ModelProvider,
                ModelName = newConfig.ModelName,
                ModelType = newConfig.ModelType,
                RequiresToken = newConfig.RequiresToken,
                Default = newConfig.Default,
                IsArchived = newConfig.IsArchived,
                LastUpdatedAt = newConfig.LastUpdatedAt,
                LastUpdatedBy = newConfig.LastUpdatedBy
            };
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not KeyNotFoundException)
        {
            await transaction.RollbackAsync();
            throw new Exception("Failed to create Ai Model Configuration");
        }
    }

    /// <summary>
    ///     Updates a single AI Model Configuration
    /// </summary>
    /// <param name="currentUserId">The ID of user making the request </param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose AI Model Configurations will be retrieved</param>
    /// <param name="aiModelConfigId">The ID of the AI Model Configuration that will be retrieved</param>
    /// <param name="dto">The DTO containing the new AI Model configuration used to update</param>
    /// <returns> The updated AI Model Config.</returns>
    public async Task<AiModelConfigResponseDto> UpdateAiModelConfig(long currentUserId, long organizationId,
        long? projectId, long aiModelConfigId, UpdateAiModelConfigDto dto)
    {
        ValidationHelper.ValidateModel(dto);

        var query = _context.AiModelConfigs
            .Where(x => x.OrganizationId == organizationId && x.Id == aiModelConfigId)
            .Where(x => x.IsArchived == false)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(x => x.ProjectId == projectId || x.ProjectId == null);

        var returnedModelConfig = await query.FirstOrDefaultAsync();
        if (returnedModelConfig == null)
            throw new KeyNotFoundException($"Ai Model Config with id {aiModelConfigId} not found");

        // If updating an org-level template for a project, create a new project-level copy instead
        if (projectId.HasValue && returnedModelConfig.ProjectId == null)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var newProjectConfig = new AiModelConfig
                {
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    ModelName = dto.ModelName ?? returnedModelConfig.ModelName,
                    ModelType = dto.ModelType ?? returnedModelConfig.ModelType,
                    ServerUrl = dto.ServerUrl ?? returnedModelConfig.ServerUrl,
                    RequiresToken = dto.RequiresToken ?? returnedModelConfig.RequiresToken,
                    ModelProvider = returnedModelConfig.ModelProvider,
                    IsArchived = false,
                    Default = dto.Default ?? false,
                    LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    LastUpdatedBy = currentUserId
                };

                _context.AiModelConfigs.Add(newProjectConfig);
                await _context.SaveChangesAsync();

                if (newProjectConfig.Default)
                    await ResetProjectDefaults(projectId.Value, newProjectConfig.Id, newProjectConfig.ModelType);

                await transaction.CommitAsync();

                // Update cached default AI model config
                if (newProjectConfig.Default)
                {
                    await UpdateDefaultAiModelConfigCache(newProjectConfig.Id, organizationId, projectId, newProjectConfig.ModelType);
                }

                return new AiModelConfigResponseDto
                {
                    Id = newProjectConfig.Id,
                    OrganizationId = newProjectConfig.OrganizationId,
                    ProjectId = newProjectConfig.ProjectId,
                    ServerUrl = newProjectConfig.ServerUrl,
                    ModelProvider = newProjectConfig.ModelProvider,
                    ModelName = newProjectConfig.ModelName,
                    ModelType = newProjectConfig.ModelType,
                    RequiresToken = newProjectConfig.RequiresToken,
                    Default = newProjectConfig.Default,
                    LastUpdatedAt = newProjectConfig.LastUpdatedAt,
                    LastUpdatedBy = newProjectConfig.LastUpdatedBy,
                    IsArchived = newProjectConfig.IsArchived
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to create project-level AI Model Configuration");
            }
        }
        else
        {

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Capture before mutation for caching 
                bool wasDefault = returnedModelConfig.Default;
                string previousModelType = returnedModelConfig.ModelType;

                if (dto.Default != null)
                {
                    if (returnedModelConfig.Default && !dto.Default.Value)
                    {
                        throw new InvalidOperationException(
                            "Must assign another AI Model Configuration to be the new default before unassigning.");
                    }

                    if (!returnedModelConfig.Default && dto.Default.Value)
                    {
                        var modelType = dto.ModelType ?? returnedModelConfig.ModelType;

                        if (projectId.HasValue)
                            await ResetProjectDefaults(projectId.Value, returnedModelConfig.Id, modelType);
                        else
                            await ResetOrganizationDefaults(organizationId, returnedModelConfig.Id, modelType);
                    }
                }

                returnedModelConfig.ModelName = dto.ModelName ?? returnedModelConfig.ModelName;
                returnedModelConfig.ModelType = dto.ModelType ?? returnedModelConfig.ModelType;
                returnedModelConfig.ServerUrl = dto.ServerUrl ?? returnedModelConfig.ServerUrl;
                returnedModelConfig.RequiresToken = dto.RequiresToken ?? returnedModelConfig.RequiresToken;
                returnedModelConfig.Default = dto.Default ?? returnedModelConfig.Default;

                returnedModelConfig.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                returnedModelConfig.LastUpdatedBy = currentUserId;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                if (returnedModelConfig.Default)
                {
                    if (wasDefault && previousModelType != returnedModelConfig.ModelType)
                    {
                        // This config was the default and the model type changed, invalidate the old key
                        await UpdateDefaultAiModelConfigCache(returnedModelConfig.Id, organizationId, projectId, previousModelType, invalidateKey: true);
                    }

                    // Update cached default AI model config
                    await UpdateDefaultAiModelConfigCache(returnedModelConfig.Id, organizationId, projectId, returnedModelConfig.ModelType);
                }

                return new AiModelConfigResponseDto
                {
                    Id = returnedModelConfig.Id,
                    OrganizationId = returnedModelConfig.OrganizationId,
                    ProjectId = returnedModelConfig.ProjectId,
                    ServerUrl = returnedModelConfig.ServerUrl,
                    ModelProvider = returnedModelConfig.ModelProvider,
                    ModelName = returnedModelConfig.ModelName,
                    ModelType = returnedModelConfig.ModelType,
                    RequiresToken = returnedModelConfig.RequiresToken,
                    Default = returnedModelConfig.Default,
                    LastUpdatedAt = returnedModelConfig.LastUpdatedAt,
                    LastUpdatedBy = returnedModelConfig.LastUpdatedBy,
                    IsArchived = returnedModelConfig.IsArchived
                };
            }
            catch (Exception ex) when (ex is not InvalidOperationException and not KeyNotFoundException)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to update Ai Model Configuration");
            }
        }
    }

    /// <summary>
    ///     Delete a single AI Model Configuration
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose AI Model Configurations will be retrieved</param>
    /// <param name="aiModelConfigId">The ID of the AI Model Configuration that will be retrieved</param>
    /// <returns> True if the operation was successful</returns>
    public async Task<bool> DeleteAiModelConfig(long organizationId, long? projectId, long aiModelConfigId)
    {
        var query = _context.AiModelConfigs
            .Where(x => x.OrganizationId == organizationId && x.Id == aiModelConfigId)
            .Where(x => x.IsArchived == false)
            .AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == projectId);
        }
        else
        {
            query = query.Where(x => x.ProjectId == null);
        }

        var returnedModelConfig = await query.FirstOrDefaultAsync();

        if (returnedModelConfig is null)
        {
            throw new KeyNotFoundException($"AI Model Configuration with ID: {aiModelConfigId} not found");
        }

        if (returnedModelConfig.Default)
        {
            throw new InvalidOperationException("Default AI Model Configurations cannot be deleted. Assign a new default Model Configuration before deleting");
        }

        _context.AiModelConfigs.Remove(returnedModelConfig);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    ///     Archive a single AI Model Configuration
    /// </summary>
    /// <param name="currentUserId">The ID of user making the request </param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose AI Model Configurations will be retrieved</param>
    /// <param name="aiModelConfigId">The ID of the AI Model Configuration that will be retrieved</param>
    /// <returns> True if the operation was successful</returns>
    public async Task<bool> ArchiveAiModelConfig(long currentUserId, long organizationId, long? projectId, long aiModelConfigId)
    {
        var query = _context.AiModelConfigs
            .Where(x => x.Id == aiModelConfigId && x.OrganizationId == organizationId)
            .AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == projectId);
        }
        else
        {
            query = query.Where(x => x.ProjectId == null);
        }

        var returnedModelConfig = await query.FirstOrDefaultAsync();

        if (returnedModelConfig is null)
        {
            throw new KeyNotFoundException($"AI Model Configuration with ID: {aiModelConfigId} is not found");
        }

        if (returnedModelConfig.IsArchived)
            throw new InvalidOperationException($"AI Model Configuration with id {aiModelConfigId} is already archived");

        if (returnedModelConfig.Default)
            throw new InvalidOperationException("Default AI Model Configuration cannot be archived." +
                                                " Please assign new default before archiving.");

        returnedModelConfig.IsArchived = true;
        returnedModelConfig.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        returnedModelConfig.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();
        return true;
    }


    /// <summary>
    ///     Unarchive a single AI Model Configuration
    /// </summary>
    /// <param name="currentUserId">The ID of user making the request</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose AI Model Configurations will be retrieved</param>
    /// <param name="aiModelConfigId">The ID of the AI Model Configuration that will be unarchived</param>
    /// <returns>True if the operation was successful</returns>
    public async Task<bool> UnarchiveAiModelConfig(long currentUserId, long organizationId, long? projectId, long aiModelConfigId)
    {
        var query = _context.AiModelConfigs
            .Where(x => x.Id == aiModelConfigId && x.OrganizationId == organizationId)
            .AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == projectId);
        }
        else
        {
            query = query.Where(x => x.ProjectId == null);
        }

        var returnedModelConfig = await query.FirstOrDefaultAsync();

        if (returnedModelConfig is null)
        {
            throw new KeyNotFoundException($"AI Model Configuration with ID: {aiModelConfigId} is not found");
        }

        if (!returnedModelConfig.IsArchived)
            throw new InvalidOperationException($"AI Model Configuration with id {aiModelConfigId} is not archived");

        returnedModelConfig.IsArchived = false;
        returnedModelConfig.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        returnedModelConfig.LastUpdatedBy = currentUserId;
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task ResetProjectDefaults(long projectId, long newDefaultId, string modelType)
    {
        await _context.AiModelConfigs
            .Where(os => os.ProjectId == projectId && os.Id != newDefaultId && os.ModelType == modelType)
            .ExecuteUpdateAsync(s => s.SetProperty(os => os.Default, false));
    }

    private async Task ResetOrganizationDefaults(long organizationId, long newDefaultId, string modelType)
    {
        await _context.AiModelConfigs
            .Where(os => os.OrganizationId == organizationId && os.ProjectId == null && os.Id != newDefaultId && os.ModelType == modelType)
            .ExecuteUpdateAsync(s => s.SetProperty(os => os.Default, false));
    }

    private async Task UpdateDefaultAiModelConfigCache(long aiModelConfigId, long organizationId, long? projectId, string modelType, bool invalidateKey = false)
    {
        var key = projectId.HasValue
            ? CacheKeys.ProjectDefaultAiModelConfig(projectId.Value, modelType)
            : CacheKeys.OrganizationDefaultAiModelConfig(organizationId, modelType);

        if (invalidateKey)
        {
            try
            {
                await CacheService.Instance.DeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Default AI model config cache invalidation failed for key {CacheKey}", key);
            }
        }
        else
        {
            try
            {
                await CacheService.Instance.SetAsync(key, aiModelConfigId, AiModelConfigCacheTtl);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Default AI model config cache update failed for key {CacheKey}", key);
            }
        }
    }
}