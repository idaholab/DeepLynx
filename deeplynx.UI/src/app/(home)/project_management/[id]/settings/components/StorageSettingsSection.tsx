// src/app/(home)/project_management/[id]/settings/components/StorageSettingsSection.tsx
"use client";

import {
  ArchiveBoxIcon,
  ArchiveBoxXMarkIcon,
  CircleStackIcon,
  ExclamationTriangleIcon,
  InformationCircleIcon,
  PencilIcon,
  PlusIcon,
  TrashIcon,
} from "@heroicons/react/24/outline";
import { ObjectStorageResponseDto } from "@/app/(home)/types/responseDTOs";
import Tabs from "@/app/(home)/components/Tabs";

type StorageTab = "default" | "manage";

interface StorageSettingsSectionProps {
  activeTab: StorageTab;
  onChangeTab: (tab: StorageTab) => void;
  projectId: number | string;
  availableStorages: ObjectStorageResponseDto[];
  selectedStorageId: number | null;
  onSelectStorage: (storageId: number) => void;
  defaultStorage: ObjectStorageResponseDto | null;
  isSavingStorage: boolean;
  onSaveDefaultStorage: () => void;
  onCreateStorage: () => void;
  onEditStorage: (storage: ObjectStorageResponseDto) => void;
  onToggleArchive: (storage: ObjectStorageResponseDto) => void;
  onDeleteStorage: (storageId: number) => void;
  t: { translations: Record<string, string> };
}

