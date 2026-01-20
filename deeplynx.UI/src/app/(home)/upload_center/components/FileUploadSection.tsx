"use client";

import { useLanguage } from "@/app/contexts/Language";
import DropUpload from "../../components/DropUpload";
import NewFileUploadCard from "../../components/NewFileUploadCard";
import { UploadType, FileMetadata, ExistingFile } from "../../types/types";

interface FileUploadSectionProps {
  uploadType: UploadType;
  setUploadType: (type: UploadType) => void;
  multi: boolean;
  setMulti: (multi: boolean) => void;
  selectedFiles: File[];
  setSelectedFiles: (files: File[]) => void;
  setShowMultiFileWarning: (show: boolean) => void;
  dropKey: number;
  filesMetadata: Record<number, FileMetadata>;
  handleMetadataChange: (fileIndex: number, metadata: FileMetadata) => void;
  targetFileId: string;
  setTargetFileId: (id: string) => void;
  availableFiles: ExistingFile[];
  needsTarget: boolean;
  isMultiAllowed: boolean;
}

export default function FileUploadSection({
  uploadType,
  setUploadType,
  multi,
  setMulti,
  selectedFiles,
  setSelectedFiles,
  setShowMultiFileWarning,
  dropKey,
  filesMetadata,
  handleMetadataChange,
  targetFileId,
  setTargetFileId,
  availableFiles,
  needsTarget,
  isMultiAllowed,
}: FileUploadSectionProps) {
  const { t } = useLanguage();

  return (
    <>
      {/* Upload Type Selector */}
      <fieldset>
        <label className="label text-base-content font-bold">
          {t.translations.UPLOADING}
          <select
            value={uploadType}
            onChange={(e) => setUploadType(e.target.value as UploadType)}
            className="select select-info select-sm mt-2"
            required
          >
            <option value="new">{t.translations.NEW_FILE}</option>
          </select>
          {needsTarget && (
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
          )}
        </label>
      </fieldset>

      {/* Multiple Files Toggle */}
      <fieldset>
        <label className="label cursor-pointer justify-start gap-3">
          <span className="label-text text-xs">
            {t.translations.UPLOAD_MULTIPLE_FILES}
          </span>
          <input
            type="checkbox"
            checked={multi}
            disabled={!isMultiAllowed}
            onChange={(e) => {
              if (!isMultiAllowed) return;
              const checked = e.target.checked;
              if (!checked && selectedFiles.length > 1) {
                setShowMultiFileWarning(true);
                return;
              }
              setMulti(checked);
            }}
            className="toggle toggle-secondary"
          />
        </label>
      </fieldset>

      {/* Drop Upload */}
      {(multi || selectedFiles.length === 0) && (
        <DropUpload
          key={dropKey}
          multiple={multi}
          files={selectedFiles}
          onFilesChange={setSelectedFiles}
          disabled={!uploadType || (needsTarget && !targetFileId)}
        />
      )}

      {/* File Cards */}
      {selectedFiles.length >= 1 &&
        selectedFiles.map((file, index) => (
          <NewFileUploadCard
            key={index}
            defaultName={file.name}
            uploadType={uploadType}
            fileIndex={index}
            onMetadataChange={handleMetadataChange}
          />
        ))}
    </>
  );
}
