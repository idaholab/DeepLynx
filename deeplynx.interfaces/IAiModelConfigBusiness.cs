using System.Net.Sockets;
using deeplynx.models;

namespace deeplynx.interfaces;

public interface IAiModelConfigBusiness
{
    Task<List<AiModelConfigResponseDto>> GetAllAiModelConfigs(long organizationId, long? projectId, bool hideArchived);
    Task<AiModelConfigResponseDto> GetAiModelConfig(long organizationId, long? projectId, long aiModelConfigId, bool hideArchived);
    Task<AiModelConfigWithTokenResponseDto> GetAiModelConfigWithToken(long currentUserId, long organizationId, long? projectId, long aiModelConfigId);

    Task<AiModelConfigResponseDto> GetDefaultAiModelConfig(long organizationId, long? projectId, string modelType);
    Task<AiModelConfigWithTokenResponseDto> GetDefaultAiModelConfigWithToken(long currentUserId, long organizationId, long? projectId, string modelType);
    Task<AiModelConfigResponseDto> CreateAiModelConfig(long currentUserId, long organizationId, long? projectId, CreateAiModelConfigDto dto);
    Task<AiModelConfigResponseDto> UpdateAiModelConfig(long currentUserId, long organizationId, long? projectId, long aiModelConfigId,
        UpdateAiModelConfigDto dto);
    Task<bool> DeleteAiModelConfig(long organizationId, long? projectId, long aiModelConfigId);
    Task<bool> ArchiveAiModelConfig(long currentUserId, long organizationId, long? projectId, long aiModelConfigId);
    Task<bool> UnarchiveAiModelConfig(long currentUserId, long organizationId, long? projectId, long aiModelConfigId);
}