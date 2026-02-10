"use client";

import { useLanguage } from "@/app/contexts/Language";
import { translations } from "@/app/lib/translations";

interface ConfirmationModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  tagName: string;
  recordName?: string | null;
}

// Main ConfirmationModal component
const ConfirmationModal = ({
  isOpen,
  onClose,
  onConfirm,
  tagName,
  recordName,
}: ConfirmationModalProps) => {
  const { t } = useLanguage();

  return (
    <>
      {/* Render the modal dialog if isOpen is true */}
      {isOpen && (
        <dialog className="modal modal-open">
          {/* Modal dialog with styles */}
          <div className="modal-box max-w-lg">
            {/* Box for modal content with max width */}
            <h3 className="font-bold text-lg mb-4 text-center text-black">
              {t.translations.ARE_YOU_SURE} {/* Header for the modal */}
            </h3>
            {/* Message for the confirmation */}
            <p className="text-center">
              <strong>{tagName}</strong> {t.translations.FROM}{" "}
              <strong>{recordName}</strong>
            </p>
            {/* Modal Action Buttons */}
            <div className="modal-action flex justify-between mt-14">
              <button className="btn text-blue-600" onClick={onClose}>
                {/* No button calls onClose */}
                {t.translations.NO}
              </button>
              <button className="btn btn-primary" onClick={onConfirm}>
                {t.translations.YES} {/* Yes button confirms unlinking */}
              </button>
            </div>
          </div>
        </dialog>
      )}
    </>
  );
};

export default ConfirmationModal;
