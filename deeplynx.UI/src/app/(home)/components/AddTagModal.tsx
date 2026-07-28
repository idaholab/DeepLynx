// src/app/(home)/components/AddTagModal.tsx
"use client";

import React, { useState } from "react";
import toast from "react-hot-toast";

import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";

import { TagResponseDto } from "@/app/(home)/types/responseDTOs";
import { createTag } from "@/app/lib/client_service/tag_services.client";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

type Props = {
  isOpen: boolean;
  onClose: () => void;
  projectId: number;
  onTagCreated?: (newTag: TagResponseDto) => void;
};

// Minimal payload for creating a tag.
// Adjust this to match your actual API contract if needed.
type CreateTagPayload = {
  name: string;
};

/* -------------------------------------------------------------------------- */
/*                               AddTagModal                                  */
/* -------------------------------------------------------------------------- */

const AddTagModal: React.FC<Props> = ({
  isOpen,
  onClose,
  projectId,
  onTagCreated,
}) => {
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();

  const [name, setName] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  /* ------------------------------------------------------------------------ */
  /*                               Helpers                                    */
  /* ------------------------------------------------------------------------ */

  const resetForm = () => {
    setName("");
    setIsSubmitting(false);
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  /* ------------------------------------------------------------------------ */
  /*                              Submit Logic                                */
  /* ------------------------------------------------------------------------ */

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    setIsSubmitting(true);

    if (!organization?.organizationId) {
      toast.error("No organization selected.");
      setIsSubmitting(false);
      return;
    }

    const payload: CreateTagPayload = {
      name,
    };

    try {
      const newTag = await createTag(
        organization.organizationId as number,
        projectId,
        payload
      );

      if (onTagCreated && newTag) {
        onTagCreated(newTag as TagResponseDto);
      }

      toast.success("Tag created");
      resetForm();
      onClose();
    } catch (error) {
      console.error("Error creating tag:", error);
      toast.error("Failed to create tag");
    } finally {
      setIsSubmitting(false);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                               Main Render                                */
  /* ------------------------------------------------------------------------ */

  return (
    <dialog className={`modal ${isOpen ? "modal-open" : ""}`}>
      <div className="modal-box">
        <h3 className="text-base-content font-bold text-lg mb-4">
          {t.translations.ADD_A_TAG}
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

export default AddTagModal;
