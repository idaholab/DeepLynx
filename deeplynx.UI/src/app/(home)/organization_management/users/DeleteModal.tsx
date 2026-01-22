// src/app/(home)/organization_management/users/DeleteModal.tsx
"use client";

import React from "react";
import { ExclamationTriangleIcon, XMarkIcon } from "@heroicons/react/24/outline";

interface DeleteModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  message: string;
  confirmText: string;
  cancelText?: string;
  isDestructive?: boolean;
  loading?: boolean;
}

const DeleteModal: React.FC<DeleteModalProps> = ({
  isOpen,
  onClose,
  onConfirm,
  title,
  message,
  confirmText,
  cancelText = "Cancel",
  isDestructive = false,
  loading = false,
}) => {
  if (!isOpen) return null;

  const confirmBtnClass = isDestructive ? "btn-error" : "btn-primary";

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-md">
        {/* Header */}
        <div className="flex items-start justify-between mb-4">
          <div className="flex items-center gap-3">
            <div className={`rounded-full p-2 ${isDestructive ? "bg-error/10" : "bg-warning/10"}`}>
              <ExclamationTriangleIcon
                className={`w-6 h-6 ${
                  isDestructive ? "text-error" : "text-warning"
                }`}
              />
            </div>
            <h3 className="font-bold text-lg">{title}</h3>
          </div>
          <button
            className="btn btn-sm btn-circle btn-ghost"
            onClick={onClose}
            disabled={loading}
          >
            <XMarkIcon className="w-4 h-4" />
          </button>
        </div>

        {/* Body */}
        <p className="text-sm text-base-content/70 whitespace-pre-line">
          {message}
        </p>

        {/* Actions */}
        <div className="modal-action mt-6">
          <button
            className="btn btn-ghost"
            onClick={onClose}
            disabled={loading}
          >
            {cancelText}
          </button>
          <button
            className={`btn ${confirmBtnClass} gap-2`}
            onClick={onConfirm}
            disabled={loading}
          >
            {loading && (
              <span className="loading loading-spinner loading-sm" />
            )}
            {confirmText}
          </button>
        </div>
      </div>

      {/* Backdrop */}
      <div
        className="modal-backdrop"
        onClick={() => {
          if (!loading) onClose();
        }}
      />
    </div>
  );
};

export default DeleteModal;
