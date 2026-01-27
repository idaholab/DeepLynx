// src/app/(home)/project_management/[id]/settings/components/RemoveLogoModal.tsx
"use client";

interface RemoveLogoModalProps {
  onRemoveLogo: () => void;
  t: { translations: Record<string, string> };
}

const RemoveLogoModal = ({ onRemoveLogo, t }: RemoveLogoModalProps) => (
  <>
    <input type="checkbox" id="remove_project_logo" className="modal-toggle" />
    <div className="modal" role="dialog">
      <div className="modal-box">
        <h3 className="text-lg font-bold">{t.translations.REMOVE_LOGO}</h3>
        <p className="py-4">
          {t.translations.ARE_YOU_SURE_YOU_WANT_TO_REMOVE_LOGO_FROM_PROJECT}
        </p>
        <div className="modal-action">
          <label htmlFor="remove_project_logo" className="btn">
            {t.translations.CANCEL}
          </label>
          <label
            htmlFor="remove_project_logo"
            className="btn btn-outline btn-secondary"
            onClick={onRemoveLogo}
          >
            {t.translations.REMOVE}
          </label>
        </div>
      </div>
    </div>
  </>
);

export default RemoveLogoModal;
