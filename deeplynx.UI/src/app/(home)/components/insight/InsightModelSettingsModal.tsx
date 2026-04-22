"use client";

import { useEffect, useMemo, useState } from "react";
import {
  CheckCircleIcon,
  EyeIcon,
  EyeSlashIcon,
  ExclamationCircleIcon,
  PencilSquareIcon,
  PlusIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import toast from "react-hot-toast";
import { useLanguage } from "@/app/contexts/Language";
import { useRBAC } from "@/app/(home)/rbac/useRBAC";
import type {
  AiModelProvider,
  AiModelType,
  CreateAiModelConfigRequestDto,
  UpdateAiModelConfigRequestDto,
} from "@/app/(home)/types/requestDTOs";
import type {
  AiModelConfigResponseDto,
  UserModelTokenResponseDto,
} from "@/app/(home)/types/responseDTOs";
import {
  archiveProjectAiModelConfig,
  createProjectAiModelConfig,
  getProjectAiModelConfigs,
  updateProjectAiModelConfig,
} from "@/app/lib/client_service/ai_model_config_services.client";
import {
  createUserModelToken,
  getUserModelTokens,
  updateUserModelToken,
} from "@/app/lib/client_service/user_model_token_services.client";
import type { InsightModelSelection } from "./useInsightModelSelection";

type InsightSelectionSection = "query" | "upload" | "embedding";
type ModelEditorMode = "create" | "edit";

interface InsightModelSettingsModalProps {
  isOpen: boolean;
  organizationId?: number;
  projectId?: number;
  selectedInsightModels: InsightModelSelection;
  onClose: () => void;
  onSaveSelection: (nextSelection: InsightModelSelection) => void;
}

interface InsightModelConfigFormState {
  modelName: string;
  modelType: AiModelType;
  modelProvider: AiModelProvider;
  serverUrl: string;
  requiresToken: boolean;
  isDefaultConfig: boolean;
  userToken: string;
}

interface InsightModelEditorState {
  editorMode: ModelEditorMode;
  editingModelConfigId: number | null;
  formState: InsightModelConfigFormState;
}

interface InsightModelSelectionCardProps {
  title: string;
  description: string;
  emptyOptionLabel: string;
  availableModelConfigs: AiModelConfigResponseDto[];
  selectedModelConfigId: number | null;
  onSelectedModelChange: (nextModelConfigId: number | null) => void;
  onOpenCreateEditor: () => void;
  onOpenEditEditor: () => void;
}

const MODEL_PROVIDER_OPTIONS: Array<{
  label: string;
  value: AiModelProvider;
}> = [
  { label: "OpenAI", value: "openai" },
  { label: "Anthropic", value: "anthropic" },
  { label: "HPC", value: "hpc" },
  { label: "Ollama", value: "ollama" },
];

const MODEL_TYPE_OPTIONS: Array<{
  label: string;
  value: AiModelType;
}> = [
  { label: "LLM", value: "llm" },
  { label: "VLM", value: "vlm" },
  { label: "Embedding", value: "embedding" },
];

const DEFAULT_MODEL_CONFIG_FORM_STATE: InsightModelConfigFormState = {
  modelName: "",
  modelType: "llm",
  modelProvider: "openai",
  serverUrl: "",
  requiresToken: false,
  isDefaultConfig: false,
  userToken: "",
};

function createEmptyModelConfigFormState(
  suggestedModelType: AiModelType,
): InsightModelConfigFormState {
  return {
    ...DEFAULT_MODEL_CONFIG_FORM_STATE,
    modelType: suggestedModelType,
  };
}

function getSuggestedModelType(
  insightSelectionSection: InsightSelectionSection,
): AiModelType {
  if (insightSelectionSection === "upload") {
    return "vlm";
  }

  if (insightSelectionSection === "embedding") {
    return "embedding";
  }

  return "llm";
}

function getAllowedModelTypes(
  insightSelectionSection: InsightSelectionSection,
): AiModelType[] {
  if (insightSelectionSection === "query") {
    return ["llm", "vlm"];
  }

  if (insightSelectionSection === "upload") {
    return ["vlm"];
  }

  return ["embedding"];
}

function buildModelConfigOptionLabel(
  aiModelConfig: AiModelConfigResponseDto,
): string {
  const scopeLabel = aiModelConfig.projectId ? "Project" : "Org";
  const defaultLabel = aiModelConfig.default ? " • Default" : "";

  return `${aiModelConfig.modelName} (${aiModelConfig.modelType.toUpperCase()} • ${aiModelConfig.modelProvider}) • ${scopeLabel}${defaultLabel}`;
}

function syncSelectedModelNames(
  currentSelection: InsightModelSelection,
  availableModelConfigs: AiModelConfigResponseDto[],
): InsightModelSelection {
  const queryModelConfig = availableModelConfigs.find(
    (aiModelConfig) => aiModelConfig.id === currentSelection.queryModelConfigId,
  );
  const uploadModelConfig = availableModelConfigs.find(
    (aiModelConfig) =>
      aiModelConfig.id === currentSelection.uploadModelConfigId,
  );
  const embeddingModelConfig = availableModelConfigs.find(
    (aiModelConfig) =>
      aiModelConfig.id === currentSelection.embeddingModelConfigId,
  );

  return {
    queryModelConfigId: queryModelConfig?.id ?? null,
    queryModelName: queryModelConfig?.modelName ?? null,
    uploadModelConfigId: uploadModelConfig?.id ?? null,
    uploadModelName: uploadModelConfig?.modelName ?? null,
    embeddingModelConfigId: embeddingModelConfig?.id ?? null,
    embeddingModelName: embeddingModelConfig?.modelName ?? null,
  };
}

function buildUpdatedSelectionForSection(
  currentSelection: InsightModelSelection,
  insightSelectionSection: InsightSelectionSection,
  selectedModelConfig: AiModelConfigResponseDto | null,
): InsightModelSelection {
  if (insightSelectionSection === "query") {
    return {
      ...currentSelection,
      queryModelConfigId: selectedModelConfig?.id ?? null,
      queryModelName: selectedModelConfig?.modelName ?? null,
    };
  }

  if (insightSelectionSection === "upload") {
    return {
      ...currentSelection,
      uploadModelConfigId: selectedModelConfig?.id ?? null,
      uploadModelName: selectedModelConfig?.modelName ?? null,
    };
  }

  return {
    ...currentSelection,
    embeddingModelConfigId: selectedModelConfig?.id ?? null,
    embeddingModelName: selectedModelConfig?.modelName ?? null,
  };
}

function buildModelEditorStateFromSelectedConfig(
  selectedModelConfig: AiModelConfigResponseDto,
  draftTokenValuesByConfigId: Record<number, string>,
  savedUserTokensByConfigId: Record<number, UserModelTokenResponseDto>,
): InsightModelEditorState {
  return {
    editorMode: "edit",
    editingModelConfigId: selectedModelConfig.projectId
      ? selectedModelConfig.id
      : null,
    formState: {
      modelName: selectedModelConfig.modelName,
      modelType: selectedModelConfig.modelType as AiModelType,
      modelProvider: selectedModelConfig.modelProvider as AiModelProvider,
      serverUrl: selectedModelConfig.serverUrl,
      requiresToken: selectedModelConfig.requiresToken,
      isDefaultConfig: selectedModelConfig.default,
      userToken:
        draftTokenValuesByConfigId[selectedModelConfig.id] ??
        savedUserTokensByConfigId[selectedModelConfig.id]?.token ??
        "",
    },
  };
}

function InsightModelSelectionCard({
  title,
  description,
  emptyOptionLabel,
  availableModelConfigs,
  selectedModelConfigId,
  onSelectedModelChange,
  onOpenCreateEditor,
  onOpenEditEditor,
}: InsightModelSelectionCardProps) {
  const { t } = useLanguage();
  const selectedModelConfig =
    availableModelConfigs.find(
      (aiModelConfig) => aiModelConfig.id === selectedModelConfigId,
    ) ?? null;

  return (
    <div className="rounded-box border border-base-300 bg-base-100 p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h4 className="text-base font-semibold text-base-content">{title}</h4>
          <p className="mt-1 text-sm text-base-content/70">{description}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            className="btn btn-ghost btn-sm gap-2"
            onClick={onOpenCreateEditor}
          >
            <PlusIcon className="size-4" />
            {t.translations.INSIGHT_NEW_MODEL}
          </button>
          <button
            type="button"
            className="btn btn-ghost btn-sm gap-2"
            onClick={onOpenEditEditor}
            disabled={!selectedModelConfig}
          >
            <PencilSquareIcon className="size-4" />
            {t.translations.INSIGHT_EDIT_SELECTED_CONFIG}
          </button>
        </div>
      </div>

      <div className="mt-4 form-control">
        <select
          className="select select-bordered w-full"
          value={selectedModelConfigId ?? ""}
          onChange={(event) => {
            const nextValue = event.target.value;
            onSelectedModelChange(nextValue ? Number(nextValue) : null);
          }}
        >
          <option value="">{emptyOptionLabel}</option>
          {availableModelConfigs.map((aiModelConfig) => (
            <option key={aiModelConfig.id} value={aiModelConfig.id}>
              {buildModelConfigOptionLabel(aiModelConfig)}
            </option>
          ))}
        </select>
      </div>
    </div>
  );
}

export default function InsightModelSettingsModal({
  isOpen,
  organizationId,
  projectId,
  selectedInsightModels,
  onClose,
  onSaveSelection,
}: InsightModelSettingsModalProps) {
  const { t } = useLanguage();
  const { user } = useRBAC();
  const currentUserId = user?.id;
  const [availableModelConfigs, setAvailableModelConfigs] = useState<
    AiModelConfigResponseDto[]
  >([]);
  const [savedUserTokensByConfigId, setSavedUserTokensByConfigId] = useState<
    Record<number, UserModelTokenResponseDto>
  >({});
  const [draftTokenValuesByConfigId, setDraftTokenValuesByConfigId] = useState<
    Record<number, string>
  >({});
  const [draftInsightModelSelection, setDraftInsightModelSelection] =
    useState<InsightModelSelection>(selectedInsightModels);
  const [modelEditorMode, setModelEditorMode] =
    useState<ModelEditorMode>("create");
  const [editorSelectionSection, setEditorSelectionSection] =
    useState<InsightSelectionSection>("query");
  const [editingModelConfigId, setEditingModelConfigId] = useState<
    number | null
  >(null);
  const [isModelEditorVisible, setIsModelEditorVisible] = useState(false);
  const [modelConfigFormState, setModelConfigFormState] =
    useState<InsightModelConfigFormState>(
      createEmptyModelConfigFormState("llm"),
    );
  const [isLoadingModelSettings, setIsLoadingModelSettings] = useState(false);
  const [isSavingModelConfig, setIsSavingModelConfig] = useState(false);
  const [isArchivingModelConfig, setIsArchivingModelConfig] = useState(false);
  const [modelConfigSaveError, setModelConfigSaveError] = useState("");
  const [isEditorTokenVisible, setIsEditorTokenVisible] = useState(false);

  const queryModelConfigs = useMemo(
    () =>
      availableModelConfigs.filter((aiModelConfig) =>
        getAllowedModelTypes("query").includes(
          aiModelConfig.modelType as AiModelType,
        ),
      ),
    [availableModelConfigs],
  );
  const uploadModelConfigs = useMemo(
    () =>
      availableModelConfigs.filter((aiModelConfig) =>
        getAllowedModelTypes("upload").includes(
          aiModelConfig.modelType as AiModelType,
        ),
      ),
    [availableModelConfigs],
  );
  const embeddingModelConfigs = useMemo(
    () =>
      availableModelConfigs.filter((aiModelConfig) =>
        getAllowedModelTypes("embedding").includes(
          aiModelConfig.modelType as AiModelType,
        ),
      ),
    [availableModelConfigs],
  );
  const selectedModelConfigsMissingTokens = useMemo(() => {
    const selectedModelConfigIds = [
      draftInsightModelSelection.queryModelConfigId,
      draftInsightModelSelection.uploadModelConfigId,
      draftInsightModelSelection.embeddingModelConfigId,
    ].filter(
      (modelConfigId): modelConfigId is number =>
        typeof modelConfigId === "number",
    );
    const uniqueSelectedModelConfigIds = [...new Set(selectedModelConfigIds)];

    return uniqueSelectedModelConfigIds
      .map((modelConfigId) =>
        availableModelConfigs.find(
          (aiModelConfig) => aiModelConfig.id === modelConfigId,
        ),
      )
      .filter((aiModelConfig): aiModelConfig is AiModelConfigResponseDto =>
        Boolean(aiModelConfig),
      )
      .filter(
        (aiModelConfig) =>
          aiModelConfig.requiresToken &&
          !savedUserTokensByConfigId[aiModelConfig.id],
      );
  }, [
    availableModelConfigs,
    draftInsightModelSelection.embeddingModelConfigId,
    draftInsightModelSelection.queryModelConfigId,
    draftInsightModelSelection.uploadModelConfigId,
    savedUserTokensByConfigId,
  ]);
  const modelSelectionSections = [
    {
      sectionKey: "query" as const,
      title: t.translations.INSIGHT_QUERY_MODEL,
      description: t.translations.INSIGHT_QUERY_MODEL_DESCRIPTION,
      availableModelConfigs: queryModelConfigs,
      selectedModelConfigId: draftInsightModelSelection.queryModelConfigId,
    },
    {
      sectionKey: "upload" as const,
      title: t.translations.INSIGHT_UPLOAD_MODEL,
      description: t.translations.INSIGHT_UPLOAD_MODEL_DESCRIPTION,
      availableModelConfigs: uploadModelConfigs,
      selectedModelConfigId: draftInsightModelSelection.uploadModelConfigId,
    },
    {
      sectionKey: "embedding" as const,
      title: t.translations.INSIGHT_EMBEDDING_MODEL,
      description: t.translations.INSIGHT_EMBEDDING_MODEL_DESCRIPTION,
      availableModelConfigs: embeddingModelConfigs,
      selectedModelConfigId:
        draftInsightModelSelection.embeddingModelConfigId,
    },
  ];

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    setDraftInsightModelSelection(selectedInsightModels);
    setModelEditorMode("create");
    setEditorSelectionSection("query");
    setEditingModelConfigId(null);
    setIsModelEditorVisible(false);
    setModelConfigFormState(createEmptyModelConfigFormState("llm"));
    setModelConfigSaveError("");
    setIsEditorTokenVisible(false);
  }, [isOpen, selectedInsightModels]);

  useEffect(() => {
    if (!isOpen || !organizationId || !projectId || !currentUserId) {
      return;
    }

    const resolvedOrganizationId = organizationId;
    const resolvedProjectId = projectId;
    const resolvedCurrentUserId = currentUserId;
    let hasCancelled = false;

    async function loadModelSettings() {
      setIsLoadingModelSettings(true);

      try {
        const [loadedModelConfigs, loadedUserTokens] = await Promise.all([
          getProjectAiModelConfigs(resolvedOrganizationId, resolvedProjectId),
          getUserModelTokens(resolvedCurrentUserId),
        ]);

        if (hasCancelled) {
          return;
        }

        setAvailableModelConfigs(loadedModelConfigs);
        setDraftInsightModelSelection((currentSelection) =>
          syncSelectedModelNames(currentSelection, loadedModelConfigs),
        );
        setSavedUserTokensByConfigId(
          Object.fromEntries(
            loadedUserTokens.map((savedUserToken) => [
              savedUserToken.aiModelConfigId,
              savedUserToken,
            ]),
          ),
        );
        setDraftTokenValuesByConfigId(
          Object.fromEntries(
            loadedUserTokens.map((savedUserToken) => [
              savedUserToken.aiModelConfigId,
              savedUserToken.token,
            ]),
          ),
        );
      } catch (error) {
        console.error("Failed to load Insight model settings:", error);
        if (!hasCancelled) {
          toast.error(t.translations.INSIGHT_MODEL_CONFIGS_FAILED);
        }
      } finally {
        if (!hasCancelled) {
          setIsLoadingModelSettings(false);
        }
      }
    }

    void loadModelSettings();

    return () => {
      hasCancelled = true;
    };
  }, [
    currentUserId,
    isOpen,
    organizationId,
    projectId,
    t.translations.INSIGHT_MODEL_CONFIGS_FAILED,
  ]);

  function updateDraftSelection(
    insightSelectionSection: InsightSelectionSection,
    nextModelConfigId: number | null,
  ) {
    const nextModelConfig =
      availableModelConfigs.find(
        (aiModelConfig) => aiModelConfig.id === nextModelConfigId,
      ) ?? null;

    setDraftInsightModelSelection((currentSelection) =>
      buildUpdatedSelectionForSection(
        currentSelection,
        insightSelectionSection,
        nextModelConfig,
      ),
    );
  }

  function closeModelEditor() {
    setIsModelEditorVisible(false);
    setModelConfigSaveError("");
    setIsEditorTokenVisible(false);
  }

  function openCreateModelEditor(
    insightSelectionSection: InsightSelectionSection,
  ) {
    const suggestedModelType = getSuggestedModelType(insightSelectionSection);

    setModelEditorMode("create");
    setEditorSelectionSection(insightSelectionSection);
    setEditingModelConfigId(null);
    setIsModelEditorVisible(true);
    setModelConfigFormState(
      createEmptyModelConfigFormState(suggestedModelType),
    );
    setModelConfigSaveError("");
    setIsEditorTokenVisible(false);
  }

  function openEditModelEditor(
    insightSelectionSection: InsightSelectionSection,
    selectedModelConfig: AiModelConfigResponseDto,
  ) {
    const nextEditorState = buildModelEditorStateFromSelectedConfig(
      selectedModelConfig,
      draftTokenValuesByConfigId,
      savedUserTokensByConfigId,
    );

    setModelEditorMode(nextEditorState.editorMode);
    setEditorSelectionSection(insightSelectionSection);
    setEditingModelConfigId(nextEditorState.editingModelConfigId);
    setIsModelEditorVisible(true);
    setModelConfigFormState(nextEditorState.formState);
    setModelConfigSaveError("");
    setIsEditorTokenVisible(false);
  }

  async function refreshAvailableModelConfigs() {
    if (!organizationId || !projectId) {
      return [] as AiModelConfigResponseDto[];
    }

    const refreshedModelConfigs = await getProjectAiModelConfigs(
      organizationId,
      projectId,
    );
    setAvailableModelConfigs(refreshedModelConfigs);
    setDraftInsightModelSelection((currentSelection) =>
      syncSelectedModelNames(currentSelection, refreshedModelConfigs),
    );
    return refreshedModelConfigs;
  }

  async function upsertUserTokenForModelConfig(
    aiModelConfigId: number,
    tokenValue: string,
  ) {
    if (!currentUserId) {
      throw new Error(t.translations.INSIGHT_CURRENT_USER_REQUIRED);
    }

    const existingUserToken = savedUserTokensByConfigId[aiModelConfigId];
    if (existingUserToken) {
      return updateUserModelToken(currentUserId, existingUserToken.id, {
        token: tokenValue,
      });
    }

    return createUserModelToken(currentUserId, {
      aiModelConfigId,
      token: tokenValue,
    });
  }

  async function handleSaveModelConfig() {
    if (!organizationId || !projectId) {
      setModelConfigSaveError(t.translations.INSIGHT_PROJECT_CONTEXT_REQUIRED);
      return;
    }

    if (
      !modelConfigFormState.modelName.trim() ||
      !modelConfigFormState.serverUrl.trim()
    ) {
      setModelConfigSaveError(
        t.translations.INSIGHT_MODEL_CONFIG_REQUIRED_FIELDS,
      );
      return;
    }

    const createModelConfigRequest: CreateAiModelConfigRequestDto = {
      model_name: modelConfigFormState.modelName.trim(),
      model_provider: modelConfigFormState.modelProvider,
      model_type: modelConfigFormState.modelType,
      server_url: modelConfigFormState.serverUrl.trim(),
      requires_token: modelConfigFormState.requiresToken,
      default: modelConfigFormState.isDefaultConfig,
    };
    const updateModelConfigRequest: UpdateAiModelConfigRequestDto = {
      model_name: modelConfigFormState.modelName.trim(),
      model_type: modelConfigFormState.modelType,
      server_url: modelConfigFormState.serverUrl.trim(),
      requires_token: modelConfigFormState.requiresToken,
      default: modelConfigFormState.isDefaultConfig,
    };

    setIsSavingModelConfig(true);
    setModelConfigSaveError("");

    try {
      // Organization-scoped selections still open in "edit" mode for the user,
      // but they save as a new project-scoped config because there is no project id to update.
      const savedModelConfig =
        modelEditorMode === "edit" && editingModelConfigId
          ? await updateProjectAiModelConfig(
              organizationId,
              projectId,
              editingModelConfigId,
              updateModelConfigRequest,
            )
          : await createProjectAiModelConfig(
              organizationId,
              projectId,
              createModelConfigRequest,
            );

      const refreshedModelConfigs = await refreshAvailableModelConfigs();

      if (
        savedModelConfig.requiresToken &&
        modelConfigFormState.userToken.trim().length > 0
      ) {
        const savedUserToken = await upsertUserTokenForModelConfig(
          savedModelConfig.id,
          modelConfigFormState.userToken.trim(),
        );
        setSavedUserTokensByConfigId((currentTokens) => ({
          ...currentTokens,
          [savedUserToken.aiModelConfigId]: savedUserToken,
        }));
        setDraftTokenValuesByConfigId((currentValues) => ({
          ...currentValues,
          [savedUserToken.aiModelConfigId]: savedUserToken.token,
        }));
      }

      const refreshedSavedModelConfig =
        refreshedModelConfigs.find(
          (aiModelConfig) => aiModelConfig.id === savedModelConfig.id,
        ) ?? savedModelConfig;
      setDraftInsightModelSelection((currentSelection) =>
        buildUpdatedSelectionForSection(
          currentSelection,
          editorSelectionSection,
          refreshedSavedModelConfig,
        ),
      );
      openEditModelEditor(editorSelectionSection, savedModelConfig);
      toast.success(t.translations.INSIGHT_MODEL_CONFIG_SAVED);
    } catch (error) {
      console.error("Failed to save Insight model config:", error);
      setModelConfigSaveError(
        error instanceof Error
          ? error.message
          : t.translations.INSIGHT_UNKNOWN_ERROR,
      );
    } finally {
      setIsSavingModelConfig(false);
    }
  }

  async function handleArchiveModelConfig() {
    if (!organizationId || !projectId || !editingModelConfigId) {
      return;
    }

    setIsArchivingModelConfig(true);
    setModelConfigSaveError("");

    try {
      await archiveProjectAiModelConfig(
        organizationId,
        projectId,
        editingModelConfigId,
      );
      await refreshAvailableModelConfigs();
      closeModelEditor();
      toast.success(t.translations.INSIGHT_MODEL_CONFIG_ARCHIVED);
    } catch (error) {
      console.error("Failed to archive Insight model config:", error);
      setModelConfigSaveError(
        error instanceof Error
          ? error.message
          : t.translations.INSIGHT_UNKNOWN_ERROR,
      );
    } finally {
      setIsArchivingModelConfig(false);
    }
  }

  if (!isOpen) {
    return null;
  }

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-5xl overflow-visible p-0">
        <div className="flex items-center justify-between border-b border-base-300 px-6 py-5">
          <div>
            <h3 className="text-2xl font-bold">
              {t.translations.INSIGHT_MODEL_SETTINGS}
            </h3>
            <p className="mt-1 text-sm text-base-content/70">
              {t.translations.INSIGHT_MODEL_SETTINGS_DESCRIPTION}
            </p>
          </div>
          <button
            type="button"
            className="btn btn-circle btn-ghost btn-sm"
            onClick={onClose}
          >
            <XMarkIcon className="size-5" />
          </button>
        </div>

        <div className="max-h-[80vh] overflow-y-auto px-6 py-5">
          {isLoadingModelSettings ? (
            <div className="flex items-center justify-center py-20">
              <span className="loading loading-spinner loading-lg" />
            </div>
          ) : (
            <div className="space-y-6">
              <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
                {modelSelectionSections.map((modelSelectionSection) => (
                  <InsightModelSelectionCard
                    key={modelSelectionSection.sectionKey}
                    title={modelSelectionSection.title}
                    description={modelSelectionSection.description}
                    emptyOptionLabel={t.translations.INSIGHT_NO_MODEL_SELECTED}
                    availableModelConfigs={
                      modelSelectionSection.availableModelConfigs
                    }
                    selectedModelConfigId={
                      modelSelectionSection.selectedModelConfigId
                    }
                    onSelectedModelChange={(nextModelConfigId) =>
                      updateDraftSelection(
                        modelSelectionSection.sectionKey,
                        nextModelConfigId,
                      )
                    }
                    onOpenCreateEditor={() =>
                      openCreateModelEditor(modelSelectionSection.sectionKey)
                    }
                    onOpenEditEditor={() => {
                      const selectedModelConfig =
                        modelSelectionSection.availableModelConfigs.find(
                          (aiModelConfig) =>
                            aiModelConfig.id ===
                            modelSelectionSection.selectedModelConfigId,
                        );

                      if (selectedModelConfig) {
                        openEditModelEditor(
                          modelSelectionSection.sectionKey,
                          selectedModelConfig,
                        );
                      }
                    }}
                  />
                ))}
              </div>

              {isModelEditorVisible ? (
                <div className="rounded-box border border-base-300 bg-base-100">
                  <div className="border-b border-base-300 px-5 py-4">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <h4 className="text-lg font-semibold text-base-content">
                          {modelEditorMode === "edit"
                            ? t.translations.INSIGHT_EDIT_MODEL_CONFIG
                            : t.translations.INSIGHT_CREATE_MODEL_CONFIG}
                        </h4>
                        <p className="mt-1 text-sm text-base-content/70">
                          {modelEditorMode === "edit"
                            ? t.translations.INSIGHT_EDIT_MODEL_EDITOR_DESCRIPTION
                            : t.translations.INSIGHT_MODEL_EDITOR_DESCRIPTION}
                        </p>
                      </div>
                      <div className="flex items-center gap-2">
                        {modelEditorMode === "edit" ? (
                          <span className="badge badge-outline">
                            {t.translations.INSIGHT_EDITING_EXISTING_CONFIG}
                          </span>
                        ) : null}
                      </div>
                    </div>
                  </div>

                  <div className="grid grid-cols-1 gap-4 p-5 lg:grid-cols-2">
                    <label className="form-control">
                      <span className="label-text mb-2 mr-2">
                        {t.translations.INSIGHT_MODEL_NAME}
                      </span>
                      <input
                        type="text"
                        className="input input-bordered"
                        placeholder={
                          t.translations.INSIGHT_MODEL_NAME_PLACEHOLDER
                        }
                        value={modelConfigFormState.modelName}
                        onChange={(event) =>
                          setModelConfigFormState((currentFormState) => ({
                            ...currentFormState,
                            modelName: event.target.value,
                          }))
                        }
                      />
                    </label>

                    <label className="form-control">
                      <span className="label-text mb-2 mr-2">
                        {t.translations.INSIGHT_MODEL_PROVIDER}
                      </span>
                      <select
                        className="select select-bordered"
                        value={modelConfigFormState.modelProvider}
                        onChange={(event) =>
                          setModelConfigFormState((currentFormState) => ({
                            ...currentFormState,
                            modelProvider: event.target
                              .value as AiModelProvider,
                          }))
                        }
                        disabled={modelEditorMode === "edit"}
                      >
                        {MODEL_PROVIDER_OPTIONS.map((modelProviderOption) => (
                          <option
                            key={modelProviderOption.value}
                            value={modelProviderOption.value}
                          >
                            {modelProviderOption.label}
                          </option>
                        ))}
                      </select>
                    </label>

                    <label className="form-control">
                      <span className="label-text mb-2 mr-2">
                        {t.translations.INSIGHT_MODEL_TYPE}
                      </span>
                      <select
                        className="select select-bordered"
                        value={modelConfigFormState.modelType}
                        onChange={(event) =>
                          setModelConfigFormState((currentFormState) => ({
                            ...currentFormState,
                            modelType: event.target.value as AiModelType,
                          }))
                        }
                      >
                        {MODEL_TYPE_OPTIONS.map((modelTypeOption) => (
                          <option
                            key={modelTypeOption.value}
                            value={modelTypeOption.value}
                          >
                            {modelTypeOption.label}
                          </option>
                        ))}
                      </select>
                    </label>

                    <label className="form-control">
                      <span className="label-text mb-2 mr-2">
                        {t.translations.INSIGHT_SERVER_URL}
                      </span>
                      <input
                        type="text"
                        className="input input-bordered"
                        placeholder={
                          t.translations.INSIGHT_SERVER_URL_PLACEHOLDER
                        }
                        value={modelConfigFormState.serverUrl}
                        onChange={(event) =>
                          setModelConfigFormState((currentFormState) => ({
                            ...currentFormState,
                            serverUrl: event.target.value,
                          }))
                        }
                      />
                    </label>

                    <label className="form-control rounded-box border border-base-300 p-4">
                      <span className="label cursor-pointer justify-start gap-3">
                        <input
                          type="checkbox"
                          className="checkbox checkbox-primary"
                          checked={modelConfigFormState.requiresToken}
                          onChange={(event) =>
                            setModelConfigFormState((currentFormState) => ({
                              ...currentFormState,
                              requiresToken: event.target.checked,
                              userToken: event.target.checked
                                ? currentFormState.userToken
                                : "",
                            }))
                          }
                        />
                        <span className="label-text font-medium">
                          {t.translations.INSIGHT_REQUIRES_TOKEN}
                        </span>
                      </span>
                    </label>

                    <label className="form-control rounded-box border border-base-300 p-4">
                      <span className="label cursor-pointer justify-start gap-3">
                        <input
                          type="checkbox"
                          className="checkbox checkbox-primary"
                          checked={modelConfigFormState.isDefaultConfig}
                          onChange={(event) =>
                            setModelConfigFormState((currentFormState) => ({
                              ...currentFormState,
                              isDefaultConfig: event.target.checked,
                            }))
                          }
                        />
                        <span className="label-text font-medium">
                          {t.translations.INSIGHT_DEFAULT_MODEL}
                        </span>
                      </span>
                    </label>

                    {modelConfigFormState.requiresToken ? (
                      <label className="form-control lg:col-span-2">
                        <span className="label-text mb-2">
                          {t.translations.INSIGHT_USER_TOKEN}
                        </span>
                        <div className="join w-full">
                          <input
                            type={isEditorTokenVisible ? "text" : "password"}
                            className="input input-bordered join-item w-full"
                            placeholder={
                              t.translations.INSIGHT_USER_TOKEN_PLACEHOLDER
                            }
                            value={modelConfigFormState.userToken}
                            onChange={(event) =>
                              setModelConfigFormState((currentFormState) => ({
                                ...currentFormState,
                                userToken: event.target.value,
                              }))
                            }
                          />
                          <button
                            type="button"
                            className="btn btn-outline join-item"
                            onClick={() =>
                              setIsEditorTokenVisible(
                                (currentIsEditorTokenVisible) =>
                                  !currentIsEditorTokenVisible,
                              )
                            }
                            title={
                              t.translations.INSIGHT_TOGGLE_TOKEN_VISIBILITY
                            }
                            aria-label={
                              t.translations.INSIGHT_TOGGLE_TOKEN_VISIBILITY
                            }
                          >
                            {isEditorTokenVisible ? (
                              <EyeSlashIcon className="size-4" />
                            ) : (
                              <EyeIcon className="size-4" />
                            )}
                          </button>
                        </div>
                      </label>
                    ) : null}
                  </div>

                  {modelConfigSaveError ? (
                    <div className="px-5 pb-5">
                      <div className="alert alert-error">
                        <ExclamationCircleIcon className="size-5" />
                        <span>{modelConfigSaveError}</span>
                      </div>
                    </div>
                  ) : null}

                  <div className="border-t border-base-300 px-5 py-4">
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        {modelEditorMode === "edit" && editingModelConfigId ? (
                          <button
                            type="button"
                            className="btn btn-error btn-outline"
                            onClick={() => {
                              void handleArchiveModelConfig();
                            }}
                            disabled={isSavingModelConfig || isArchivingModelConfig}
                          >
                            {isArchivingModelConfig ? (
                              <span className="loading loading-spinner loading-sm" />
                            ) : null}
                            {t.translations.INSIGHT_ARCHIVE_MODEL_CONFIG}
                          </button>
                        ) : null}
                      </div>
                      <div className="flex justify-end gap-3">
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={closeModelEditor}
                          disabled={isSavingModelConfig || isArchivingModelConfig}
                        >
                          {t.translations.CANCEL}
                        </button>
                      <button
                        type="button"
                        className="btn btn-primary gap-2"
                        disabled={isSavingModelConfig || isArchivingModelConfig}
                        onClick={() => {
                          void handleSaveModelConfig();
                        }}
                      >
                        {isSavingModelConfig ? (
                          <span className="loading loading-spinner loading-sm" />
                        ) : (
                          <CheckCircleIcon className="size-4" />
                        )}
                        {modelEditorMode === "edit"
                          ? t.translations.SAVE_CHANGES
                          : t.translations.CREATE}
                      </button>
                      </div>
                    </div>
                  </div>
                </div>
              ) : null}
            </div>
          )}
        </div>

        <div className="modal-action m-0 border-t border-base-300 px-6 py-4">
          {selectedModelConfigsMissingTokens.length > 0 ? (
            <div className="mr-auto max-w-2xl text-sm text-warning">
              {t.translations.INSIGHT_TOKENS_REQUIRED_BEFORE_SAVE}:{" "}
              {selectedModelConfigsMissingTokens
                .map((aiModelConfig) => aiModelConfig.modelName)
                .join(", ")}
            </div>
          ) : null}
          <button type="button" className="btn btn-ghost" onClick={onClose}>
            {t.translations.CANCEL}
          </button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              if (selectedModelConfigsMissingTokens.length > 0) {
                toast.error(t.translations.INSIGHT_TOKENS_REQUIRED_BEFORE_SAVE);
                return;
              }
              onSaveSelection(draftInsightModelSelection);
              onClose();
            }}
            disabled={selectedModelConfigsMissingTokens.length > 0}
          >
            {t.translations.INSIGHT_SAVE_SELECTION}
          </button>
        </div>
      </div>

      <div className="modal-backdrop" onClick={onClose} />
    </div>
  );
}