const StorageSettingsSection = ({
  activeTab,
  onChangeTab,
  projectId,
  availableStorages,
  selectedStorageId,
  onSelectStorage,
  defaultStorage,
  isSavingStorage,
  onSaveDefaultStorage,
  onCreateStorage,
  onEditStorage,
  onToggleArchive,
  onDeleteStorage,
  t,
}: StorageSettingsSectionProps) => {
  const currentProjectId = Number(projectId);
  const hasProjectDefaultStorage = availableStorages.some(
    (storage) =>
      storage.default && Number(storage.projectId) === currentProjectId,
  );
  const defaultStorageProjectId = Number(defaultStorage?.projectId);
  const hasDefaultStorageWithProject =
    defaultStorage !== null && defaultStorageProjectId === currentProjectId;
  const isCurrentDefaultStorage = (storage: ObjectStorageResponseDto) => {
    if (defaultStorage) {
      return (
        String(storage.id) === String(defaultStorage.id) &&
        (!hasDefaultStorageWithProject ||
          Number(storage.projectId) === currentProjectId)
      );
    }

    return (
      storage.default &&
      (!hasProjectDefaultStorage ||
        Number(storage.projectId) === currentProjectId)
    );
  };

  const defaultTabContent = (
    <div className="mt-4">
      <p className="text-sm text-base-content/70 mb-4">
        {t.translations.SET_DEFAULT_UNMOUNTED_OBJECT_STORAGE}
      </p>

      <div className="form-control">
        <label className="label">
          <span className="label-text font-semibold mr-4 mb-6">
            {t.translations.DEFAULT_UNMOUNT_STORAGE}
          </span>
        </label>

        {availableStorages.length === 0 ? (
          <div className="alert alert-warning">
            <ExclamationTriangleIcon className="h-6 w-6" />
            <span>
              {t.translations.NO_STORAGE_LOCATIONS_AVAILABLE_CREATE_ONE}
            </span>
          </div>
        ) : (
          <>
            <select
              className="select select-bordered"
              value={selectedStorageId || ""}
              onChange={(e) => onSelectStorage(Number(e.target.value))}
            >
              <option value="" disabled>
                {t.translations.SELECT_STORAGE_LOCATION}
              </option>
              {availableStorages.map((storage) => (
                <option key={storage.id} value={storage.id}>
                  {storage.name}
                  {isCurrentDefaultStorage(storage)
                    ? t.translations.CURRENT_DEFAULT_SUFFIX
                    : ""}
                  {storage.isArchived ? t.translations.ARCHIVED_SUFFIX : ""}
                </option>
              ))}
            </select>

            {defaultStorage && (
              <div>
                <label className="label">
                  <span className="label-text-alt text-base-content/60">
                    {t.translations.DEFAULT_STORAGE_FOR_DATA_SOURCES_HELPER}
                  </span>
                </label>
                <div className="alert alert-info mt-3">
                  <InformationCircleIcon className="h-6 w-6" />
                  <span className="text-sm">
                    {t.translations.CURRENT_DEFAULT}{" "}
                    <strong>{defaultStorage.name}</strong>
                  </span>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );

  const manageTabContent = (
    <div className="mt-4">
      <div className="flex justify-between items-center mb-4">
        <p className="text-sm text-base-content/70">
          {t.translations.CREATE_EDIT_MANAGE_STORAGE_LOCATIONS}
        </p>
        <button className="btn btn-primary btn-sm" onClick={onCreateStorage}>
          <PlusIcon className="w-4 h-4" />
          {t.translations.CREATE_STORAGE}
        </button>
      </div>

      <div className="space-y-2 max-h-96 overflow-y-auto">
        {availableStorages.length === 0 ? (
          <div className="text-center py-8 text-base-content/60">
            <CircleStackIcon className="w-12 h-12 mx-auto mb-2 opacity-50" />
            <p>{t.translations.NO_STORAGES_CREATED_YET}</p>
            <p className="text-sm">
              {t.translations.CLICK_CREATE_STORAGE_TO_ADD_ONE}
            </p>
          </div>
        ) : (
          availableStorages.map((storage) => (
            <div
              key={storage.id}
              className={`card border bg-base-100 shadow-sm ${storage.isArchived ? "border-warning/30 opacity-60" : "border-base-300/50"}`}
            >
              <div className="card-body p-4">
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <div className="flex items-center gap-2 mb-1">
                      <h4 className="font-semibold">{storage.name}</h4>
                      {isCurrentDefaultStorage(storage) && (
                        <span className="badge badge-primary badge-sm">
                          {t.translations.DEFAULT_BADGE}
                        </span>
                      )}
                      {storage.isArchived && (
                        <span className="badge badge-warning badge-sm">
                          {t.translations.ARCHIVED_BADGE}
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-base-content/60">
                      {t.translations.STORAGE_TYPE_LABEL}{" "}
                      {storage.type || t.translations.NOT_AVAILABLE}
                    </p>
                    <p className="text-xs text-base-content/60">
                      {t.translations.LAST_UPDATED_LABEL}{" "}
                      {new Date(storage.lastUpdatedAt).toLocaleDateString()}
                    </p>
                  </div>

                  <div className="flex gap-1">
                    <button
                      className="btn btn-ghost btn-xs disabled:cursor-not-allowed"
                      onClick={() => onEditStorage(storage)}
                      disabled={storage.isArchived}
                      title={
                        storage.isArchived
                          ? t.translations.ARCHIVED_STORAGE_CANNOT_BE_EDITED
                          : t.translations.EDIT_STORAGE
                      }
                    >
                      <PencilIcon className="w-4 h-4" />
                    </button>
                    <button
                      className="btn btn-ghost btn-xs"
                      onClick={() => onToggleArchive(storage)}
                      title={
                        storage.isArchived
                          ? t.translations.UNARCHIVE
                          : t.translations.ARCHIVE
                      }
                    >
                      {storage.isArchived ? (
                        <ArchiveBoxXMarkIcon className="w-4 h-4" />
                      ) : (
                        <ArchiveBoxIcon className="w-4 h-4" />
                      )}
                    </button>
                    <button
                      className="btn btn-ghost btn-xs text-error"
                      onClick={() => onDeleteStorage(storage.id as number)}
                      title={t.translations.DELETE}
                    >
                      <TrashIcon className="w-4 h-4" />
                    </button>
                  </div>
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );

  const tabs = [
    { label: t.translations.DEFAULT_STORAGE_TAB, content: defaultTabContent },
    { label: t.translations.MANAGE_STORAGES_TAB, content: manageTabContent },
  ];
  const activeTabLabel =
    activeTab === "default"
      ? t.translations.DEFAULT_STORAGE_TAB
      : t.translations.MANAGE_STORAGES_TAB;

  return (
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body">
        <div className="flex justify-between">
          <div className="flex items-center gap-2 mb-4">
            <CircleStackIcon className="w-6 h-6 text-primary" />
            <h3 className="card-title text-lg">
              {t.translations.STORAGE_SETTINGS}
            </h3>
          </div>
          {selectedStorageId && selectedStorageId !== defaultStorage?.id && (
            <button
              className="btn btn-primary btn-sm mt-2 ml-4"
              onClick={onSaveDefaultStorage}
              disabled={isSavingStorage}
            >
              {isSavingStorage && (
                <span className="loading loading-spinner loading-xs" />
              )}
              {t.translations.SAVE_DEFAULT_STORAGE}
            </button>
          )}
        </div>

        <Tabs
          tabs={tabs}
          activeTab={activeTabLabel}
          onTabChange={(label) =>
            onChangeTab(
              label === t.translations.DEFAULT_STORAGE_TAB
                ? "default"
                : "manage",
            )
          }
          className="mb-4"
        />
      </div>
    </div>
  );
};

export default StorageSettingsSection;
