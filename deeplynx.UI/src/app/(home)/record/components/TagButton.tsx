import React, { useEffect, useRef, useState } from "react";
import { PlusIcon } from "@heroicons/react/24/outline";
import { TagResponseDto } from "../../types/responseDTOs";
import AddTagModal from "@/app/(home)/components/AddTagModal";
import { useLanguage } from "@/app/contexts/Language";
import toast from "react-hot-toast";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  unattachTagFromRecord,
  attachTagToRecord,
} from "@/app/lib/client_service/record_services.client";

interface TagButtonProps {
  tags: TagResponseDto[];
  onSelectionChange?: (selected: string[]) => void;
  projectId: number;
  recordId: number;
  selectedIds: string[];
  setSelectedIds: (ids: string[]) => void;
  setTags: React.Dispatch<React.SetStateAction<TagResponseDto[]>>;
  setSelectedTags: React.Dispatch<React.SetStateAction<TagResponseDto[]>>;
}

const TagButton: React.FC<TagButtonProps> = ({
  tags,
  onSelectionChange,
  projectId,
  recordId,
  selectedIds,
  setTags,
  setSelectedTags,
}) => {
  const [isOpen, setIsOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState("");
  const [tempSelectedIds, setTempSelectedIds] = useState<string[]>(selectedIds);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const longestNameRef = useRef<HTMLSpanElement>(null);
  const [isTagModalOpen, setIsTagModalOpen] = useState(false);
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();

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

  const toggleTag = async (id: string) => {
    let newSelectionIds: string[];

    if (tempSelectedIds.map(String).includes(id)) {
      newSelectionIds = tempSelectedIds
        .map(String)
        .filter((selectedId) => selectedId !== id);
      await unattachTagFromRecord(
        organization?.organizationId as number,
        projectId,
        recordId,
        Number(id),
      );
    } else {
      newSelectionIds = [...tempSelectedIds.map(String), id];
      try {
        await attachTagToRecord(
          organization?.organizationId as number,
          projectId,
          recordId,
          Number(id),
        );
      } catch (error) {
        console.error("Error attaching tag to record:", error);
      }
    }
    setTempSelectedIds(newSelectionIds);

    if (onSelectionChange) {
      onSelectionChange(newSelectionIds);
    }
  };

  const handleTagCreated = async (newTag: TagResponseDto) => {
    // Add the new tag to the tags list
    setTags((prevTags) => [...prevTags, newTag]);

    // Automatically attach it to the record
    try {
      await attachTagToRecord(
        organization?.organizationId as number,
        projectId,
        recordId,
        Number(newTag.id),
      );

      // Update selection state
      const newSelectionIds = [
        ...tempSelectedIds.map(String),
        newTag.id.toString(),
      ];
      setTempSelectedIds(newSelectionIds);

      // Directly update selectedTags in parent
      setSelectedTags((prevSelectedTags) => [...prevSelectedTags, newTag]);

      toast.success(
        `${t.translations.TAG_} "${newTag.name}" ${t.translations.CREATED_AND_ATTACHED}`,
      );
    } catch (error) {
      console.error("Error attaching new tag:", error);
      toast.error(
        `${t.translations.TAG_} ${t.translations.CREATED_BUT_FAILED_TO_ATTACH}`,
      );
    }
  };

  const filteredTags = tags.filter((t) =>
    t.name.toLowerCase().includes(searchTerm.toLowerCase()),
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
            {filteredTags.map((tag) => (
              <label
                key={tag.id}
                className="label cursor-pointer justify-start gap-2"
              >
                <input
                  type="checkbox"
                  className="checkbox checkbox-primary"
                  checked={tempSelectedIds
                    .map(String)
                    .includes(tag.id.toString())}
                  onChange={() => toggleTag(tag.id.toString())}
                />
                <span
                  className="label-text whitespace-nowrap"
                  ref={tag.id === filteredTags[0]?.id ? longestNameRef : null}
                >
                  {tag.name}
                </span>
              </label>
            ))}
          </div>
          <div className="flex flex-row items-center gap-2 my-4">
            <button
              onClick={() => setIsTagModalOpen(true)}
              className="btn btn-primary btn-sm flex-1 sm:flex-initial"
            >
              <PlusIcon className="size-5" />
              <span>{t.translations.TAG}</span>
            </button>
          </div>
        </div>
      )}
      <AddTagModal
        projectId={projectId}
        isOpen={isTagModalOpen}
        onClose={() => {
          setIsTagModalOpen(false);
        }}
        onTagCreated={handleTagCreated}
      />
    </div>
  );
};

export default TagButton;
