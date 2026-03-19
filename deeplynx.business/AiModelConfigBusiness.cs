using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class AiModelConfigBusiness : IAiModelConfigBusiness
{
    private readonly DeeplynxContext _context;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiModelConfigBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for operations.</param>
    public AiModelConfigBusiness(DeeplynxContext context)
    {
        _context = context;
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
        "language",
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
                ModelProvider = dto.ModelProvider,
                ModelName = dto.ModelName,
                ModelType = dto.ModelType,
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
            query = query.Where(x => x.ProjectId == projectId);
        else
            query = query.Where(x => x.ProjectId == null);

        var returnedModelConfig = await query.FirstOrDefaultAsync();
        if (returnedModelConfig == null)
            throw new KeyNotFoundException($"Ai Model Config with id {aiModelConfigId} not found");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (dto.Default != null)
            {
                if (returnedModelConfig.Default && !dto.Default.Value)
                {
                    throw new InvalidOperationException(
                        "Must assign another AI Model Configuration to be the new default before unassigning.");
                }

                if (!returnedModelConfig.Default && dto.Default.Value)
                {
                    // Use the incoming ModelType if it's being updated, otherwise use the existing one.
                    // This ensures we reset defaults only among configs of the same model type.
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
        // Reset defaults only among configs of the same model type at the project level
        await _context.AiModelConfigs
            .Where(os => os.ProjectId == projectId && os.Id != newDefaultId && os.ModelType == modelType)
            .ExecuteUpdateAsync(s => s.SetProperty(os => os.Default, false));
    }

    private async Task ResetOrganizationDefaults(long organizationId, long newDefaultId, string modelType)
    {
        // Reset defaults only among configs of the same model type at the org level
        await _context.AiModelConfigs
            .Where(os => os.OrganizationId == organizationId && os.ProjectId == null && os.Id != newDefaultId && os.ModelType == modelType)
            .ExecuteUpdateAsync(s => s.SetProperty(os => os.Default, false));
    }
}