"use client";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { CreateProjectRequestDto } from "../types/requestDTOs";
import { createProject } from "@/app/lib/client_service/projects_services.client";

interface CreateProjectsModalProps {
  isOpen: boolean; // Indicates whether the modal is open
  onClose: () => void; // Function to call when closing the modal
  onProjectCreated: () => void;
}

// Main CreateProject component
const CreateProject = ({
  isOpen,
  onClose,
  onProjectCreated,
}: CreateProjectsModalProps) => {
  const { t } = useLanguage();
  const [name, setName] = useState("");
  const [abbreviation, setAbbreviation] = useState("");
  const [description, setDescription] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  // TODO: Use the react hot toast ... it uses a lot less code
  const [toastMessage, setToastMessage] = useState("");
  const [toastType, setToastType] = useState<
    "success" | "error" | "info" | null
  >(null);
  const router = useRouter();
  const { organization } = useOrganizationSession();

  const handleSubmit = async () => {
    let data;
    if (isLoading) return;
    setIsLoading(true);
    const dto: CreateProjectRequestDto = {
      name: name,
      abbreviation: abbreviation,
      description: description,
    };
    try {
      data = await createProject(organization?.organizationId as number, dto);

      setToastType("success");
      setToastMessage("Project Created Successfully");

      setName("");
      setAbbreviation("");
      setDescription("");

      setTimeout(() => {
        onProjectCreated();
        setToastMessage("");
        setToastType(null);
        onClose();
      }, 1000);
    } catch (error) {
      console.error("Failed to create project", error);
      setToastType("error");
      setToastMessage("Failed to create Project");

      setTimeout(() => {
        setToastMessage("");
        setToastType(null);
      }, 2000);
    } finally {
      setIsLoading(false);
      router.push(`/project/${data?.id}`);
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
              {t.translations.CREATE_PROJECT}
            </h3>
            <form
              method="dialog"
              className="flex flex-col gap-4"
              onSubmit={(e) => {
                e.preventDefault();
                handleSubmit();
              }}
            >
              <input
                type="text"
                placeholder={t.translations.NAME}
                className="input input-bordered input-primary bg-base-100 text-base-content placeholder:text-base-content/40 w-full"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
              />
              <textarea
                placeholder={t.translations.DESCRIPTION} // Placeholder for project description
                className="textarea textarea-bordered textarea-primary bg-base-100 text-base-content placeholder:text-base-content/40 min-h-[100px] w-full"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />

              {/* Modal Actions */}
              <div className="modal-action mt-6">
                <button
                  type="button"
                  className="btn btn-ghost"
                  onClick={onClose}
                >
                  {t.translations.CANCEL}
                </button>
                <button
                  type="submit"
                  disabled={isLoading}
                  aria-busy={isLoading}
                  className="btn btn-primary"
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
            </form>
          </div>
        </dialog>
      )}
    </>
  );
};

export default CreateProject;
