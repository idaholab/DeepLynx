// src/app/(home)/project_management/[id]/settings/components/ProjectSettingsTable.tsx
"use client";

import React, { useState } from "react";
import { CheckCircleIcon, XCircleIcon, PencilIcon } from "@heroicons/react/24/outline";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";

// Table Shape
interface ProjectPropertyRow {
  key: string;
  value: React.ReactNode;
  editable?: boolean;
  onEdit?: (newValue: string) => void;
  maxCharacters?: number;
}

// Type Definitions
interface Props {
  projectRows: ProjectPropertyRow[];
}

const ProjectSettingsTable: React.FC<Props> = (projectInfo) => {
  const { project, setProject } = useProjectSession();
  // State
  const [editingKey, setEditingKey] = useState<string>("");
  const [editValue, setEditingValue] = useState<string>("");

  // Helper Functions
  const handleEdit = (key: string, currentValue: string) => {
    setEditingKey(key);
    setEditingValue(currentValue);
  }

  const handleSave = async (row: ProjectPropertyRow) => {
    row.onEdit?.(editValue);
    if (editingKey == "Project Name ") {
      if (!project) return;
      setProject({
        ...project,
        projectName: editValue,
      });
    }
    setEditingKey("");
  }

  const handleCancel = () => {
    setEditingKey("");
    setEditingValue("");
  }

  const renderRow = (
    row: ProjectPropertyRow,
    index: number,
    depth: number = 0,
    isLast: boolean = false,
    parentIsLast: boolean[] = [],
  ) => {
    return (
      <React.Fragment key={index}>
        <div className={`grid grid-cols-12 border-b border-base-300`}>
          <div className="col-span-4 p-3 font-medium text-base-content text-sm bg-base-200 border-r border-base-300 flex items-center relative">
            {/* Tree branch visualization */}
            {depth > 0 && (
              <div className="absolute left-0 top-0 bottom-0 flex">
                {parentIsLast.map((parentLast, i) => (
                  <div key={i} className="relative" style={{ width: "1.5rem" }}>
                    {!parentLast && (
                      <div className="absolute left-1/2 top-0 bottom-0 w-px bg-base-300" />
                    )}
                  </div>
                ))}
                <div className="relative" style={{ width: "1.5rem" }}>
                  {/* Vertical line */}
                  {!isLast && (
                    <div className="absolute left-1/2 top-0 bottom-0 w-px bg-base-300" />
                  )}
                  {/* Horizontal line */}
                  <div
                    className="absolute top-1/2 left-1/2 w-2 h-px bg-base-300"
                    style={{ transform: "translateY(-50%)" }}
                  />
                  {/* Corner for last item */}
                  {isLast && (
                    <div
                      className="absolute left-1/2 top-0 w-px bg-base-300"
                      style={{ height: "50%" }}
                    />
                  )}
                </div>
              </div>
            )}

            <div
              className="flex items-center"
              style={{
                paddingLeft: depth > 0 ? `${depth * 1.5 + 0.5}rem` : "0",
              }}
            >
              <span className="truncate ml-2">{row.key}</span>
            </div>
          </div>
          <div className="col-span-7 p-3 text-sm text-base-content break-words">
            {editingKey === row.key ? (
                  <input
                    type="text"
                    value={editValue}
                    onChange={(e) => setEditingValue(e.target.value)}
                    maxLength={row.maxCharacters}
                    className="input input-sm input-bordered w-full"
                  />
                  ) : (
                    <td>{row.value}</td>
                )}
          </div>
          <div className="col-span-1 p-3 flex justify-center items-center gap-1">
            {row.editable && editingKey !== row.key && (
              <PencilIcon
                className="text-primary hover:text-primary-focus size-6 cursor-pointer transition-colors"
                onClick={() => handleEdit(row.key, String(row.value))}
              />
            )}
            {editingKey === row.key && (
              <>
                <button>
                  <CheckCircleIcon
                    className="text-success hover:text-success-content size-6 cursor-pointer transition-colors"
                    onClick={() => handleSave(row)}
                  />
                </button>
                <button>
                  <XCircleIcon
                    className="text-error hover:text-error-content size-6 cursor-pointer transition-colors"
                    onClick={handleCancel}
                  />
                </button>
              </>
            )}
          </div>
        </div>
      </React.Fragment>
    );
  };

  // Build table
  return (
    <div className="border border-base-300 rounded-lg overflow-hidden bg-base-100">
      {projectInfo.projectRows.map((row, index) =>
        renderRow(row, index, 0, index === projectInfo.projectRows.length - 1, []),
      )}
    </div>
  );
}

export default ProjectSettingsTable;