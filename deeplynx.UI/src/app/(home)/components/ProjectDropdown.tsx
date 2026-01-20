"use client";

import { useLanguage } from "@/app/contexts/Language";
import { ChevronDownIcon, ChevronUpIcon } from "@heroicons/react/24/outline";
import React, { useEffect, useMemo, useRef, useState } from "react";

interface ProjectDropdownProps {
  projects: { id: string; name: string }[];
  onSelectionChange?: (selected: string[]) => void;
  defaultSelected?: string[];
}

const ProjectDropdown: React.FC<ProjectDropdownProps> = ({
  projects,
  onSelectionChange,
  defaultSelected,
}) => {
  const { t } = useLanguage();
  const [isOpen, setIsOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const allIds = useMemo(() => projects.map((p) => p.id), [projects]);
  const defaultToken = useMemo(
    () => (defaultSelected ?? []).map(String).join("|"),
    [defaultSelected]
  );

  // Apply defaultSelected when loaded / when it changes
  useEffect(() => {
    if (!projects.length) return;
    if (defaultToken.length) {
      setSelectedIds((defaultSelected ?? []).map(String));
    } else {
      setSelectedIds(["ALL"]);
    }
  }, [projects.length, defaultToken, defaultSelected]);

  //  Notify parent anytime selectedIds changes (and projects exists)
  useEffect(() => {
    if (!projects.length) return;
    const isAll = selectedIds.includes("ALL");
    onSelectionChange?.(isAll ? allIds : selectedIds);
  }, [selectedIds, projects.length, allIds, onSelectionChange]);

  // 🧹 Close dropdown on outside click
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

  const toggleProject = (id: string) => {
    let newSelection: string[];
    if (id === "ALL") {
      newSelection = ["ALL"];
    } else {
      newSelection = selectedIds.includes(id)
        ? selectedIds.filter((sid) => sid !== id)
        : [id, ...selectedIds.filter((sid) => sid !== "ALL")];

      if (newSelection.length === 0) newSelection = ["ALL"];
    }
    setSelectedIds(newSelection);
  };

  const filteredProjects = useMemo(
    () =>
      projects.filter((p) =>
        p.name.toLowerCase().includes(searchTerm.toLowerCase())
      ),
    [projects, searchTerm]
  );

  const selectedLabel = useMemo(() => {
    if (selectedIds.includes("ALL")) return "All Your Projects";
    if (selectedIds.length === 1) {
      const project = projects.find((p) => p.id === selectedIds[0]);
      return project?.name || "1 project selected";
    }
    return `${selectedIds.length} projects selected`;
  }, [selectedIds, projects]);

  return (
    <div
      className="relative inline-block text-left min-w-sm text-base-content/80"
      ref={dropdownRef}
    >
      <button
        className="flex items-center gap-1 text-md"
        onClick={() => setIsOpen((o) => !o)}
        type="button"
      >
        {selectedLabel}{" "}
        {selectedLabel === "All your Projects" && `(${projects.length})`}
        {isOpen ? (
          <ChevronUpIcon className="w-5 h-5 ml-1" />
        ) : (
          <ChevronDownIcon className="w-5 h-5 ml-1" />
        )}
      </button>

      {isOpen && (
        <div className="absolute z-10 mt-2 w-full bg-base-100 shadow shadow-dynamic-shadow rounded-box p-4 max-h-80 overflow-auto">
          <input
            type="text"
            placeholder="Search"
            className="input input-bordered w-full mb-4"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />

          <div className="space-y-2">
            <label className="label cursor-pointer justify-start gap-2">
              <input
                type="checkbox"
                className="checkbox text-white checked:bg-dynamic-blue border-dynamic-blue"
                checked={selectedIds.includes("ALL")}
                onChange={() => toggleProject("ALL")}
              />
              <span className="label-text text-base-content">
                {t.translations.ALL_YOUR_PROJECTS}
              </span>
            </label>
          </div>

          <div className="flex flex-col gap-2">
            {filteredProjects.map((project) => (
              <label
                key={project.id}
                className="label cursor-pointer justify-start gap-2 text-base-content"
              >
                <input
                  type="checkbox"
                  className="checkbox text-white checked:bg-dynamic-blue border-dynamic-blue"
                  checked={selectedIds.includes(project.id)}
                  onChange={() => toggleProject(project.id)}
                />
                <span className="label-text">{project.name}</span>
              </label>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};

export default ProjectDropdown;
