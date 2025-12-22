import React, { useState } from "react";

type ActionType = "archive" | "delete";

interface ArchiveDeleteProps {
  actionType: ActionType;
  itemType: string; // e.g., "Project", "Organization", "Document"
  itemName: string;
  onConfirm: () => void | Promise<void>;
}

const ArchiveDelete: React.FC<ArchiveDeleteProps> = ({
  actionType,
  itemType,
  itemName,
  onConfirm,
}) => {
  const [showConfirm, setShowConfirm] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const isArchive = actionType === "archive";
  const capitalizedAction = actionType === "archive" ? "Archive" : "Delete";

  const handleConfirm = async () => {
    setIsLoading(true);
    try {
      await onConfirm();
      setShowConfirm(false);
    } catch (error) {
      console.error(`${actionType} failed:`, error);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div>
      <div
        className={`border ${
          isArchive ? "border-orange-200 bg-orange-600/5" : "border-red-200 bg-red-600/5"
        } rounded-lg p-4 max-w-2xl`}
      >
        <div className="flex items-center gap-2">
          <svg
            className={`w-5 h-5 ${isArchive ? "text-orange-600" : "text-red-600"}`}
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={1.5}
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d={
                isArchive
                  ? "m20.25 7.5-.625 10.632a2.25 2.25 0 0 1-2.247 2.118H6.622a2.25 2.25 0 0 1-2.247-2.118L3.75 7.5m8.25 3v6.75m0 0-3-3m3 3 3-3M3.375 7.5h17.25c.621 0 1.125-.504 1.125-1.125v-1.5c0-.621-.504-1.125-1.125-1.125H3.375c-.621 0-1.125.504-1.125 1.125v1.5c0 .621.504 1.125 1.125 1.125Z"
                  : "m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0"
              }
            />
          </svg>
          <h3 className="font-semibold text-base-content">
            {capitalizedAction} {itemType}
          </h3>
        </div>
        <p className="text-sm text-base-content mt-1">
          {isArchive
            ? `Archive this ${itemType.toLowerCase()} to remove it from your active ${itemType.toLowerCase()}s. Archived ${itemType.toLowerCase()}s can be restored later.`
            : `Permanently delete this ${itemType.toLowerCase()}. This action cannot be undone.`}
        </p>
        <button
          onClick={() => setShowConfirm(true)}
          className={`px-4 py-2 mt-4 border ${
            isArchive ? "border-orange-600 text-orange-600 hover:bg-orange-600" : "border-red-600 text-red-600 hover:bg-red-600"
          } text-xs font-medium rounded-lg hover:text-white transition-colors whitespace-nowrap`}
        >
          {capitalizedAction} {itemType}: {itemName}
        </button>
      </div>

      {showConfirm && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-lg max-w-md w-full p-6 shadow-xl">
            <div className="flex items-start gap-3">
              <div
                className={`${
                  isArchive ? "bg-orange-100" : "bg-red-100"
                } rounded-full p-2 shrink-0`}
              >
                <svg
                  className={`w-6 h-6 ${isArchive ? "text-orange-600" : "text-red-600"}`}
                  fill="none"
                  viewBox="0 0 24 24"
                  strokeWidth={1.5}
                  stroke="currentColor"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z"
                  />
                </svg>
              </div>
              <div className="flex-1">
                <h3 className="text-lg font-semibold text-black">
                  {capitalizedAction} {itemType}
                </h3>
                <p className="text-sm text-black mt-2">
                  {isArchive
                    ? `Are you sure you want to archive this ${itemType.toLowerCase()}? You'll be able to restore it later from your archived ${itemType.toLowerCase()}s.`
                    : `Are you sure you want to delete this ${itemType.toLowerCase()}? This action cannot be undone and all data will be permanently lost.`}
                </p>
              </div>
            </div>

            <div className="flex gap-3 mt-6 justify-end">
              <button
                onClick={() => setShowConfirm(false)}
                disabled={isLoading}
                className="px-4 py-2 text-black border border-base-300 rounded-lg font-medium hover:bg-base-200/70 hover:border-base-200/50 hover:text-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                Cancel
              </button>
              <button
                onClick={handleConfirm}
                disabled={isLoading}
                className={`px-4 py-2 ${
                  isArchive ? "bg-orange-600 hover:bg-orange-700" : "bg-red-600 hover:bg-red-700"
                } text-white rounded-lg font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed`}
              >
                {isLoading ? `${capitalizedAction}ing...` : `${capitalizedAction} ${itemType}`}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default ArchiveDelete;