"use client";

import { useEffect, useMemo, useState } from "react";
import {
  CheckCircleIcon,
  ExclamationCircleIcon,
  EyeIcon,
  EyeSlashIcon,
  KeyIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import toast from "react-hot-toast";
import { useLanguage } from "@/app/contexts/Language";
import { useRBAC } from "@/app/(home)/rbac/useRBAC";
import type { AiModelType } from "@/app/(home)/types/requestDTOs";
import type {
  AiModelConfigResponseDto,
  UserModelTokenResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { getProjectAiModelConfigs } from "@/app/lib/client_service/ai_model_config_services.client";
import {
  createUserModelToken,
  getUserModelTokens,
  updateUserModelToken,
} from "@/app/lib/client_service/user_model_token_services.client";
import type { InsightModelSelection } from "./useInsightModelSelection";
import { fetchInsightEndpointHealth } from "@/app/lib/client_service/insight_services.client";
import type { InsightEndpointHealthByRole, InsightModelHealthState } from "@/app/lib/client_service/insight_services.client";

type InsightSelectionSection = "query" | "upload" | "embedding";

interface InsightModelSettingsModalProps {
  isOpen: boolean;
  organizationId?: number | string | null;
  projectId?: number | string | null;
  selectedInsightModels: InsightModelSelection;
  onClose: () => void;
  onSaveSelection: (nextSelection: InsightModelSelection) => void;
  endpointHealth?: InsightEndpointHealthByRole;
}

interface InsightModelSelectionCardProps {
  title: string;
  description: string;
  defaultModelLabel: string;
  availableModelConfigs: AiModelConfigResponseDto[];
  selectedModelConfigId: number | null;
  selectedModelName: string | null;
  savedUserTokensByConfigId: Record<number, UserModelTokenResponseDto>;
  onSelectedModelChange: (nextModelConfigId: number | null) => void;
  onOpenTokenEditor: () => void;
  endpointHealth?: InsightModelHealthState;
}

interface InsightTokenEditorState {
  modelConfigId: number;
  modelName: string;
  tokenValue: string;
  tokenStatus: "saved" | "missing";
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

function buildUpdatedSelectionForSection(
  currentSelection: InsightModelSelection,
  insightSelectionSection: InsightSelectionSection,
  selectedModelConfig: AiModelConfigResponseDto | null,
  defaultModelLabel: string,
): InsightModelSelection {
  // A null config id intentionally means "use the backend default Nexus Model".
  const nextModelConfigId = selectedModelConfig?.id ?? null;
  const nextModelName = selectedModelConfig?.modelName ?? defaultModelLabel;

  if (insightSelectionSection === "query") {
    return {
      ...currentSelection,
      queryModelConfigId: nextModelConfigId,
      queryModelName: nextModelName,
    };
  }

  if (insightSelectionSection === "upload") {
    return {
      ...currentSelection,
      uploadModelConfigId: nextModelConfigId,
      uploadModelName: nextModelName,
    };
  }

  return {
    ...currentSelection,
    embeddingModelConfigId: nextModelConfigId,
    embeddingModelName: nextModelName,
  };
}

function syncSelectedModelNames(
  currentSelection: InsightModelSelection,
  availableModelConfigs: AiModelConfigResponseDto[],
  defaultModelLabel: string,
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
    queryModelName: queryModelConfig?.modelName ?? defaultModelLabel,
    uploadModelConfigId: uploadModelConfig?.id ?? null,
    uploadModelName: uploadModelConfig?.modelName ?? defaultModelLabel,
    embeddingModelConfigId: embeddingModelConfig?.id ?? null,
    embeddingModelName: embeddingModelConfig?.modelName ?? defaultModelLabel,
  };
}

function InsightModelSelectionCard({
  title,
  description,
  defaultModelLabel,
  availableModelConfigs,
  selectedModelConfigId,
  selectedModelName,
  savedUserTokensByConfigId,
  onSelectedModelChange,
  onOpenTokenEditor,
  endpointHealth,
}: InsightModelSelectionCardProps) {
  const { t } = useLanguage();
  const selectedModelConfig =
    availableModelConfigs.find(
      (aiModelConfig) => aiModelConfig.id === selectedModelConfigId,
    ) ?? null;
  const activeModelName = selectedModelName ?? defaultModelLabel;
  const tokenIsSaved = selectedModelConfig
    ? Boolean(savedUserTokensByConfigId[selectedModelConfig.id])
    : false;
  const tokenActionIsEnabled = Boolean(
    selectedModelConfig?.requiresToken,
  );

  return (
    <div className="rounded-box border border-base-300 bg-base-100 p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h4 className="text-base font-semibold text-base-content">{title}</h4>
          <p className="mt-1 text-sm text-base-content/70">{description}</p>
        </div>
        <button
          type="button"
          className="btn btn-ghost btn-sm gap-2"
          onClick={onOpenTokenEditor}
          disabled={!tokenActionIsEnabled}
        >
          <KeyIcon className="size-4" />
          {t.translations.INSIGHT_MANAGE_TOKEN}
        </button>
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
          <option value="">{defaultModelLabel}</option>
          {availableModelConfigs.map((aiModelConfig) => (
            <option key={aiModelConfig.id} value={aiModelConfig.id}>
              {buildModelConfigOptionLabel(aiModelConfig)}
            </option>
          ))}
        </select>
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-2 text-sm">
        <span className="font-medium text-base-content/70">
          {t.translations.INSIGHT_ACTIVE_MODEL}:
        </span>
        <span className="badge badge-outline">{activeModelName}</span>
        {selectedModelConfig?.requiresToken ? (
          tokenIsSaved ? (
            <span className="badge badge-success badge-outline">
              {t.translations.INSIGHT_TOKEN_SAVED}
            </span>
          ) : (
            <span className="badge badge-warning badge-outline">
              {t.translations.INSIGHT_TOKEN_MISSING}
            </span>
          )
        ) : (
          <span className="badge badge-ghost">
            {t.translations.INSIGHT_NO_TOKEN_REQUIRED}
          </span>
        )}
      </div>
      {endpointHealth ? (
          <div className="mt-3 rounded-box border border-base-300 bg-base-200/50 p-3 text-sm">
            {endpointHealth.isChecking ? (
                <div className="flex items-center gap-2 text-base-content/70">
                  <span className="loading loading-spinner loading-xs" />
                  <span>Checking endpoint health...</span>
                </div>
            ) : endpointHealth.response ? (
                <div className="space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
          <span
              className={`badge badge-sm ${
                  endpointHealth.response.reachable &&
                  endpointHealth.response.model_available
                      ? "badge-success"
                      : "badge-warning"
              }`}
          >
            {endpointHealth.response.reachable &&
            endpointHealth.response.model_available
                ? "Healthy"
                : "Unavailable"}
          </span>

                    {typeof endpointHealth.response.latency_ms === "number" ? (
                        <span className="text-xs text-base-content/60">
              {endpointHealth.response.latency_ms} ms
            </span>
                    ) : null}
                  </div>

                  {endpointHealth.response.detail ? (
                      <p className="text-xs text-base-content/70">
                        {endpointHealth.response.detail}
                      </p>
                  ) : null}
                </div>
            ) : endpointHealth.error ? (
                <div className="space-y-2">
                  <span className="badge badge-error badge-sm">Unavailable</span>
                  <p className="text-xs text-base-content/70">{endpointHealth.error}</p>
                </div>
            ) : (
                <span className="text-xs text-base-content/60">
        Endpoint health has not been checked yet.
      </span>
            )}
          </div>
      ) : null}
    </div>
  );
}

function resolveNumericId(id?: number | string | null): number | undefined {
  if (id === undefined || id === null) {
    return undefined;
  }

  const parsedId = typeof id === "number" ? id : Number(id);

  return Number.isFinite(parsedId) ? parsedId : undefined;
}

export default function InsightModelSettingsModal({
  isOpen,
  organizationId,
  projectId,
  selectedInsightModels,
  onClose,
  onSaveSelection,
  endpointHealth,
}: InsightModelSettingsModalProps) {
  const { t } = useLanguage();
  const { user } = useRBAC();
  const currentUserId = user?.id;
  const resolvedOrganizationId = useMemo(
    () => resolveNumericId(organizationId),
    [organizationId],
  );
  const resolvedProjectId = useMemo(
    () => resolveNumericId(projectId),
    [projectId],
  );
  const defaultModelLabel = t.translations.INSIGHT_NEXUS_MODEL;
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
  const [activeTokenEditor, setActiveTokenEditor] =
    useState<InsightTokenEditorState | null>(null);
  const [isEditorTokenVisible, setIsEditorTokenVisible] = useState(false);
  const [isLoadingModelSettings, setIsLoadingModelSettings] = useState(false);
  const [isSavingUserToken, setIsSavingUserToken] = useState(false);
  const [tokenSaveError, setTokenSaveError] = useState("");
  const [draftEndpointHealth, setDraftEndpointHealth] = useState<InsightEndpointHealthByRole | null>(null);

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
      // Keep the config template visible in the dropdown, but do not let the user
      // assign it as an active model until their personal token is saved.
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

  useEffect(() => {
    if (
        !isOpen ||
        !resolvedOrganizationId ||
        !resolvedProjectId ||
        isLoadingModelSettings
    ) {
      setDraftEndpointHealth(null);
      return;
    }

    const organizationIdForHealthCheck = resolvedOrganizationId;
    const projectIdForHealthCheck = resolvedProjectId;

    let cancelled = false;

    async function checkDraftEndpointHealth() {
      setDraftEndpointHealth({
        query: { isChecking: true, response: null, error: null },
        upload: { isChecking: true, response: null, error: null },
        embedding: { isChecking: true, response: null, error: null },
      });

      const [queryHealth, uploadHealth, embeddingHealth] =
          await Promise.allSettled([
            fetchInsightEndpointHealth({
              organizationId: organizationIdForHealthCheck,
              projectId: projectIdForHealthCheck,
              modelConfigId: draftInsightModelSelection.queryModelConfigId,
              modelType: "llm",
            }),
            fetchInsightEndpointHealth({
              organizationId: organizationIdForHealthCheck,
              projectId: projectIdForHealthCheck,
              modelConfigId: draftInsightModelSelection.uploadModelConfigId,
              modelType: "vlm",
            }),
            fetchInsightEndpointHealth({
              organizationId: organizationIdForHealthCheck,
              projectId: projectIdForHealthCheck,
              modelConfigId: draftInsightModelSelection.embeddingModelConfigId,
              modelType: "embedding",
            }),
          ]);

      if (cancelled) return;

      setDraftEndpointHealth({
        query:
            queryHealth.status === "fulfilled"
                ? { isChecking: false, response: queryHealth.value, error: null }
                : {
                  isChecking: false,
                  response: null,
                  error:
                      queryHealth.reason instanceof Error
                          ? queryHealth.reason.message
                          : "Query model health check failed",
                },
        upload:
            uploadHealth.status === "fulfilled"
                ? { isChecking: false, response: uploadHealth.value, error: null }
                : {
                  isChecking: false,
                  response: null,
                  error:
                      uploadHealth.reason instanceof Error
                          ? uploadHealth.reason.message
                          : "Upload/OCR model health check failed",
                },
        embedding:
            embeddingHealth.status === "fulfilled"
                ? { isChecking: false, response: embeddingHealth.value, error: null }
                : {
                  isChecking: false,
                  response: null,
                  error:
                      embeddingHealth.reason instanceof Error
                          ? embeddingHealth.reason.message
                          : "Embedding model health check failed",
                },
      });
    }

    void checkDraftEndpointHealth();

    return () => {
      cancelled = true;
    };
  }, [
    draftInsightModelSelection.queryModelConfigId,
    draftInsightModelSelection.uploadModelConfigId,
    draftInsightModelSelection.embeddingModelConfigId,
    isLoadingModelSettings,
    isOpen,
    resolvedOrganizationId,
    resolvedProjectId,
  ]);
  
  const modelSelectionSections = [
    {
      sectionKey: "query" as const,
      title: t.translations.INSIGHT_QUERY_MODEL,
      description: t.translations.INSIGHT_QUERY_MODEL_DESCRIPTION,
      availableModelConfigs: queryModelConfigs,
      selectedModelConfigId: draftInsightModelSelection.queryModelConfigId,
      selectedModelName: draftInsightModelSelection.queryModelName,
      endpointHealth: draftEndpointHealth?.query ?? endpointHealth?.query,
    },
    {
      sectionKey: "upload" as const,
      title: t.translations.INSIGHT_UPLOAD_MODEL,
      description: t.translations.INSIGHT_UPLOAD_MODEL_DESCRIPTION,
      availableModelConfigs: uploadModelConfigs,
      selectedModelConfigId: draftInsightModelSelection.uploadModelConfigId,
      selectedModelName: draftInsightModelSelection.uploadModelName,
      endpointHealth: draftEndpointHealth?.upload ?? endpointHealth?.upload,
    },
    {
      sectionKey: "embedding" as const,
      title: t.translations.INSIGHT_EMBEDDING_MODEL,
      description: t.translations.INSIGHT_EMBEDDING_MODEL_DESCRIPTION,
      availableModelConfigs: embeddingModelConfigs,
      selectedModelConfigId: draftInsightModelSelection.embeddingModelConfigId,
      selectedModelName: draftInsightModelSelection.embeddingModelName,
      endpointHealth: draftEndpointHealth?.embedding ?? endpointHealth?.embedding,
    },
  ];

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    // Reset the modal from the persisted selection each time it opens so
    // unsaved edits do not leak between sessions.
    setDraftInsightModelSelection(selectedInsightModels);
    setActiveTokenEditor(null);
    setIsEditorTokenVisible(false);
    setTokenSaveError("");
  }, [isOpen, selectedInsightModels]);

  useEffect(() => {
    if (!isOpen || !resolvedOrganizationId || !resolvedProjectId) {
      return;
    }

    const organizationIdForRequest = resolvedOrganizationId;
    const projectIdForRequest = resolvedProjectId;
    const currentUserIdForRequest = currentUserId;
    let hasCancelled = false;

    async function loadModelSettings() {
      setIsLoadingModelSettings(true);

      try {
        const loadedModelConfigs = await getProjectAiModelConfigs(
          organizationIdForRequest,
          projectIdForRequest,
        );

        let loadedUserTokens: UserModelTokenResponseDto[] = [];
        if (currentUserIdForRequest) {
          try {
            loadedUserTokens = await getUserModelTokens();
          } catch (error) {
            console.error("Failed to load Insight user model tokens:", error);
          }
        }

        if (hasCancelled) {
          return;
        }

        setAvailableModelConfigs(loadedModelConfigs);
        setDraftInsightModelSelection((currentSelection) =>
          syncSelectedModelNames(
            currentSelection,
            loadedModelConfigs,
            defaultModelLabel,
          ),
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
    defaultModelLabel,
    isOpen,
    resolvedOrganizationId,
    resolvedProjectId,
    t.translations.INSIGHT_MODEL_CONFIGS_FAILED,
  ]);

  function closeTokenEditor() {
    setActiveTokenEditor(null);
    setTokenSaveError("");
    setIsEditorTokenVisible(false);
  }

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
        defaultModelLabel,
      ),
    );

    if (activeTokenEditor?.modelConfigId === nextModelConfigId) {
      return;
    }

    closeTokenEditor();
  }

  function openTokenEditor(modelConfig: AiModelConfigResponseDto) {
    setActiveTokenEditor({
      modelConfigId: modelConfig.id,
      modelName: modelConfig.modelName,
      tokenValue:
        draftTokenValuesByConfigId[modelConfig.id] ??
        savedUserTokensByConfigId[modelConfig.id]?.token ??
        "",
      tokenStatus: savedUserTokensByConfigId[modelConfig.id]
        ? "saved"
        : "missing",
    });
    setTokenSaveError("");
    setIsEditorTokenVisible(false);
  }

  async function handleSaveUserToken() {
    if (!currentUserId || !activeTokenEditor) {
      return;
    }

    const trimmedTokenValue = activeTokenEditor.tokenValue.trim();
    if (!trimmedTokenValue) {
      setTokenSaveError(t.translations.INSIGHT_TOKEN_REQUIRED);
      return;
    }

    setIsSavingUserToken(true);
    setTokenSaveError("");

    try {
      const existingUserToken =
        savedUserTokensByConfigId[activeTokenEditor.modelConfigId];
      const savedUserToken = existingUserToken
        ? await updateUserModelToken(existingUserToken.id, {
            token: trimmedTokenValue,
          })
        : await createUserModelToken({
            aiModelConfigId: activeTokenEditor.modelConfigId,
            token: trimmedTokenValue,
          });

      setSavedUserTokensByConfigId((currentTokens) => ({
        ...currentTokens,
        [savedUserToken.aiModelConfigId]: savedUserToken,
      }));
      setDraftTokenValuesByConfigId((currentValues) => ({
        ...currentValues,
        [savedUserToken.aiModelConfigId]: savedUserToken.token,
      }));
      setActiveTokenEditor((currentEditorState) =>
        currentEditorState
          ? {
              ...currentEditorState,
              tokenValue: savedUserToken.token,
              tokenStatus: "saved",
            }
          : null,
      );
      toast.success(t.translations.INSIGHT_TOKEN_SAVED);
    } catch (error) {
      console.error("Failed to save Insight user token:", error);
      setTokenSaveError(
        error instanceof Error
          ? error.message
          : t.translations.INSIGHT_UNKNOWN_ERROR,
      );
    } finally {
      setIsSavingUserToken(false);
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
                    defaultModelLabel={defaultModelLabel}
                    availableModelConfigs={
                      modelSelectionSection.availableModelConfigs
                    }
                    selectedModelConfigId={
                      modelSelectionSection.selectedModelConfigId
                    }
                    selectedModelName={modelSelectionSection.selectedModelName}
                    savedUserTokensByConfigId={savedUserTokensByConfigId}
                    onSelectedModelChange={(nextModelConfigId) =>
                      updateDraftSelection(
                        modelSelectionSection.sectionKey,
                        nextModelConfigId,
                      )
                    }
                    onOpenTokenEditor={() => {
                      const selectedModelConfig =
                        modelSelectionSection.availableModelConfigs.find(
                          (aiModelConfig) =>
                            aiModelConfig.id ===
                            modelSelectionSection.selectedModelConfigId,
                        );

                      if (selectedModelConfig?.requiresToken) {
                        openTokenEditor(selectedModelConfig);
                      }
                    }}
                    endpointHealth={modelSelectionSection.endpointHealth}
                  />
                ))}
              </div>

              {activeTokenEditor ? (
                <div className="rounded-box border border-base-300 bg-base-100">
                  <div className="border-b border-base-300 px-5 py-4">
                    <h4 className="text-lg font-semibold text-base-content">
                      {t.translations.INSIGHT_MANAGE_TOKEN}
                    </h4>
                    <p className="mt-1 text-sm text-base-content/70">
                      {activeTokenEditor.tokenStatus === "saved"
                        ? t.translations.INSIGHT_TOKEN_SAVED_DESCRIPTION
                        : t.translations.INSIGHT_TOKEN_REQUIRED}
                    </p>
                    <div className="mt-3">
                      <span className="badge badge-outline">
                        {activeTokenEditor.modelName}
                      </span>
                    </div>
                  </div>

                  <div className="space-y-4 p-5">
                    {/* Tokens remain user-scoped. Model templates stay at org/project scope. */}
                    <label className="form-control">
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
                          value={activeTokenEditor.tokenValue}
                          onChange={(event) =>
                            setActiveTokenEditor((currentEditorState) =>
                              currentEditorState
                                ? {
                                    ...currentEditorState,
                                    tokenValue: event.target.value,
                                  }
                                : null,
                            )
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
                          title={t.translations.INSIGHT_TOGGLE_TOKEN_VISIBILITY}
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

                    {tokenSaveError ? (
                      <div className="alert alert-error">
                        <ExclamationCircleIcon className="size-5" />
                        <span>{tokenSaveError}</span>
                      </div>
                    ) : null}
                  </div>

                  <div className="border-t border-base-300 px-5 py-4">
                    <div className="flex justify-end gap-3">
                      <button
                        type="button"
                        className="btn btn-ghost"
                        onClick={closeTokenEditor}
                        disabled={isSavingUserToken}
                      >
                        {t.translations.CANCEL}
                      </button>
                      <button
                        type="button"
                        className="btn btn-primary gap-2"
                        disabled={isSavingUserToken}
                        onClick={() => {
                          void handleSaveUserToken();
                        }}
                      >
                        {isSavingUserToken ? (
                          <span className="loading loading-spinner loading-sm" />
                        ) : (
                          <CheckCircleIcon className="size-4" />
                        )}
                        {t.translations.SAVE_CHANGES}
                      </button>
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

              onSaveSelection(
                syncSelectedModelNames(
                  draftInsightModelSelection,
                  availableModelConfigs,
                  defaultModelLabel,
                ),
              );
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
