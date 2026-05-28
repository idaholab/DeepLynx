"use client";

import { useLanguage } from "@/app/contexts/Language";
import { CHUNK_THRESHOLD } from "@/app/lib/client_service/file_upload_services.client";
import toast from "react-hot-toast";
import { ExistingFile, FileMetadata } from "../../types/types";
import type { ClassResponseDto } from "../../types/responseDTOs";
import DropUpload from "./DropUpload";
import NewFileUploadCard from "./NewFileUploadCard";

interface FileUploadSectionProps {
  selectedFiles: File[];
  setSelectedFiles: (files: File[]) => void;
  dropKey: number;
  handleMetadataChange: (fileIndex: number, metadata: FileMetadata) => void;
  uploadErrorByFileIndex: Record<number, string>;
  targetFileId: string;
  setTargetFileId: (id: string) => void;
  availableFiles: ExistingFile[];
  availableClasses: ClassResponseDto[];
  isLoadingClasses: boolean;
  onSearchFiles: (query: string) => Promise<ExistingFile[]>;
  needsTarget: boolean;
  isUploading: boolean;
  canUpload: boolean;
  onUpload: () => Promise<void>;
  onClear: () => void;
  onRemoveAt: (idx: number) => void;
  projectId: number;
}

export default function FileUploadSection({
  selectedFiles,
  setSelectedFiles,
  dropKey,
  handleMetadataChange,
  uploadErrorByFileIndex,
  targetFileId,
  setTargetFileId,
  availableFiles,
  availableClasses,
  isLoadingClasses,
  onSearchFiles,
  needsTarget,
  isUploading,
  canUpload,
  onUpload,
  onClear,
  onRemoveAt,
  projectId,
}: FileUploadSectionProps) {
  const { t } = useLanguage();
  const isLargeFile = (file: File) => file.size >= CHUNK_THRESHOLD;
  const cardKeys = (() => {
    const occurrences = new Map<string, number>();
    return selectedFiles.map((file) => {
      const baseKey = `${file.name}-${file.size}-${file.lastModified}`;
      const count = (occurrences.get(baseKey) ?? 0) + 1;
      occurrences.set(baseKey, count);
      return `${baseKey}-${count}`;
    });
  })();

  const handleFilesChange = (files: File[]) => {
    const largeCount = files.filter(isLargeFile).length;
    if (largeCount <= 1) {
      setSelectedFiles(files);
      return;
    }

    let hasKeptLarge = false;
    const filtered = files.filter((file) => {
      if (!isLargeFile(file)) return true;
      if (hasKeptLarge) return false;
      hasKeptLarge = true;
      return true;
    });

    setSelectedFiles(filtered);
    toast.error(t.translations.ONLY_ONE_LARGE_FILE_ALLOWED);
  };

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
        onFilesChange={handleFilesChange}
        disabled={(needsTarget && !targetFileId) || isUploading}
      />

      {/* File Cards */}
      {selectedFiles.length > 0 &&
        selectedFiles.map((file, index) => (
          <NewFileUploadCard
            key={cardKeys[index]}
            defaultName={file.name}
            fileIndex={index}
            disableMetadataFile={file.size > CHUNK_THRESHOLD}
            onMetadataChange={handleMetadataChange}
            onRemove={() => onRemoveAt(index)}
            availableFiles={availableFiles}
            availableClasses={availableClasses}
            isLoadingClasses={isLoadingClasses}
            onSearchFiles={onSearchFiles}
            projectId={projectId}
            uploadError={uploadErrorByFileIndex[index]}
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
