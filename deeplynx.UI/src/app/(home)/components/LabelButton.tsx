import React, { useEffect, useRef, useState } from "react";
import { PlusIcon } from "@heroicons/react/24/outline";
import { SensitivityLabelsDto } from "../types/responseDTOs";
import AddLabelModal from "./AddLabelModal";
import toast from "react-hot-toast";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useLanguage } from "@/app/contexts/Language";
import {
  attachSensitivityLabelToRecord,
  unattachSensitivityLabelFromRecord,
} from "@/app/lib/client_service/record_services.client";

interface LabelButtonProps {
  labels: SensitivityLabelsDto[];
  onSelectionChange?: (selected: string[]) => void;
  projectId: number;
  recordId: number;
  selectedIds: string[];
  setSelectedIds: (ids: string[]) => void;
  setLabels: React.Dispatch<React.SetStateAction<SensitivityLabelsDto[]>>;
  setSelectedLabels: React.Dispatch<
    React.SetStateAction<SensitivityLabelsDto[]>
  >;
}

const LabelButton: React.FC<LabelButtonProps> = ({
  labels,
  onSelectionChange,
  projectId,
  recordId,
  selectedIds,
  setLabels,
  setSelectedLabels,
}) => {
  const [isOpen, setIsOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState("");
  const [tempSelectedIds, setTempSelectedIds] = useState<string[]>(selectedIds);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const longestNameRef = useRef<HTMLSpanElement>(null);
  const [isLabelModalOpen, setIsLabelModalOpen] = useState(false);
  const { organization } = useOrganizationSession();
  const { t } = useLanguage();

  useEffect(() => {
    setTempSelectedIds(selectedIds);
  }, [selectedIds]);

  useEffect(() => {
    if (longestNameRef.current) {
      const longestNameWidth = longestNameRef.current.offsetWidth;
      if (dropdownRef.current) {
        dropdownRef.current.style.minWidth = `${longestNameWidth + 40}px`;
      }
    }
  }, [isOpen, tempSelectedIds]);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (
        dropdownRef.current &&
        !dropdownRef.current.contains(e.target as Node)
      ) {
        setIsOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const toggleLabel = async (id: string) => {
    let newSelectionIds: string[];

    if (tempSelectedIds.map(String).includes(id)) {
      newSelectionIds = tempSelectedIds
        .map(String)
        .filter((selectedId) => selectedId !== id);
      await unattachSensitivityLabelFromRecord(
        organization?.organizationId as number,
        projectId,
        recordId,
        Number(id),
      );
    } else {
      newSelectionIds = [...tempSelectedIds.map(String), id];
      try {
        await attachSensitivityLabelToRecord(
          organization?.organizationId as number,
          projectId,
          recordId,
          Number(id),
        );
      } catch (error) {
        console.error("Error attaching label to record:", error);
      }
    }
    setTempSelectedIds(newSelectionIds);

    if (onSelectionChange) {
      onSelectionChange(newSelectionIds);
    }
  };

  const handleLabelCreated = async (newLabel: SensitivityLabelsDto) => {
    setLabels((prevLabels) => [...prevLabels, newLabel]);

    try {
      await attachSensitivityLabelToRecord(
        organization?.organizationId as number,
        projectId,
        recordId,
        Number(newLabel.id),
      );

      const newSelectionIds = [
        ...tempSelectedIds.map(String),
        newLabel.id.toString(),
      ];
      setTempSelectedIds(newSelectionIds);
      setSelectedLabels((prevSelectedLabels) => [...prevSelectedLabels, newLabel]);

      toast.success(
        `${t.translations.LABEL} "${newLabel.name}" ${t.translations.LABEL_CREATED_AND_ATTACHED}`,
      );
    } catch (error) {
      console.error("Error attaching new label:", error);
      toast.error(
        `${t.translations.LABEL} ${t.translations.LABEL_CREATED_BUT_FAILED_TO_ATTACH}`,
      );
    }
  };

  const filteredLabels = labels.filter((l) =>
    l.name.toLowerCase().includes(searchTerm.toLowerCase()),
  );

  return (
    <div className="relative inline-flex text-left text-accent-content">
      <button
        className="flex items-center justify-center w-7 h-7 rounded-full bg-primary text-white cursor-pointer"
        onClick={() => setIsOpen(!isOpen)}
      >
        <PlusIcon className="size-6" />
      </button>

      {isOpen && (
        <div
          className="absolute z-50 mt-2 right-0 bg-base-100 shadow-lg rounded-box p-4 max-h-80"
          ref={dropdownRef}
          style={{ minWidth: "200px" }}
        >
          <input
            type="text"
            placeholder={t.translations.SEARCH}
            className="input input-bordered w-full mb-4"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
          <div className="flex flex-col gap-2 overflow-y-auto max-h-48">
            {filteredLabels.map((label) => (
              <label
                key={label.id}
                className="label cursor-pointer justify-start gap-2"
              >
                <input
                  type="checkbox"
                  className="checkbox checkbox-primary"
                  checked={tempSelectedIds
                    .map(String)
                    .includes(label.id.toString())}
                  onChange={() => toggleLabel(label.id.toString())}
                />
                <span
                  className="label-text whitespace-nowrap"
                  ref={label.id === filteredLabels[0]?.id ? longestNameRef : null}
                >
                  {label.name}
                </span>
              </label>
            ))}
          </div>
          <div className="flex flex-row items-center gap-2 my-4">
            <button
              onClick={() => setIsLabelModalOpen(true)}
              className="btn btn-primary btn-sm flex-1 sm:flex-initial"
            >
              <PlusIcon className="size-5" />
              <span>{t.translations.LABEL}</span>
            </button>
          </div>
        </div>
      )}
      <AddLabelModal
        projectId={projectId}
        isOpen={isLabelModalOpen}
        onClose={() => {
          setIsLabelModalOpen(false);
        }}
        onLabelCreated={handleLabelCreated}
      />
    </div>
  );
};

export default LabelButton;
