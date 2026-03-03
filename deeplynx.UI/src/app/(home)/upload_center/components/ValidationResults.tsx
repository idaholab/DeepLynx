"use client";

import {
  ArrowUpTrayIcon,
  CheckCircleIcon,
  ExclamationCircleIcon,
  EyeIcon,
  XCircleIcon,
} from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";
import RecordPreviewTable from "./RecordPreviewTable";
import UploadProgressBar from "./UploadProgressBar";
import type {
  ValidationError,
  ValidationResult,
} from "../../types/bulk_upload_types";

function interpolate(
  template: string,
  values: Record<string, string | number>,
): string {
  return Object.entries(values).reduce(
    (result, [key, value]) => result.replace(`{${key}}`, String(value)),
    template,
  );
}

interface ValidationResultsProps {
  isParsing: boolean;
  csvParseErrors: string[];
  isValidating: boolean;
  validationResult: ValidationResult | null;
  isUploading: boolean;
  showPreview: boolean;
  setShowPreview: (show: boolean) => void;
  uploadProgress: number;
  setShowUploadConfirm: (show: boolean) => void;
  backendErrors: Array<{
    message: string;
    type: "validation" | "not_found" | "permission" | "general";
    suggestion?: string;
  }>;
}

export default function ValidationResults({
  isParsing,
  csvParseErrors,
  isValidating,
  validationResult,
  isUploading,
  showPreview,
  setShowPreview,
  uploadProgress,
  setShowUploadConfirm,
  backendErrors,
}: ValidationResultsProps) {
  const { t } = useLanguage();

  return (
    <div className="mt-4 space-y-4">
      {/* Parsing Status */}
      {isParsing && (
        <div className="alert alert-info">
          <span className="loading loading-spinner loading-sm"></span>
          <span>{t.translations.PARSING_CSV_FILE}</span>
        </div>
      )}

      {/* Parsing Errors */}
      {!isParsing && csvParseErrors.length > 0 && (
        <div className="alert alert-error">
          <XCircleIcon className="size-6" />
          <div>
            <h3 className="font-bold">{t.translations.CSV_PARSING_ERRORS}</h3>
            <ul className="list-disc list-inside text-sm">
              {csvParseErrors.map((error, idx) => (
                <li key={idx}>{error}</li>
              ))}
            </ul>
          </div>
        </div>
      )}

      {/* Validating Status */}
      {isValidating && (
        <div className="alert alert-info">
          <span className="loading loading-spinner loading-sm"></span>
          <span>{t.translations.VALIDATING_RECORDS}</span>
        </div>
      )}

      {/* Backend Errors */}
      {!isParsing && !isValidating && backendErrors.length > 0 && (
        <div className="alert alert-error">
          <XCircleIcon className="size-6" />
          <div className="w-full">
            <h3 className="font-bold">{t.translations.UPLOAD_FAILED_TITLE}</h3>
            <p className="text-sm mb-3">
              {t.translations.SERVER_REJECTED_UPLOAD_FIX_ISSUES}
            </p>

            <div className="space-y-3">
              {backendErrors.map((error, idx) => (
                <div key={idx} className="bg-base-100 text-error p-3 rounded">
                  <div className="flex items-start gap-2 mb-2">
                    {error.type === "not_found" && (
                      <span className="badge badge-warning badge-sm">
                        {t.translations.NOT_FOUND}
                      </span>
                    )}
                    {error.type === "validation" && (
                      <span className="badge badge-error badge-sm">
                        {t.translations.VALIDATION}
                      </span>
                    )}
                    {error.type === "permission" && (
                      <span className="badge badge-error badge-sm">
                        {t.translations.PERMISSION}
                      </span>
                    )}
                    {error.type === "general" && (
                      <span className="badge badge-neutral badge-sm">
                        {t.translations.ERROR_LABEL}
                      </span>
                    )}
                  </div>

                  <p className="text-sm font-semibold mb-1">{error.message}</p>

                  {error.suggestion && (
                    <p className="text-sm text-base-content/70 italic">
                      {error.suggestion}
                    </p>
                  )}
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Validation Success */}
      {!isParsing &&
        !isValidating &&
        validationResult &&
        validationResult.isValid && (
          <div className="space-y-3">
            <div className="alert alert-success">
              <CheckCircleIcon className="size-6" />
              <div className="w-full">
                <h3 className="font-bold">
                  {t.translations.VALIDATION_SUCCESSFUL}
                </h3>
                <p className="text-sm">
                  {interpolate(
                    t.translations.ALL_RECORDS_VALID_READY_TO_UPLOAD,
                    {
                      count: validationResult.validCount,
                    },
                  )}
                </p>
              </div>
            </div>

            {/* Preview Toggle */}
            {!isUploading && (
              <div className="flex justify-between items-center">
                <button
                  onClick={() => setShowPreview(!showPreview)}
                  className="btn btn-ghost btn-sm gap-2"
                  type="button"
                >
                  <EyeIcon className="size-6" />
                  {showPreview
                    ? t.translations.HIDE_PREVIEW
                    : t.translations.PREVIEW_RECORDS}
                </button>
              </div>
            )}

            {/* Record Preview Table */}
            {showPreview && !isUploading && (
              <div className="bg-base-200/50 p-4 rounded-lg">
                <RecordPreviewTable records={validationResult.validRecords} />
              </div>
            )}

            {/* Upload Progress */}
            {isUploading && (
              <div className="bg-base-200/50 p-6 rounded-lg">
                <UploadProgressBar
                  progress={uploadProgress}
                  current={Math.round(
                    (uploadProgress / 100) * validationResult.validCount
                  )}
                  total={validationResult.validCount}
                />
              </div>
            )}

            {/* Upload Button */}
            {!isUploading && (
              <div className="flex justify-end">
                <button
                  onClick={() => setShowUploadConfirm(true)}
                  className="btn btn-primary gap-2"
                  type="button"
                >
                  <ArrowUpTrayIcon className="size-6" />
                  {interpolate(t.translations.UPLOAD_RECORDS_WITH_COUNT, {
                    count: validationResult.validCount,
                  })}
                </button>
              </div>
            )}
          </div>
        )}

      {/* Validation Errors */}
      {!isParsing &&
        !isValidating &&
        validationResult &&
        !validationResult.isValid && (
          <div className="space-y-3">
            <div className="alert alert-warning">
              <ExclamationCircleIcon className="size-6" />
              <div className="w-full">
                <h3 className="font-bold">
                  {t.translations.VALIDATION_ERRORS_FOUND}
                </h3>
                <p className="text-sm mb-2">
                  {interpolate(t.translations.VALIDATION_ERROR_SUMMARY, {
                    invalid: validationResult.invalidCount,
                    total: validationResult.totalRows,
                  })}
                </p>
                <div className="text-sm">
                  <strong>{t.translations.VALID_LABEL}:</strong>{" "}
                  {validationResult.validCount} |{" "}
                  <strong>{t.translations.INVALID_LABEL}:</strong>{" "}
                  {validationResult.invalidCount}
                </div>
              </div>
            </div>

            {/* Detailed Errors */}
            <div className="bg-base-200/50 rounded-lg p-4">
              <h4 className="font-semibold mb-3 text-base-content">
                {t.translations.ERROR_DETAILS}
              </h4>
              <div className="space-y-2 max-h-96 overflow-y-auto">
                {validationResult.errors.map((error: ValidationError, idx) => (
                  <div
                    key={idx}
                    className="bg-base-100 p-3 rounded border-l-4 border-error"
                  >
                    <div className="flex items-start gap-2">
                      <span className="badge badge-error badge-sm">
                        {interpolate(t.translations.ROW_WITH_NUMBER, {
                          row: error.row,
                        })}
                      </span>
                      <div className="flex-1">
                        <p className="font-semibold text-sm text-base-content">
                          {error.recordName}
                        </p>
                        <ul className="list-disc list-inside text-sm text-base-content/70 mt-1 space-y-1">
                          {error.errors.map((err, errIdx) => (
                            <li key={errIdx}>{err}</li>
                          ))}
                        </ul>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}
    </div>
  );
}
