"use client";

import React, { useState } from "react";
import toast from "react-hot-toast";

import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";

import { SensitivityLabelsDto } from "@/app/(home)/types/responseDTOs";
import { createSensitivityLabelProject } from "@/app/lib/client_service/sensitivity_labels_services.client";

type Props = {
  isOpen: boolean;
  onClose: () => void;
  projectId: number;
  onLabelCreated?: (newLabel: SensitivityLabelsDto) => void;
};

const AddLabelModal: React.FC<Props> = ({
  isOpen,
  onClose,
  projectId,
  onLabelCreated,
}) => {
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const resetForm = () => {
    setName("");
    setDescription("");
    setIsSubmitting(false);
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    setIsSubmitting(true);

    if (!organization?.organizationId) {
      toast.error(t.translations.NO_ORG_SELECTED);
      setIsSubmitting(false);
      return;
    }

    try {
      const newLabel = await createSensitivityLabelProject(projectId, {
        name,
        description: description.trim() || null,
      });

      if (onLabelCreated && newLabel) {
        onLabelCreated(newLabel);
      }

      toast.success(t.translations.LABEL_CREATED);
      resetForm();
      onClose();
    } catch (error) {
      console.error("Error creating label:", error);
      toast.error(t.translations.FAILED_TO_CREATE_LABEL);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <dialog className={`modal ${isOpen ? "modal-open" : ""}`}>
      <div className="modal-box">
        <h3 className="text-base-content font-bold text-lg mb-4">
          {t.translations.ADD_A_LABEL}
        </h3>

        <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
          <input
            type="text"
            className="input input-primary w-full"
            placeholder={t.translations.NAME}
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
          />

          <textarea
            className="textarea textarea-bordered w-full"
            placeholder={t.translations.DESCRIPTION}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />

          <div className="modal-action">
            <button type="button" className="btn" onClick={handleClose}>
              {t.translations.CANCEL}
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isSubmitting}
            >
              {isSubmitting ? t.translations.SAVING : t.translations.SAVE}
            </button>
          </div>
        </form>
      </div>
    </dialog>
  );
};

export default AddLabelModal;
