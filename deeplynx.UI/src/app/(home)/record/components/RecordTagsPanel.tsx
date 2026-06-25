// src/app/(home)/record/components/RecordTagPanel.tsx

"use client";

import { XMarkIcon } from "@heroicons/react/24/outline";
import React, { useState } from "react";

import LabelButton from "@/app/(home)/record/components/LabelButton";
import Tabs from "@/app/(home)/components/Tabs";
import type {
  SensitivityLabelsDto,
  TagResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import TagButton from "./TagButton";

interface Props {
  // Data
  tags: TagResponseDto[];
  selectedTags: TagResponseDto[];
  selectedIds: string[];
  labels: SensitivityLabelsDto[];
  selectedLabels: SensitivityLabelsDto[];
  selectedLabelIds: string[];

  // Callbacks
  onSelectionChange: (selectedIds: string[]) => void;
  onRemoveTag: (tagId: number) => void;
  onLabelSelectionChange: (selectedIds: string[]) => void;
  onRemoveLabel: (labelId: number) => void;

  // Tag mutation helpers (for TagButton)
  projectId: number;
  recordId: number;
  setTags: React.Dispatch<React.SetStateAction<TagResponseDto[]>>;
  setSelectedTags: React.Dispatch<React.SetStateAction<TagResponseDto[]>>;
  setSelectedIds: React.Dispatch<React.SetStateAction<string[]>>;
  setLabels: React.Dispatch<React.SetStateAction<SensitivityLabelsDto[]>>;
  setSelectedLabels: React.Dispatch<
    React.SetStateAction<SensitivityLabelsDto[]>
  >;
  setSelectedLabelIds: React.Dispatch<React.SetStateAction<string[]>>;

  // Translations
  title: string;
}

const RecordTagsPanel: React.FC<Props> = ({
  tags,
  selectedTags,
  selectedIds,
  labels,
  selectedLabels,
  selectedLabelIds,
  onSelectionChange,
  onRemoveTag,
  onLabelSelectionChange,
  onRemoveLabel,
  projectId,
  recordId,
  setTags,
  setSelectedTags,
  setSelectedIds,
  setLabels,
  setSelectedLabels,
  setSelectedLabelIds,
  title,
}) => {
  const { t } = useLanguage();
  const [activeTab, setActiveTab] = useState(t.translations.SENSITIVITY_LABELS);

  const tagContent = (
    <>
      <div className="flex justify-end mb-3 mt-3">
        <TagButton
          tags={tags}
          onSelectionChange={onSelectionChange}
          projectId={projectId}
          recordId={recordId}
          selectedIds={selectedIds}
          setSelectedIds={setSelectedIds}
          setTags={setTags}
          setSelectedTags={setSelectedTags}
        />
      </div>

      <div className="space-y-2 max-h-48 overflow-y-auto rounded-lg px-3 py-2">
        {selectedTags.length === 0 ? (
          <div className="py-4 text-center text-xs text-base-content/60">
            {t.translations.NO_TAGS_ATTACHED_TO_RECORD}{" "}
            {t.translations.USE_SELECTOR_TO_ADD_TAGS}
          </div>
        ) : (
          selectedTags.map((tag) => (
            <div
              key={tag.id}
              className="flex items-center justify-between gap-3 bg-base-200/60 hover:bg-base-200 rounded-lg px-3 py-1.5"
            >
              <div className="flex items-center gap-2">
                <span className="badge badge-secondary badge-outline badge-sm">
                  {tag.name}
                </span>
              </div>
              {tag.id != null && (
                <button
                  type="button"
                  className="btn btn-ghost btn-xs text-error gap-1"
                  onClick={() => onRemoveTag(tag.id as number)}
                >
                  <XMarkIcon className="w-3 h-3" />
                  <span className="hidden sm:inline">
                    {t.translations.REMOVE}
                  </span>
                </button>
              )}
            </div>
          ))
        )}
      </div>
    </>
  );

  const labelContent = (
    <>
      <div className="flex justify-end mb-3 mt-3">
        <LabelButton
          labels={labels}
          onSelectionChange={onLabelSelectionChange}
          projectId={projectId}
          recordId={recordId}
          selectedIds={selectedLabelIds}
          setSelectedIds={setSelectedLabelIds}
          setLabels={setLabels}
          setSelectedLabels={setSelectedLabels}
        />
      </div>

      <div className="space-y-2 max-h-48 overflow-y-auto rounded-lg px-3 py-2">
        {selectedLabels.length === 0 ? (
          <div className="py-4 text-center text-xs text-base-content/60">
            {t.translations.NO_LABELS_ATTACHED_TO_RECORD}{" "}
            {t.translations.USE_SELECTOR_TO_ADD_LABELS}
          </div>
        ) : (
          selectedLabels.map((label) => (
            <div
              key={label.id}
              className="flex items-center justify-between gap-3 bg-base-200/60 hover:bg-base-200 rounded-lg px-3 py-1.5"
            >
              <div className="flex items-center gap-2">
                <span className="badge badge-secondary badge-outline badge-sm">
                  {label.name}
                </span>
              </div>
              {label.id != null && (
                <button
                  type="button"
                  className="btn btn-ghost btn-xs text-error gap-1"
                  onClick={() => onRemoveLabel(label.id as number)}
                >
                  <XMarkIcon className="w-3 h-3" />
                  <span className="hidden sm:inline">
                    {t.translations.REMOVE}
                  </span>
                </button>
              )}
            </div>
          ))
        )}
      </div>
    </>
  );

  const tabs = [
    { label: t.translations.SENSITIVITY_LABELS, content: labelContent },
    {
      label: t.translations.TAGS,
      content: tagContent,
    },
  ];

  return (
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body p-3 sm:p-6">
        <Tabs tabs={tabs} activeTab={activeTab} onTabChange={setActiveTab} />
      </div>
    </div>
  );
};

export default RecordTagsPanel;
