"use client";

import { useEffect, useMemo, useState } from "react";
import {
  ArchiveBoxIcon,
  ArchiveBoxXMarkIcon,
  CheckCircleIcon,
  CpuChipIcon,
  ExclamationTriangleIcon,
  InformationCircleIcon,
  PencilIcon,
  PlusIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import toast from "react-hot-toast";
import Tabs from "@/app/(home)/components/Tabs";
import type {
  AiModelProvider,
  AiModelType,
  CreateAiModelConfigRequestDto,
  UpdateAiModelConfigRequestDto,
} from "@/app/(home)/types/requestDTOs";
import type { AiModelConfigResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import {
  archiveProjectAiModelConfig,
  createProjectAiModelConfig,
  getProjectAiModelConfigs,
  updateProjectAiModelConfig,
} from "@/app/lib/client_service/ai_model_config_services.client";

type ModelTemplateTab = "defaults" | "manage";
type ModelEditorMode = "create" | "edit";
type ProjectDefaultRole = "query" | "upload" | "embedding";

interface ProjectInsightModelTemplateSectionProps {
  organizationId?: number;
  projectId?: number;
}

interface ModelTemplateFormState {
  modelName: string;
  modelType: AiModelType;
  modelProvider: AiModelProvider;
  serverUrl: string;
  requiresToken: boolean;
}

interface ProjectDefaultSelectionState {
  query: number | null;
  upload: number | null;
  embedding: number | null;
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

const DEFAULT_MODEL_TEMPLATE_FORM_STATE: ModelTemplateFormState = {
  modelName: "",
  modelType: "llm",
  modelProvider: "openai",
  serverUrl: "",
  requiresToken: false,
};

function buildProjectDefaultSelectionState(
  allModelConfigs: AiModelConfigResponseDto[],
): ProjectDefaultSelectionState {
  return {
    query:
      allModelConfigs.find(
        (modelConfig) =>
          modelConfig.projectId &&
          !modelConfig.isArchived &&
          modelConfig.default &&
          modelConfig.modelType === "llm",
      )?.id ?? null,
    upload:
      allModelConfigs.find(
        (modelConfig) =>
          modelConfig.projectId &&
          !modelConfig.isArchived &&
          modelConfig.default &&
          modelConfig.modelType === "vlm",
      )?.id ?? null,
    embedding:
      allModelConfigs.find(
        (modelConfig) =>
          modelConfig.projectId &&
          !modelConfig.isArchived &&
          modelConfig.default &&
          modelConfig.modelType === "embedding",
      )?.id ?? null,
  };
}

function createEmptyModelTemplateFormState(
  modelType: AiModelType,
): ModelTemplateFormState {
  return {
    ...DEFAULT_MODEL_TEMPLATE_FORM_STATE,
    modelType,
  };
}

function buildModelTemplateOptionLabel(
  modelConfig: AiModelConfigResponseDto,
  labels: { defaultLabel: string },
): string {
  const defaultLabel = modelConfig.default ? ` • ${labels.defaultLabel}` : "";

  return `${modelConfig.modelName} (${modelConfig.modelProvider})${defaultLabel}`;
}

function getRoleModelType(projectDefaultRole: ProjectDefaultRole): AiModelType {
  if (projectDefaultRole === "upload") {
    return "vlm";
  }

  if (projectDefaultRole === "embedding") {
    return "embedding";
  }

  return "llm";
}

function buildActiveModelConfigForRole(
  projectModelConfigs: AiModelConfigResponseDto[],
  projectDefaultRole: ProjectDefaultRole,
): AiModelConfigResponseDto | null {
  const modelType = getRoleModelType(projectDefaultRole);

  return (
    projectModelConfigs.find(
      (modelConfig) =>
        modelConfig.projectId &&
        !modelConfig.isArchived &&
        modelConfig.default &&
        modelConfig.modelType === modelType,
    ) ?? null
  );
}

export default function ProjectInsightModelTemplateSection({
  organizationId,
  projectId,
}: ProjectInsightModelTemplateSectionProps) {
  const { t } = useLanguage();
  const [activeTab, setActiveTab] = useState<ModelTemplateTab>("defaults");
  const [availableModelConfigs, setAvailableModelConfigs] = useState<
    AiModelConfigResponseDto[]
  >([]);
  const [selectedProjectDefaultIds, setSelectedProjectDefaultIds] =
    useState<ProjectDefaultSelectionState>({
      query: null,
      upload: null,
      embedding: null,
    });
  const [isLoadingModelConfigs, setIsLoadingModelConfigs] = useState(true);
  const [isSavingProjectDefaults, setIsSavingProjectDefaults] = useState(false);
  const [isSavingModelTemplate, setIsSavingModelTemplate] = useState(false);
  const [isArchivingModelTemplate, setIsArchivingModelTemplate] =
    useState(false);
  const [isModelEditorVisible, setIsModelEditorVisible] = useState(false);
  const [modelEditorMode, setModelEditorMode] =
    useState<ModelEditorMode>("create");
  const [editingModelConfigId, setEditingModelConfigId] = useState<
    number | null
  >(null);
  const [modelTemplateFormState, setModelTemplateFormState] =
    useState<ModelTemplateFormState>(DEFAULT_MODEL_TEMPLATE_FORM_STATE);
  const [modelTemplateSaveError, setModelTemplateSaveError] = useState("");

  const activeProjectDefaultIds = useMemo(
    () => buildProjectDefaultSelectionState(availableModelConfigs),
    [availableModelConfigs],
  );
  const modelTemplateOptionLabels = useMemo(
    () => ({
      defaultLabel: t.translations.DEFAULT_BADGE,
    }),
    [t.translations.DEFAULT_BADGE],
  );
  const projectScopedModelConfigs = useMemo(
    () =>
      availableModelConfigs.filter((modelConfig) =>
        Boolean(modelConfig.projectId),
      ),
    [availableModelConfigs],
  );
  const activeProjectScopedModelConfigs = useMemo(
    () =>
      projectScopedModelConfigs.filter(
        (modelConfig) => !modelConfig.isArchived,
      ),
    [projectScopedModelConfigs],
  );
  const orderedProjectModelConfigs = useMemo(
    () =>
      [...projectScopedModelConfigs].sort(
        (leftModelConfig, rightModelConfig) => {
          if (leftModelConfig.isArchived !== rightModelConfig.isArchived) {
            return leftModelConfig.isArchived ? 1 : -1;
          }

          return leftModelConfig.modelName.localeCompare(
            rightModelConfig.modelName,
          );
        },
      ),
    [projectScopedModelConfigs],
  );

  useEffect(() => {
    if (!organizationId || !projectId) {
      setIsLoadingModelConfigs(false);
      return;
    }

    const resolvedOrganizationId = organizationId;
    const resolvedProjectId = projectId;
    let hasCancelled = false;

    async function loadProjectModelTemplates() {
      setIsLoadingModelConfigs(true);

      try {
        const loadedModelConfigs = await getProjectAiModelConfigs(
          resolvedOrganizationId,
          resolvedProjectId,
          false,
        );

        if (hasCancelled) {
          return;
        }

        setAvailableModelConfigs(loadedModelConfigs);
        setSelectedProjectDefaultIds(
          buildProjectDefaultSelectionState(loadedModelConfigs),
        );
      } catch (error) {
        console.error("Failed to load project Insight model templates:", error);
        if (!hasCancelled) {
          toast.error(t.translations.INSIGHT_PROJECT_TEMPLATES_LOAD_FAILED);
        }
      } finally {
        if (!hasCancelled) {
          setIsLoadingModelConfigs(false);
        }
      }
    }

    void loadProjectModelTemplates();

    return () => {
      hasCancelled = true;
    };
  }, [
    organizationId,
    projectId,
    t.translations.INSIGHT_PROJECT_TEMPLATES_LOAD_FAILED,
  ]);

  async function refreshProjectModelTemplates() {
    if (!organizationId || !projectId) {
      return [] as AiModelConfigResponseDto[];
    }

    const refreshedModelConfigs = await getProjectAiModelConfigs(
      organizationId,
      projectId,
      false,
    );
    setAvailableModelConfigs(refreshedModelConfigs);
    setSelectedProjectDefaultIds(
      buildProjectDefaultSelectionState(refreshedModelConfigs),
    );
    return refreshedModelConfigs;
  }

  function closeModelEditor() {
    setIsModelEditorVisible(false);
    setModelTemplateSaveError("");
    setEditingModelConfigId(null);
  }

  function openCreateModelEditor(modelType: AiModelType) {
    setActiveTab("manage");
    setModelEditorMode("create");
    setEditingModelConfigId(null);
    setModelTemplateFormState(createEmptyModelTemplateFormState(modelType));
    setModelTemplateSaveError("");
    setIsModelEditorVisible(true);
  }

  function openEditModelEditor(modelConfig: AiModelConfigResponseDto) {
    setActiveTab("manage");
    setModelEditorMode("edit");
    setEditingModelConfigId(modelConfig.id);
    setModelTemplateFormState({
      modelName: modelConfig.modelName,
      modelType: modelConfig.modelType as AiModelType,
      modelProvider: modelConfig.modelProvider as AiModelProvider,
      serverUrl: modelConfig.serverUrl,
      requiresToken: modelConfig.requiresToken,
    });
    setModelTemplateSaveError("");
    setIsModelEditorVisible(true);
  }

  async function handleSaveProjectDefaults() {
    if (!organizationId || !projectId) {
      return;
    }

    const changedProjectDefaultIds = (
      ["query", "upload", "embedding"] as ProjectDefaultRole[]
    ).filter(
      (projectDefaultRole) =>
        selectedProjectDefaultIds[projectDefaultRole] !== null &&
        selectedProjectDefaultIds[projectDefaultRole] !==
          activeProjectDefaultIds[projectDefaultRole],
    );

    if (changedProjectDefaultIds.length === 0) {
      return;
    }

    setIsSavingProjectDefaults(true);

    try {
      await Promise.all(
        changedProjectDefaultIds.map((projectDefaultRole) =>
          updateProjectAiModelConfig(
            organizationId,
            projectId,
            selectedProjectDefaultIds[projectDefaultRole] as number,
            { default: true },
          ),
        ),
      );

      await refreshProjectModelTemplates();
      toast.success(t.translations.INSIGHT_PROJECT_DEFAULTS_SAVED);
    } catch (error) {
      console.error("Failed to save project Insight model defaults:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.INSIGHT_UNKNOWN_ERROR,
      );
    } finally {
      setIsSavingProjectDefaults(false);
    }
  }

  async function handleSaveModelTemplate() {
    if (!organizationId || !projectId) {
      return;
    }

    if (
      !modelTemplateFormState.modelName.trim() ||
      !modelTemplateFormState.serverUrl.trim()
    ) {
      setModelTemplateSaveError(
        t.translations.INSIGHT_MODEL_CONFIG_REQUIRED_FIELDS,
      );
      return;
    }

    setIsSavingModelTemplate(true);
    setModelTemplateSaveError("");

    try {
      if (modelEditorMode === "edit" && editingModelConfigId) {
        const updateModelTemplateRequest: UpdateAiModelConfigRequestDto = {
          model_name: modelTemplateFormState.modelName.trim(),
          model_type: modelTemplateFormState.modelType,
          server_url: modelTemplateFormState.serverUrl.trim(),
          requires_token: modelTemplateFormState.requiresToken,
        };

        await updateProjectAiModelConfig(
          organizationId,
          projectId,
          editingModelConfigId,
          updateModelTemplateRequest,
        );
      } else {
        const createModelTemplateRequest: CreateAiModelConfigRequestDto = {
          model_name: modelTemplateFormState.modelName.trim(),
          model_provider: modelTemplateFormState.modelProvider,
          model_type: modelTemplateFormState.modelType,
          server_url: modelTemplateFormState.serverUrl.trim(),
          requires_token: modelTemplateFormState.requiresToken,
          default: false,
        };

        await createProjectAiModelConfig(
          organizationId,
          projectId,
          createModelTemplateRequest,
        );
      }

      await refreshProjectModelTemplates();
      closeModelEditor();
      toast.success(t.translations.INSIGHT_MODEL_CONFIG_SAVED);
    } catch (error) {
      console.error("Failed to save project Insight model template:", error);
      setModelTemplateSaveError(
        error instanceof Error
          ? error.message
          : t.translations.INSIGHT_UNKNOWN_ERROR,
      );
    } finally {
      setIsSavingModelTemplate(false);
    }
  }

  async function handleToggleArchive(modelConfig: AiModelConfigResponseDto) {
    if (!organizationId || !projectId || !modelConfig.projectId) {
      return;
    }

    setIsArchivingModelTemplate(true);
    setModelTemplateSaveError("");

    try {
      await archiveProjectAiModelConfig(
        organizationId,
        projectId,
        modelConfig.id,
        !modelConfig.isArchived,
      );
      await refreshProjectModelTemplates();
      if (editingModelConfigId === modelConfig.id) {
        closeModelEditor();
      }
      toast.success(
        modelConfig.isArchived
          ? t.translations.INSIGHT_MODEL_CONFIG_UNARCHIVED
          : t.translations.INSIGHT_MODEL_CONFIG_ARCHIVED,
      );
    } catch (error) {
      console.error("Failed to archive project Insight model template:", error);
      setModelTemplateSaveError(
        error instanceof Error
          ? error.message
          : t.translations.INSIGHT_UNKNOWN_ERROR,
      );
    } finally {
      setIsArchivingModelTemplate(false);
    }
  }

  const defaultSections = (
    [
      {
        roleKey: "query" as const,
        title: t.translations.INSIGHT_QUERY_MODEL,
        description: t.translations.INSIGHT_PROJECT_QUERY_DEFAULT_DESCRIPTION,
      },
      {
        roleKey: "upload" as const,
        title: t.translations.INSIGHT_UPLOAD_MODEL,
        description: t.translations.INSIGHT_PROJECT_UPLOAD_DEFAULT_DESCRIPTION,
      },
      {
        roleKey: "embedding" as const,
        title: t.translations.INSIGHT_EMBEDDING_MODEL,
        description:
          t.translations.INSIGHT_PROJECT_EMBEDDING_DEFAULT_DESCRIPTION,
      },
    ] as const
  ).map(({ roleKey, title, description }) => {
    const modelType = getRoleModelType(roleKey);
    const availableProjectTemplatesForRole =
      activeProjectScopedModelConfigs.filter(
        (modelConfig) => modelConfig.modelType === modelType,
      );
    const activeModelConfig = buildActiveModelConfigForRole(
      projectScopedModelConfigs,
      roleKey,
    );
    const selectedProjectDefaultId = selectedProjectDefaultIds[roleKey];

    return (
      <div
        key={roleKey}
        className="rounded-box border border-base-300/50 bg-base-100 p-4"
      >
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h4 className="text-base font-semibold text-base-content">
              {title}
            </h4>
            <p className="mt-1 text-sm text-base-content/70">{description}</p>
          </div>
          <button
            type="button"
            className="btn btn-ghost btn-md gap-2"
            onClick={() => openCreateModelEditor(modelType)}
          >
            <PlusIcon className="size-6" />
            {t.translations.INSIGHT_CREATE_TEMPLATE}
          </button>
        </div>

        <div className="mt-4 space-y-3">
          <div className="alert alert-info">
            <InformationCircleIcon className="size-5" />
            <span className="text-sm">
              {t.translations.INSIGHT_CURRENT_ACTIVE_TEMPLATE}:{" "}
              <strong>
                {activeModelConfig?.modelName ??
                  t.translations.INSIGHT_NO_ACTIVE_TEMPLATE}
              </strong>
            </span>
          </div>

          {availableProjectTemplatesForRole.length === 0 ? (
            <div className="alert alert-warning">
              <ExclamationTriangleIcon className="size-5" />
              <span>
                {t.translations.INSIGHT_NO_PROJECT_TEMPLATES_FOR_ROLE}
              </span>
            </div>
          ) : (
            <label className="form-control">
              <span className="label-text mb-2 mr-2">
                {t.translations.INSIGHT_PROJECT_DEFAULT_TEMPLATE}
              </span>
              <select
                className="select select-bordered"
                value={selectedProjectDefaultId ?? ""}
                onChange={(event) =>
                  setSelectedProjectDefaultIds((currentSelections) => ({
                    ...currentSelections,
                    [roleKey]: event.target.value
                      ? Number(event.target.value)
                      : null,
                  }))
                }
              >
                <option value="" disabled>
                  {t.translations.INSIGHT_SELECT_PROJECT_TEMPLATE}
                </option>
                {availableProjectTemplatesForRole.map((modelConfig) => (
                  <option key={modelConfig.id} value={modelConfig.id}>
                    {buildModelTemplateOptionLabel(
                      modelConfig,
                      modelTemplateOptionLabels,
                    )}
                  </option>
                ))}
              </select>
            </label>
          )}
        </div>
      </div>
    );
  });

  const manageTabContent = (
    <div className="mt-4 space-y-4">
      <div className="flex justify-between items-center gap-3">
        <p className="text-sm text-base-content/70"></p>
        <button
          className="btn btn-primary btn-sm"
          onClick={() => openCreateModelEditor("llm")}
        >
          <PlusIcon className="size-6" />
          {t.translations.INSIGHT_CREATE_TEMPLATE}
        </button>
      </div>

      <div className="space-y-2 max-h-96 overflow-y-auto">
        {orderedProjectModelConfigs.length === 0 ? (
          <div className="text-center py-8 text-base-content/60">
            <CpuChipIcon className="w-12 h-12 mx-auto mb-2 opacity-50" />
            <p>{t.translations.INSIGHT_NO_TEMPLATES_CREATED_YET}</p>
            <p className="text-sm">
              {t.translations.INSIGHT_CLICK_CREATE_TEMPLATE_TO_ADD_ONE}
            </p>
          </div>
        ) : (
          orderedProjectModelConfigs.map((modelConfig) => (
            <div
              key={modelConfig.id}
              className={`card border bg-base-100 shadow-sm ${
                modelConfig.isArchived
                  ? "border-warning/30 opacity-60"
                  : "border-base-300/50"
              }`}
            >
              <div className="card-body p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="flex-1">
                    <div className="flex flex-wrap items-center gap-2 mb-1">
                      <h4 className="font-semibold">{modelConfig.modelName}</h4>
                      {modelConfig.default ? (
                        <span className="badge badge-primary badge-sm">
                          {t.translations.DEFAULT_BADGE}
                        </span>
                      ) : null}
                      {modelConfig.requiresToken ? (
                        <span className="badge badge-secondary badge-sm">
                          {t.translations.INSIGHT_REQUIRES_TOKEN}
                        </span>
                      ) : null}
                      {modelConfig.isArchived ? (
                        <span className="badge badge-warning badge-sm">
                          {t.translations.ARCHIVED_BADGE}
                        </span>
                      ) : null}
                    </div>
                    <p className="text-xs text-base-content/60">
                      {modelConfig.modelType.toUpperCase()} •{" "}
                      {modelConfig.modelProvider}
                    </p>
                    <p className="text-xs text-base-content/60 break-all">
                      {modelConfig.serverUrl}
                    </p>
                    <p className="text-xs text-base-content/60">
                      {t.translations.LAST_UPDATED_LABEL}{" "}
                      {new Date(modelConfig.lastUpdatedAt).toLocaleDateString()}
                    </p>
                  </div>

                  {modelConfig.projectId ? (
                    <div className="flex gap-1">
                      <button
                        className="btn btn-ghost btn-xs disabled:cursor-not-allowed"
                        onClick={() => openEditModelEditor(modelConfig)}
                        disabled={modelConfig.isArchived}
                        title={
                          modelConfig.isArchived
                            ? t.translations
                                .INSIGHT_ARCHIVED_TEMPLATE_CANNOT_BE_EDITED
                            : t.translations.INSIGHT_EDIT_TEMPLATE
                        }
                      >
                        <PencilIcon className="size-6" />
                      </button>
                      <button
                        className="btn btn-ghost btn-xs disabled:cursor-not-allowed"
                        onClick={() => {
                          void handleToggleArchive(modelConfig);
                        }}
                        disabled={
                          isArchivingModelTemplate ||
                          (!modelConfig.isArchived && modelConfig.default)
                        }
                        title={
                          modelConfig.isArchived
                            ? t.translations.UNARCHIVE
                            : modelConfig.default
                              ? t.translations
                                  .INSIGHT_DEFAULT_TEMPLATE_CANNOT_BE_ARCHIVED
                              : t.translations.ARCHIVE
                        }
                      >
                        {modelConfig.isArchived ? (
                          <ArchiveBoxXMarkIcon className="size-6" />
                        ) : (
                          <ArchiveBoxIcon className="size-6" />
                        )}
                      </button>
                    </div>
                  ) : null}
                </div>
              </div>
            </div>
          ))
        )}
      </div>

      {isModelEditorVisible ? (
        <div className="rounded-box border border-base-300/50 bg-base-100">
          <div className="border-b border-base-300/50 px-5 py-4">
            <div className="flex items-center justify-between gap-3">
              <div>
                <h4 className="text-lg font-semibold text-base-content">
                  {modelEditorMode === "edit"
                    ? t.translations.INSIGHT_EDIT_TEMPLATE
                    : t.translations.INSIGHT_CREATE_TEMPLATE}
                </h4>
                <p className="mt-1 text-sm text-base-content/70">
                  {t.translations.INSIGHT_PROJECT_TEMPLATE_EDITOR_DESCRIPTION}
                </p>
              </div>
              <button
                type="button"
                className="btn btn-circle btn-ghost btn-sm"
                onClick={closeModelEditor}
              >
                <XMarkIcon className="size-6" />
              </button>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 p-5 lg:grid-cols-2">
            {/* Project templates are connection templates only. User tokens stay in the Insight chat settings flow. */}
            <label className="form-control">
              <span className="label-text mb-2">
                {t.translations.INSIGHT_MODEL_NAME}
              </span>
              <input
                type="text"
                className="input input-bordered"
                placeholder={t.translations.INSIGHT_MODEL_NAME_PLACEHOLDER}
                value={modelTemplateFormState.modelName}
                onChange={(event) =>
                  setModelTemplateFormState((currentFormState) => ({
                    ...currentFormState,
                    modelName: event.target.value,
                  }))
                }
              />
            </label>

            <label className="form-control">
              <span className="label-text mb-2">
                {t.translations.INSIGHT_MODEL_PROVIDER}
              </span>
              <select
                className="select select-bordered"
                value={modelTemplateFormState.modelProvider}
                onChange={(event) =>
                  setModelTemplateFormState((currentFormState) => ({
                    ...currentFormState,
                    modelProvider: event.target.value as AiModelProvider,
                  }))
                }
                disabled={modelEditorMode === "edit"}
              >
                {MODEL_PROVIDER_OPTIONS.map((providerOption) => (
                  <option
                    key={providerOption.value}
                    value={providerOption.value}
                  >
                    {providerOption.label}
                  </option>
                ))}
              </select>
            </label>

            <label className="form-control">
              <span className="label-text mb-2">
                {t.translations.INSIGHT_MODEL_TYPE}
              </span>
              <select
                className="select select-bordered"
                value={modelTemplateFormState.modelType}
                onChange={(event) =>
                  setModelTemplateFormState((currentFormState) => ({
                    ...currentFormState,
                    modelType: event.target.value as AiModelType,
                  }))
                }
              >
                <option value="llm">LLM</option>
                <option value="vlm">VLM</option>
                <option value="embedding">Embedding</option>
              </select>
            </label>

            <label className="form-control">
              <span className="label-text mb-2">
                {t.translations.INSIGHT_SERVER_URL}
              </span>
              <input
                type="text"
                className="input input-bordered"
                placeholder={t.translations.INSIGHT_SERVER_URL_PLACEHOLDER}
                value={modelTemplateFormState.serverUrl}
                onChange={(event) =>
                  setModelTemplateFormState((currentFormState) => ({
                    ...currentFormState,
                    serverUrl: event.target.value,
                  }))
                }
              />
            </label>

            <label className="form-control rounded-box border border-base-300/50 p-4">
              <span className="label cursor-pointer justify-start gap-3">
                <input
                  type="checkbox"
                  className="checkbox checkbox-primary"
                  checked={modelTemplateFormState.requiresToken}
                  onChange={(event) =>
                    setModelTemplateFormState((currentFormState) => ({
                      ...currentFormState,
                      requiresToken: event.target.checked,
                    }))
                  }
                />
                <span className="label-text font-medium">
                  {t.translations.INSIGHT_REQUIRES_TOKEN}
                </span>
              </span>
            </label>
          </div>

          {modelTemplateSaveError ? (
            <div className="px-5 pb-5">
              <div className="alert alert-error">
                <ExclamationTriangleIcon className="size-5" />
                <span>{modelTemplateSaveError}</span>
              </div>
            </div>
          ) : null}

          <div className="border-t border-base-300/50 px-5 py-4">
            <div className="flex justify-end gap-3">
              <button
                type="button"
                className="btn btn-ghost"
                onClick={closeModelEditor}
                disabled={isSavingModelTemplate}
              >
                {t.translations.CANCEL}
              </button>
              <button
                type="button"
                className="btn btn-primary gap-2"
                onClick={() => {
                  void handleSaveModelTemplate();
                }}
                disabled={isSavingModelTemplate}
              >
                {isSavingModelTemplate ? (
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
      ) : null}
    </div>
  );

  const defaultsTabContent = (
    <div className="mt-4 space-y-4">
      <p className="text-sm text-base-content/70">
        {t.translations.INSIGHT_PROJECT_DEFAULTS_DESCRIPTION}
      </p>
      {/* This page manages project-owned templates only. If no project default exists yet, show the empty state and let admins create one here. */}
      <div className="space-y-4">{defaultSections}</div>
    </div>
  );

  const tabs = [
    {
      label: t.translations.INSIGHT_TEMPLATE_DEFAULTS_TAB,
      content: defaultsTabContent,
    },
    {
      label: t.translations.INSIGHT_TEMPLATE_MANAGE_TAB,
      content: manageTabContent,
    },
  ];
  const activeTabLabel =
    activeTab === "defaults"
      ? t.translations.INSIGHT_TEMPLATE_DEFAULTS_TAB
      : t.translations.INSIGHT_TEMPLATE_MANAGE_TAB;
  const hasPendingProjectDefaultChanges = (
    ["query", "upload", "embedding"] as ProjectDefaultRole[]
  ).some(
    (projectDefaultRole) =>
      selectedProjectDefaultIds[projectDefaultRole] !== null &&
      selectedProjectDefaultIds[projectDefaultRole] !==
        activeProjectDefaultIds[projectDefaultRole],
  );

  if (!organizationId || !projectId) {
    return null;
  }

  return (
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body">
        <div className="flex justify-between gap-3">
          <div className="flex items-center gap-2 mb-4">
            <CpuChipIcon className="size-10 text-primary" />
            <div>
              <h3 className="card-title text-lg">
                {t.translations.INSIGHT_MODEL_TEMPLATE}
              </h3>
              <p className="text-sm font-normal text-base-content/70">
                {t.translations.INSIGHT_MODEL_TEMPLATE_DESCRIPTION}
              </p>
            </div>
          </div>
          {activeTab === "defaults" && hasPendingProjectDefaultChanges ? (
            <button
              className="btn btn-primary btn-sm mt-2 ml-4"
              onClick={() => {
                void handleSaveProjectDefaults();
              }}
              disabled={isSavingProjectDefaults}
            >
              {isSavingProjectDefaults ? (
                <span className="loading loading-spinner loading-xs" />
              ) : null}
              {t.translations.INSIGHT_SAVE_PROJECT_DEFAULTS}
            </button>
          ) : null}
        </div>

        {isLoadingModelConfigs ? (
          <div className="flex items-center justify-center py-10">
            <span className="loading loading-spinner loading-lg" />
          </div>
        ) : (
          <Tabs
            tabs={tabs}
            activeTab={activeTabLabel}
            onTabChange={(label) =>
              setActiveTab(
                label === t.translations.INSIGHT_TEMPLATE_DEFAULTS_TAB
                  ? "defaults"
                  : "manage",
              )
            }
            className="mb-4"
          />
        )}
      </div>
    </div>
  );
}
