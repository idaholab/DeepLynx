"use client";

import { useLanguage } from "@/app/contexts/Language";
import DropUpload from "../../components/DropUpload";
import NewFileUploadCard from "../../components/NewFileUploadCard";
import { FileMetadata, ExistingFile } from "../../types/types";

interface FileUploadSectionProps {
  selectedFiles: File[];
  setSelectedFiles: (files: File[]) => void;
  dropKey: number;
  handleMetadataChange: (fileIndex: number, metadata: FileMetadata) => void;
  targetFileId: string;
  setTargetFileId: (id: string) => void;
  availableFiles: ExistingFile[];
  onSearchFiles: (query: string) => Promise<ExistingFile[]>;
  needsTarget: boolean;
  isUploading: boolean;
  canUpload: boolean;
  onUpload: () => Promise<void>;
  onClear: () => void;
  onRemoveAt: (idx: number) => void;
}

export default function FileUploadSection({
  selectedFiles,
  setSelectedFiles,
  dropKey,
  handleMetadataChange,
  targetFileId,
  setTargetFileId,
  availableFiles,
  onSearchFiles,
  needsTarget,
  isUploading,
  canUpload,
  onUpload,
  onClear,
  onRemoveAt,
}: FileUploadSectionProps) {
  const { t } = useLanguage();

  return (
    <>
      {needsTarget && (
        <fieldset>
          <label className="label text-base-content font-bold">
            {t.translations.SELECT_EXISTING_FILE}
            <select
              value={targetFileId}
              onChange={(e) => setTargetFileId(e.target.value)}
              className="select select-info select-sm mt-2"
              required
            >
              <option value="" disabled>
                {t.translations.SELECT_EXISTING_FILE}
              </option>
              {availableFiles.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.name}
                </option>
              ))}
            </select>
          </label>
        </fieldset>
      )}

      {/* Drop Upload */}
      <DropUpload
        key={dropKey}
        multiple={true}
        files={selectedFiles}
        onFilesChange={setSelectedFiles}
        disabled={(needsTarget && !targetFileId) || isUploading}
      />

      {/* File Cards */}
      {selectedFiles.length > 0 &&
        selectedFiles.map((file, index) => (
          <NewFileUploadCard
            key={index}
            defaultName={file.name}
            fileIndex={index}
            onMetadataChange={handleMetadataChange}
            onRemove={() => onRemoveAt(index)}
            availableFiles={availableFiles}
            onSearchFiles={onSearchFiles}
          />
        ))}

      {selectedFiles.length > 0 && (
        <div className="mt-4 flex justify-end gap-2 border-t border-base-300/60 pt-4">
          <button
            type="button"
            className="btn btn-ghost btn-sm"
            onClick={onClear}
            disabled={isUploading}
          >
            {t.translations.CLEAR_ALL}
          </button>
          <button
            type="button"
            className="btn btn-secondary btn-sm"
            onClick={() => void onUpload()}
            disabled={!canUpload || isUploading}
          >
            {isUploading ? (
              <>
                <span className="loading loading-spinner loading-xs"></span>
                {t.translations.UPLOADING}
              </>
            ) : (
              t.translations.UPLOAD
            )}
          </button>
        </div>
      )}
    </>
  );
}
