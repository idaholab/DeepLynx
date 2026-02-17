import React from "react";
import {
  ShieldCheckIcon,
  LockClosedIcon,
  LockOpenIcon,
  MagnifyingGlassIcon,
  InformationCircleIcon,
} from "@heroicons/react/24/outline";
import type { SensitivityLabelsDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";

interface Props {
  labels: SensitivityLabelsDto[];
  labelsLocked: boolean;
  labelsLoading: boolean;
  labelsError: string | null;
  filteredLabels: SensitivityLabelsDto[];
  labelSearch: string;
  setLabelSearch: (value: string) => void;
  filteredCount: number;
  labelCount: number;
  orgId?: number;
  archivingLabelId: number | null;
  onToggleLock: () => void;
  onCreateLabel: () => void;
  onEditLabel: (id: number) => void;
  onArchiveClick: (label: SensitivityLabelsDto) => void;
}

const SecurityLabelsOrg: React.FC<Props> = ({
  labelsLocked,
  labelsLoading,
  labelsError,
  filteredLabels,
  labelSearch,
  setLabelSearch,
  filteredCount,
  labelCount,
  orgId,
  archivingLabelId,
  onToggleLock,
  onCreateLabel,
  onEditLabel,
  onArchiveClick,
}) => {
  const { t } = useLanguage();
  return (
    <div className="card bg-base-100 shadow-lg">
      <div className="card-body">
        {/* Header + Controls */}
        <div className="flex items-start justify-between gap-4 mb-3">
          <div className="flex-1">
            <div className="flex items-center gap-2">
              <ShieldCheckIcon className="w-5 h-5 text-secondary" />
              <h3 className="font-semibold text-base">
                {t.translations.ORGANIZATION_SECURITY_LABELS}
              </h3>
            </div>
            <p className="text-xs text-base-content/70 mt-1 max-w-md">
              {t.translations.ORGANIZATION_SECURITY_LABELS_DESCRIPTION}
            </p>
          </div>

          <div className="flex flex-col items-end gap-2">
            {/* Search input */}
            <div className="form-control w-40">
              <div className="input input-xs input-bordered flex items-center gap-1 px-2">
                <MagnifyingGlassIcon className="w-3 h-3 text-base-content/60" />
                <input
                  type="text"
                  className="grow text-[0.7rem] bg-transparent focus:outline-none"
                  placeholder={t.translations.SEARCH_LABELS}
                  value={labelSearch}
                  onChange={(e) => setLabelSearch(e.target.value)}
                />
              </div>
            </div>

            {/* Add button */}
            <button
              type="button"
              className="btn btn-primary btn-xs gap-1"
              onClick={onCreateLabel}
              disabled={labelsLocked || !orgId}
              title={
                !orgId
                  ? t.translations.NO_ORG_SELECTED
                  : labelsLocked
                    ? t.translations.LABELS_LOCKED_AT_ORG_LEVEL_SHORT
                    : t.translations.CREATE_NEW_LABEL
              }
            >
              + {t.translations.NEW_LABEL}
            </button>
          </div>
        </div>

        {/* n of m line */}
        <div className="flex justify-between items-center mb-3 text-[0.7rem] text-base-content/60">
          <span>
            {t.translations.SHOWING}{" "}
            <span className="font-semibold">{filteredCount}</span>{" "}
            {t.translations.OF}{" "}
            <span className="font-semibold">{labelCount}</span>{" "}
            {t.translations.LABELS_LOWER}
          </span>
          {labelSearch.trim() && (
            <span className="italic">
              {t.translations.FILTERED_BY}
              <span className="font-medium break-all">{labelSearch}</span>
            </span>
          )}
        </div>

        {/* Labels list */}
        <div className="space-y-2 max-h-72 overflow-y-auto">
          {labelsLoading ? (
            <div className="py-6 text-center text-xs text-base-content/60">
              {t.translations.LOADING_ORGANIZATION_LABELS}
            </div>
          ) : labelsError ? (
            <div className="py-6 text-center text-xs text-error">
              {labelsError}
            </div>
          ) : filteredLabels.length === 0 ? (
            <div className="py-6 text-center text-xs text-base-content/60 border border-dashed border-base-300 rounded-lg">
              {labelSearch.trim()
                ? t.translations.NO_LABELS_MATCH_SEARCH
                : t.translations.NO_LABELS_DEFINED_HELP}
            </div>
          ) : (
            filteredLabels.map((label) => (
              <div
                key={label.id}
                className="flex items-center justify-between bg-base-200/70 hover:bg-base-300/80 transition rounded-lg px-3 py-2"
              >
                <div className="flex items-center gap-2">
                  <span className="badge badge-secondary badge-outline badge-sm">
                    {label.name}
                  </span>
                </div>
                <div className="flex items-center gap-1">
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs"
                    onClick={() => onEditLabel(label.id)}
                    disabled={labelsLocked}
                    title={
                      labelsLocked
                        ? t.translations.LABELS_ARE_LOCKED
                        : t.translations.EDIT
                    }
                  >
                    {t.translations.EDIT}
                  </button>
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs text-error"
                    onClick={() => onArchiveClick(label)}
                    disabled={labelsLocked || archivingLabelId === label.id}
                    title={
                      labelsLocked
                        ? t.translations.LABELS_ARE_LOCKED
                        : t.translations.ARCHIVE_SOFT_DELETE_LABEL
                    }
                  >
                    {archivingLabelId === label.id
                      ? t.translations.ARCHIVING
                      : t.translations.DELETE}
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
};

export default SecurityLabelsOrg;
