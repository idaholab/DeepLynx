"use client";
import { useLanguage } from "@/app/contexts/Language";
import { createOrganization } from "@/app/lib/client_service/organization_services.client";
import { useRouter } from "next/navigation";
import { useState } from "react";

interface CreateOrganizationModalProps {
  isOpen: boolean; // Indicates whether the modal is open
  onClose: () => void; // Function to call when closing the modal
  onOrganizationCreated: () => void;
}

const CreateOrganization = ({
  isOpen,
  onClose,
  onOrganizationCreated,
}: CreateOrganizationModalProps) => {
  const { t } = useLanguage();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [toastMessage, setToastMessage] = useState("");
  const [toastType, setToastType] = useState<
    "success" | "error" | "info" | null
  >(null);

  const handleSubmit = async () => {
    let data;
    if (isLoading) return;
    setIsLoading(true);
    try {
      data = await createOrganization({
        name,
        description,
      });

      setToastType("success");
      setToastMessage("Project Created Successfully");

      setName("");
      setDescription("");

      setTimeout(() => {
        onOrganizationCreated();
        setToastMessage("");
        setToastType(null);
        onClose();
      }, 1000);
    } catch (error) {
      console.error("Failed to create organization", error);
      setToastType("error");
      setToastMessage("Failed to create organization");

      setTimeout(() => {
        setToastMessage("");
        setToastType(null);
      }, 2000);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <>
      {/* Toast Message */}
      {toastMessage && toastType && (
        <div className="toast toast-top toast-end z-50">
          <div className={`alert alert-${toastType}`}>
            <span>{toastMessage}</span>
          </div>
        </div>
      )}
      {/* Render the modal dialog if isOpen is true */}
      {isOpen && (
        <dialog className="modal modal-open">
          <div className="modal-box max-w-lg">
            <h3 className="font-bold text-lg mb-4 text-base-content">
              {t.translations.CREATE_ORGANIZATION}
            </h3>
            <div className="mb-2">
              <input
                type="text"
                placeholder={t.translations.NAME}
                className="input input-bordered input-primary bg-base-100 text-base-content placeholder:text-base-content/40 w-full"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
              />
            </div>
            <textarea
              placeholder={t.translations.DESCRIPTION}
              className="textarea textarea-bordered textarea-primary bg-base-100 text-base-content placeholder:text-base-content/40 min-h-[100px] w-full"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />

            {/* Modal Actions */}
            <div className="modal-action mt-6">
              <button type="button" className="btn btn-ghost" onClick={onClose}>
                {t.translations.CANCEL}
              </button>
              <button
                type="submit"
                disabled={isLoading}
                aria-busy={isLoading}
                className="btn btn-primary"
                onClick={handleSubmit}
              >
                {isLoading ? (
                  <>
                    <span className="spinner" aria-hidden="true" />
                  </>
                ) : (
                  t.translations.CREATE
                )}
              </button>
            </div>
          </div>
        </dialog>
      )}
    </>
  );
};

export default CreateOrganization;
