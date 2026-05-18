import React, { useState, useEffect } from "react";
import { useLanguage } from "@/app/contexts/Language";
import { updateOauthApplication } from "@/app/lib/client_service/oauth_services.client";

interface EditOAuthApplicationProps {
  isOpen: boolean;
  onClose: () => void;
  oAuthApplicationId: number;
  oAuthApplicationName: string;
  oAuthApplicationCallbackURL: string;
  oAuthApplicationDescription: string;
  oAuthApplicationBaseURL: string;
  oAuthApplicationAppOwnerEmail: string;
  onOAuthApplicationUpdated: () => void;
}

// Main EditSysUser component
const EditOAuthApplication = ({
  isOpen,
  onClose,
  oAuthApplicationId,
  oAuthApplicationName,
  oAuthApplicationCallbackURL,
  oAuthApplicationDescription,
  oAuthApplicationBaseURL,
  oAuthApplicationAppOwnerEmail,
  onOAuthApplicationUpdated,
}: EditOAuthApplicationProps) => {
  const { t } = useLanguage();
  const [name, setName] = useState(oAuthApplicationName || "");
  const [callbackUrl, setCallbackURL] = useState(
    oAuthApplicationCallbackURL || ""
  );
  const [description, setDescription] = useState(
    oAuthApplicationDescription || ""
  );
  const [baseUrl, setBaseUrl] = useState(oAuthApplicationBaseURL || "");
  const [appOwnerEmail, setAppOwnerEmail] = useState(
    oAuthApplicationAppOwnerEmail || ""
  );

  useEffect(() => {
    if (isOpen) {
      setName(oAuthApplicationName ?? "");
      setDescription(oAuthApplicationDescription ?? "");
      setCallbackURL(oAuthApplicationCallbackURL ?? "");
      setBaseUrl(oAuthApplicationBaseURL ?? "");
      setAppOwnerEmail(oAuthApplicationAppOwnerEmail ?? "");
    }
  }, [
    isOpen,
    oAuthApplicationName,
    oAuthApplicationDescription,
    oAuthApplicationCallbackURL,
    oAuthApplicationBaseURL,
    oAuthApplicationAppOwnerEmail,
  ]);

  const handleUpdate = async () => {
    try {
      await updateOauthApplication(oAuthApplicationId, {
        name,
        description,
        callbackUrl,
        baseUrl,
        appOwnerEmail,
      });
      onOAuthApplicationUpdated();
    } catch (error) {
      console.error("Error updating oAuthApplication:", error);
      alert("An error occurred while updating the oAuthApplication.");
    }

    onClose();
  };

  return (
    <>
      {isOpen && (
        <dialog className="modal modal-open">
          <div className="modal-box max-w-lg">
            <h3 className="font-bold text-lg mb-4 text-neutral">
              {t.translations.EDIT_OAUTH_APP}
            </h3>
            <label className="font-semibold text-sm text-neutral">
              {t.translations.NAME}
            </label>
            <div>
              <input
                type="text"
                placeholder="Name"
                className="input input-primary w-full"
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
            <label className="font-semibold text-sm text-neutral">
              {t.translations.CALLBACK_URL}
            </label>
            <input
              type="text"
              placeholder="CallbackURL"
              className="input input-primary w-full"
              value={callbackUrl}
              onChange={(e) => setCallbackURL(e.target.value)}
              required
            />
            <label className="font-semibold text-sm text-neutral">
              {t.translations.DESCRIPTION}
            </label>
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
            <label className="font-semibold text-sm text-neutral">
              {t.translations.BASE_URL}
            </label>
            <input
              placeholder={t.translations.BASE_URL}
              className="input input-bordered input-primary bg-base-100 text-base-content placeholder:text-base-content/40 w-full"
              value={baseUrl}
              onChange={(e) => setBaseUrl(e.target.value)}
            />
            <label className="font-semibold text-sm text-neutral">
              {t.translations.APP_OWNER_EMAIL}
            </label>
            <input
              placeholder={t.translations.APP_OWNER_EMAIL}
              className="input input-bordered input-primary bg-base-100 text-base-content placeholder:text-base-content/40 w-full"
              value={appOwnerEmail}
              onChange={(e) => setAppOwnerEmail(e.target.value)}
            />
            <div className="modal-action">
              <button type="button" className="btn" onClick={onClose}>
                {t.translations.CANCEL}
              </button>
              <button
                type="submit"
                className="btn btn-primary"
                onClick={handleUpdate}
              >
                {t.translations.SAVE}
              </button>
            </div>
          </div>
        </dialog>
      )}
    </>
  );
};

export default EditOAuthApplication;
