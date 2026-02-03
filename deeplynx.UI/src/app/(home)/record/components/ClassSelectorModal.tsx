import React, { useState, useEffect } from "react";
import { XMarkIcon, PlusIcon } from "@heroicons/react/24/outline";

interface ClassResponseDto {
  id: number;
  name: string;
  description?: string;
  projectId: number;
}

interface ClassSelectorModalProps {
  isOpen: boolean;
  onClose: () => void;
  currentClassId: number | null;
  projectId: number;
  onClassUpdate: (classId: number) => void;
  availableClasses: ClassResponseDto[];
  onCreateClass: (name: string, description?: string) => Promise<void>;
  isLoading?: boolean;
}

export default function ClassSelectorModal({
  isOpen,
  onClose,
  currentClassId,
  projectId,
  onClassUpdate,
  availableClasses,
  onCreateClass,
  isLoading = false,
}: ClassSelectorModalProps) {
  const [selectedClassId, setSelectedClassId] = useState<number | null>(
    currentClassId,
  );
  const [isCreatingNew, setIsCreatingNew] = useState(false);
  const [newClassName, setNewClassName] = useState("");
  const [newClassDescription, setNewClassDescription] = useState("");
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setSelectedClassId(currentClassId);
  }, [currentClassId]);

  useEffect(() => {
    if (!isOpen) {
      setIsCreatingNew(false);
      setNewClassName("");
      setNewClassDescription("");
    }
  }, [isOpen]);

  const handleSave = async () => {
    if (isCreatingNew) {
      if (!newClassName.trim()) {
        alert("Please enter a class name");
        return;
      }

      setIsSaving(true);
      try {
        await onCreateClass(newClassName, newClassDescription);
        setIsCreatingNew(false);
        setNewClassName("");
        setNewClassDescription("");
      } catch (error) {
        console.error("Error creating class:", error);
      } finally {
        setIsSaving(false);
      }
    } else if (selectedClassId && selectedClassId !== currentClassId) {
      onClassUpdate(selectedClassId);
      onClose();
    } else {
      onClose();
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />

      {/* Modal */}
      <div className="relative bg-base-100 rounded-lg shadow-xl w-full max-w-md mx-4 p-6">
        {/* Header */}
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-lg font-semibold">
            {isCreatingNew ? "Create New Class" : "Select Class"}
          </h3>
          <button onClick={onClose} className="btn btn-ghost btn-sm btn-circle">
            <XMarkIcon className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="space-y-4">
          {!isCreatingNew ? (
            <>
              {/* Class Selector */}
              <div className="form-control">
                <label className="label">
                  <span className="label-text">Choose a class</span>
                </label>
                <select
                  className="select select-bordered w-full"
                  value={selectedClassId || ""}
                  onChange={(e) => setSelectedClassId(Number(e.target.value))}
                  disabled={isLoading}
                >
                  <option value="">No class</option>
                  {availableClasses.map((cls) => (
                    <option key={cls.id} value={cls.id}>
                      {cls.name}
                    </option>
                  ))}
                </select>
              </div>

              {/* Create New Button */}
              <button
                onClick={() => setIsCreatingNew(true)}
                className="btn btn-outline btn-sm w-full"
                disabled={isLoading}
              >
                <PlusIcon className="w-4 h-4 mr-2" />
                Create New Class
              </button>
            </>
          ) : (
            <>
              {/* New Class Form */}
              <div className="form-control">
                <label className="label">
                  <span className="label-text">Class Name *</span>
                </label>
                <input
                  type="text"
                  className="input input-bordered w-full"
                  value={newClassName}
                  onChange={(e) => setNewClassName(e.target.value)}
                  placeholder="Enter class name"
                  disabled={isSaving}
                />
              </div>

              <div className="form-control">
                <label className="label">
                  <span className="label-text">Description</span>
                </label>
                <textarea
                  className="textarea textarea-bordered w-full"
                  value={newClassDescription}
                  onChange={(e) => setNewClassDescription(e.target.value)}
                  placeholder="Enter class description (optional)"
                  rows={3}
                  disabled={isSaving}
                />
              </div>

              <button
                onClick={() => setIsCreatingNew(false)}
                className="btn btn-ghost btn-sm w-full"
                disabled={isSaving}
              >
                ← Back to class selection
              </button>
            </>
          )}
        </div>

        {/* Footer */}
        <div className="flex gap-2 mt-6">
          <button
            onClick={onClose}
            className="btn btn-ghost flex-1"
            disabled={isSaving}
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            className="btn btn-primary flex-1"
            disabled={isSaving || (isCreatingNew && !newClassName.trim())}
          >
            {isSaving ? (
              <>
                <span className="loading loading-spinner loading-sm" />
                Saving...
              </>
            ) : isCreatingNew ? (
              "Create & Apply"
            ) : (
              "Update"
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
