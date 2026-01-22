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
              No storage locations available. Create one in the "Manage
              Storages" tab.
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
                Select a storage location
              </option>
              {availableStorages.map((storage) => (
                <option key={storage.id} value={storage.id}>
                  {storage.name}
                  {storage.default ? " (Current Default)" : ""}
                  {storage.isArchived ? " [Archived]" : ""}
                </option>
              ))}
            </select>

            {defaultStorage && (
              <div>
                <label className="label">
                  <span className="label-text-alt text-base-content/60">
                    This will be the default storage for data sources in this
                    project
                  </span>
                </label>
                <div className="alert alert-info mt-3">
                  <InformationCircleIcon className="h-6 w-6" />
                  <span className="text-sm">
                    Current default: <strong>{defaultStorage.name}</strong>
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
          Create, edit, and manage your storage locations
        </p>
        <button className="btn btn-primary btn-sm" onClick={onCreateStorage}>
          <PlusIcon className="w-4 h-4" />
          Create Storage
        </button>
      </div>

      <div className="space-y-2 max-h-96 overflow-y-auto">
        {availableStorages.length === 0 ? (
          <div className="text-center py-8 text-base-content/60">
            <CircleStackIcon className="w-12 h-12 mx-auto mb-2 opacity-50" />
            <p>No storages created yet</p>
            <p className="text-sm">Click "Create Storage" to add one</p>
          </div>
        ) : (
          availableStorages.map((storage) => (
            <div
              key={storage.id}
              className={`card bg-base-200 border ${storage.isArchived ? "border-warning/30 opacity-60" : "border-base-300"}`}
            >
              <div className="card-body p-4">
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <div className="flex items-center gap-2 mb-1">
                      <h4 className="font-semibold">{storage.name}</h4>
                      {storage.default && (
                        <span className="badge badge-primary badge-sm">
                          Default
                        </span>
                      )}
                      {storage.isArchived && (
                        <span className="badge badge-warning badge-sm">
                          Archived
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-base-content/60">
                      Type: {storage.type || "N/A"}
                    </p>
                    <p className="text-xs text-base-content/60">
                      Last updated:{" "}
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
                          ? "Archived storages cannot be edited"
                          : "Edit"
                      }
                    >
                      <PencilIcon className="w-4 h-4" />
                    </button>
                    <button
                      className="btn btn-ghost btn-xs"
                      onClick={() => onToggleArchive(storage)}
                      title={storage.isArchived ? "Unarchive" : "Archive"}
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
                      title="Delete"
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
    { label: "Default Storage", content: defaultTabContent },
    { label: "Manage Storages", content: manageTabContent },
  ];
  const activeTabLabel =
    activeTab === "default" ? "Default Storage" : "Manage Storages";

  return (
    <div className="card bg-base-100 border border-primary/40 shadow-sm">
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
              Save Default Storage
            </button>
          )}
        </div>

        <Tabs
          tabs={tabs}
          activeTab={activeTabLabel}
          onTabChange={(label) =>
            onChangeTab(label === "Default Storage" ? "default" : "manage")
          }
          className="mb-4"
        />
      </div>
    </div>
  );
};

export default StorageSettingsSection;
