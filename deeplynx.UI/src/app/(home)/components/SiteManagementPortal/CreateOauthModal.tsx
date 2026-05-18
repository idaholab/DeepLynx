"use client";
import { useLanguage } from "@/app/contexts/Language";
import { useState } from "react";
import toast from "react-hot-toast";
import ToastInfoModal from "../ToastInfoModal";
import { createOauthApplication } from "@/app/lib/client_service/oauth_services.client";

interface CreateOAuthModalProps {
  isOpen: boolean;
  onClose: () => void;
  onOAuthApplicationCreated: () => void;
}

const CreateOAuthModal = ({
  isOpen,
  onClose,
  onOAuthApplicationCreated,
}: CreateOAuthModalProps) => {
  const { t } = useLanguage();
  const [name, setName] = useState("");
  const [callbackUrl, setCallbackUrl] = useState("");
  const [description, setDescription] = useState("");
  const [baseUrl, setBaseUrl] = useState("");
  const [appOwnerEmail, setAppOwnerEmail] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [toastMessage, setToastMessage] = useState("");
  const [toastType, setToastType] = useState<
    "success" | "error" | "info" | null
  >(null);

  const handleSubmit = async () => {
    if (isLoading) return;
    setIsLoading(true);
    try {
      const response = await createOauthApplication({
        name,
        callbackUrl,
        description,
        baseUrl,
        appOwnerEmail,
      });
      const clientid = "Client Id: " + response.clientId;
      const clientsecret = "Client Secret: " + response.clientSecretRaw;
      toast.success(
        (t) => (
          <ToastInfoModal
            title={
              "OAuth Application created successfully! Keep these somewhere safe:"
            }
            toastId={t.id}
            infoDisplay={[clientid, clientsecret]}
          />
        ),
        {
          duration: Infinity,
          style: {
            maxWidth: "none",
          },
        }
      );
      setName("");
      setCallbackUrl("");
      setDescription("");
      setBaseUrl("");
      setAppOwnerEmail("");

      setTimeout(() => {
        onOAuthApplicationCreated();
        setToastMessage("");
        setToastType(null);
        onClose();
      }, 1000);
    } catch (error) {
      console.error("Failed to create OAuth Application", error);
      setToastType("error");
      setToastMessage("Failed to create OAuth Application");

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
      {isOpen && (
        <dialog className="modal modal-open">
          <div className="modal-box max-w-lg">
            <h3 className="font-bold text-lg mb-4 text-base-content">
              {t.translations.CREATE_OAUTH_APPLICATION}
            </h3>

            <div className="flex flex-col gap-4">
              <div>
                <input
                  type="text"
                  placeholder={t.translations.NAME}
                  className="input input-bordered input-primary bg-base-100 text-base-content placeholder:text-base-content/40 w-full"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  maxLength={50}
                  required
                />
                <span className={`text-xs mt-1 float-right ${name.length >= 50 ? "text-error" :
                  name.length >= 40 ? "text-warning" :
                    "text-base-content"
                  }`}>
                  {name.length}/50
                </span>
              </div>

              <input
                placeholder={t.translations.CALLBACK_URL}
                className="input input-bordered input-primary bg-base-100 text-base-content placeholder:text-base-content/40 w-full"
                value={callbackUrl}
                onChange={(e) => setCallbackUrl(e.target.value)}
              />
              <div>
                <textarea
                  placeholder={t.translations.DESCRIPTION}
                  className="textarea textarea-bordered textarea-primary bg-base-100 text-base-content placeholder:text-base-content/40 min-h-[100px] w-full"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  maxLength={250}
                />
                <span className={`text-xs mt-1 float-right ${description.length >= 250 ? "text-error" :
                  description.length >= 240 ? "text-warning" :
                    "text-base-content"
                  }`}>
                  {description.length}/250
                </span>
              </div>

              <input
                placeholder={t.translations.BASE_URL}
                className="input input-bordered input-primary bg-base-100 text-base-content placeholder:text-base-content/40 w-full"
                value={baseUrl}
                onChange={(e) => setBaseUrl(e.target.value)}
              />
              <input
                placeholder={t.translations.APP_OWNER_EMAIL}
                className="input input-bordered input-primary bg-base-100 text-base-content placeholder:text-base-content/40 w-full"
                value={appOwnerEmail}
                onChange={(e) => setAppOwnerEmail(e.target.value)}
              />
            </div>

            <div className="modal-action mt-6">
              <button type="button" className="btn btn-ghost" onClick={onClose}>
                {t.translations.CANCEL}
              </button>
              <button
                type="button"
                disabled={isLoading}
                aria-busy={isLoading}
                className="btn btn-primary"
                onClick={handleSubmit}
              >
                {isLoading ? (
                  <span className="spinner" aria-hidden="true" />
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

export default CreateOAuthModal;
