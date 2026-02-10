// src/app/(home)/record/components/ClassSelectorModal.tsx

import { useLanguage } from "@/app/contexts/Language";
import { ClassResponseDto } from "@/app/(home)/types/responseDTOs";
import { PlusIcon, XMarkIcon } from "@heroicons/react/24/outline";
import { useEffect, useState } from "react";

interface ClassSelectorModalProps {
  isOpen: boolean;
  onClose: () => void;
  currentClassId?: number | null;
  onClassUpdate: (classId: number) => void;
  availableClasses: ClassResponseDto[];
  onCreateClass: (name: string, description?: string) => Promise<void>;
  isLoading?: boolean;
}

export default function ClassSelectorModal({
  isOpen,
  onClose,
  currentClassId,
  onClassUpdate,
  availableClasses,
  onCreateClass,
  isLoading = false,
}: ClassSelectorModalProps) {
  const { t } = useLanguage();
  const [selectedClassId, setSelectedClassId] = useState<
    number | null | undefined
  >(currentClassId);
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
            {isCreatingNew
              ? t.translations.CREATE_NEW_CLASS
              : t.translations.SELECT_CLASS}
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
                  <span className="label-text">
                    {t.translations.CHOOSE_A_CLASS}
                  </span>
                </label>
                <select
                  className="select select-bordered w-full"
                  value={selectedClassId || ""}
                  onChange={(e) => setSelectedClassId(Number(e.target.value))}
                  disabled={isLoading}
                >
                  <option value="">{t.translations.NO_CLASS}</option>
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
                {t.translations.CREATE_NEW_CLASS}
              </button>
            </>
          ) : (
            <>
              {/* New Class Form */}
              <div className="form-control">
                <label className="label">
                  <span className="label-text">
                    {t.translations.CLASS_NAME_REQUIRED}
                  </span>
                </label>
                <input
                  type="text"
                  className="input input-bordered w-full"
                  value={newClassName}
                  onChange={(e) => setNewClassName(e.target.value)}
                  placeholder={t.translations.ENTER_CLASS_NAME}
                  disabled={isSaving}
                />
              </div>

              <div className="form-control">
                <label className="label">
                  <span className="label-text">
                    {t.translations.DESCRIPTION}
                  </span>
                </label>
                <textarea
                  className="textarea textarea-bordered w-full"
                  value={newClassDescription}
                  onChange={(e) => setNewClassDescription(e.target.value)}
                  placeholder={t.translations.ENTER_CLASS_DESCRIPTION_OPTIONAL}
                  rows={3}
                  disabled={isSaving}
                />
              </div>

              <button
                onClick={() => setIsCreatingNew(false)}
                className="btn btn-ghost btn-sm w-full"
                disabled={isSaving}
              >
                {t.translations.BACK_TO_CLASS_SELECTION}
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
            {t.translations.CANCEL}
          </button>
          <button
            onClick={handleSave}
            className="btn btn-primary flex-1"
            disabled={isSaving || (isCreatingNew && !newClassName.trim())}
          >
            {isSaving ? (
              <>
                <span className="loading loading-spinner loading-sm" />
                {t.translations.SAVING}
              </>
            ) : isCreatingNew ? (
              t.translations.CREATE_AND_APPLY
            ) : (
              t.translations.UPDATE
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
