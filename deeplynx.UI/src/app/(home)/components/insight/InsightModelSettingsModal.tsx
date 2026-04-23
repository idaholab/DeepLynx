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

type InsightSelectionSection = "query" | "upload" | "embedding";

interface InsightModelSettingsModalProps {
  isOpen: boolean;
  organizationId?: number;
  projectId?: number;
  selectedInsightModels: InsightModelSelection;
  onClose: () => void;
  onSaveSelection: (nextSelection: InsightModelSelection) => void;
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

  const modelSelectionSections = [
    {
      sectionKey: "query" as const,
      title: t.translations.INSIGHT_QUERY_MODEL,
      description: t.translations.INSIGHT_QUERY_MODEL_DESCRIPTION,
      availableModelConfigs: queryModelConfigs,
      selectedModelConfigId: draftInsightModelSelection.queryModelConfigId,
      selectedModelName: draftInsightModelSelection.queryModelName,
    },
    {
      sectionKey: "upload" as const,
      title: t.translations.INSIGHT_UPLOAD_MODEL,
      description: t.translations.INSIGHT_UPLOAD_MODEL_DESCRIPTION,
      availableModelConfigs: uploadModelConfigs,
      selectedModelConfigId: draftInsightModelSelection.uploadModelConfigId,
      selectedModelName: draftInsightModelSelection.uploadModelName,
    },
    {
      sectionKey: "embedding" as const,
      title: t.translations.INSIGHT_EMBEDDING_MODEL,
      description: t.translations.INSIGHT_EMBEDDING_MODEL_DESCRIPTION,
      availableModelConfigs: embeddingModelConfigs,
      selectedModelConfigId: draftInsightModelSelection.embeddingModelConfigId,
      selectedModelName: draftInsightModelSelection.embeddingModelName,
    },
  ];

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    // Reset the modal from the persisted selection each time it opens so
    // unsaved edits do not leak between sessions.
    setDraftInsightModelSelection(
      syncSelectedModelNames(
        selectedInsightModels,
        availableModelConfigs,
        defaultModelLabel,
      ),
    );
    setActiveTokenEditor(null);
    setIsEditorTokenVisible(false);
    setTokenSaveError("");
  }, [
    availableModelConfigs,
    defaultModelLabel,
    isOpen,
    selectedInsightModels,
  ]);

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
    organizationId,
    projectId,
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
        ? await updateUserModelToken(currentUserId, existingUserToken.id, {
            token: trimmedTokenValue,
          })
        : await createUserModelToken(currentUserId, {
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
