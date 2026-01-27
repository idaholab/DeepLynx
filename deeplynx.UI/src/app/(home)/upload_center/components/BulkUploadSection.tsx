"use client";

import { useLanguage } from "@/app/contexts/Language";
import { parseCsvFile } from "@/app/lib/client_service/csv_parser";
import { validateCsvRecords } from "@/app/lib/validate_records";
import type {
  ParsedCsvRow,
  ValidationResult,
} from "@/app/(home)/types/bulk_upload_types";
import type {
  DataSourceResponseDto,
  ProjectResponseDto,
} from "@/app/(home)/types/responseDTOs";
import toast from "react-hot-toast";
import CsvTemplateDownload from "../../components/CsvTemplateDownload";
import ValidationResults from "./ValidationResults";
import { InformationCircleIcon } from "@heroicons/react/24/outline";

type BackendError = {
  message: string;
  type: "validation" | "not_found" | "permission" | "general";
  suggestion?: string;
};

interface BulkUploadSectionProps {
  // CSV State
  csvFile: File | null;
  setCsvFile: (file: File | null) => void;
  isParsing: boolean;
  setIsParsing: (parsing: boolean) => void;
  setParsedCsvData: (data: ParsedCsvRow[]) => void;
  csvParseErrors: string[];
  setCsvParseErrors: (errors: string[]) => void;

  // Validation State
  validationResult: ValidationResult | null;
  setValidationResult: (result: ValidationResult | null) => void;
  isValidating: boolean;
  setIsValidating: (validating: boolean) => void;

  // Upload State
  isUploading: boolean;
  setShowUploadConfirm: (show: boolean) => void;
  backendErrors: BackendError[];
  setBackendErrors: (errors: BackendError[]) => void;
  showPreview: boolean;
  setShowPreview: (show: boolean) => void;
  uploadProgress: number;

  // Project Resources
  projectId: string;
  dataSourceId: string;
  organizationId?: number;

  // For confirmation modal
  projects: ProjectResponseDto[];
  dataSources: DataSourceResponseDto[];
}

export default function BulkUploadSection(props: BulkUploadSectionProps) {
  const { t } = useLanguage();

  const handleCsvUpload = async (file: File) => {
    props.setCsvFile(file);
    props.setIsParsing(true);
    props.setParsedCsvData([]);
    props.setCsvParseErrors([]);
    props.setValidationResult(null);
    props.setBackendErrors([]);

    try {
      const parseResult = await parseCsvFile(file);

      if (parseResult.success) {
        props.setParsedCsvData(parseResult.data);
        toast.success(
          `${t.translations.SUCESSFULLY_PARSED} ${parseResult.data.length} ${t.translations.ROWS_FROM_CSV}`
        );

        if (!props.projectId || !props.dataSourceId || !props.organizationId) {
          toast.error(`${t.translations.PLEASE_SELECT_PROJECT_AND_DATASOURCE}`);
          props.setIsParsing(false);
          return;
        }

        props.setIsValidating(true);

        try {
          const validationResult = validateCsvRecords(
            parseResult.data,
            props.projectId,
            props.dataSourceId,
            props.organizationId
          );

          props.setValidationResult(validationResult);

          if (validationResult.isValid) {
            toast.success(
              `${t.translations.ALL} ${validationResult.validCount} ${t.translations.RECORDS_VALIDATED_SUCCESSFULLY}`
            );
          } else {
            toast.error(
              `${t.translations.VALIDATION_FAILED}: ${validationResult.invalidCount} ${t.translations.OF} ${validationResult.totalRows} ${t.translations.RECORDS_HAVE_ERRORS}`
            );
          }
        } catch (error) {
          console.error("Validation error:", error);
          toast.error(t.translations.ERROR_VALIDATING_RECORDS);
        } finally {
          props.setIsValidating(false);
        }
      } else {
        props.setCsvParseErrors(parseResult.errors);
        toast.error(t.translations.FAILED_TO_PARSE_CSV_FILE);
      }
    } catch (error) {
      console.error("Error parsing CSV:", error);
      props.setCsvParseErrors(["Unexpected error while parsing CSV file"]);
      toast.error(t.translations.ERROR_PARSING_CSV_FILE);
    } finally {
      props.setIsParsing(false);
    }
  };

  return (
    <>
      {/* Info Box */}
      <div className="bg-info/10 p-6 rounded-lg space-y-4 border border-info/20">
        <div className="flex items-start gap-3">
          <InformationCircleIcon className="size-6" />
          <div>
            <h3 className="font-semibold text-base-content">
              {t.translations.BULK_METADATA_UPLOAD || "Bulk Metadata Upload"}
            </h3>
            <p className="text-sm text-base-content/70 mt-1">
              {t.translations.BULK_METADATA_INSTRUCTIONS ||
                "Create multiple records at once by uploading a CSV file with metadata."}
            </p>
          </div>
        </div>

        {/* Steps */}
        <div className="flex flex-col gap-3">
          {/* Step 1: Download Template */}
          <label className="label flex-col items-start">
            <span className="label-text font-semibold">
              {t.translations.STEP_1_DOWNLOAD_TEMPLATE}
            </span>
            <CsvTemplateDownload />
          </label>

          {/* Step 2: Upload CSV */}
          <div>
            <label className="label flex-col items-start">
              <span className="label-text font-semibold">
                {t.translations.STEP_2_UPLOAD_YOUR_CSV}
              </span>
              <input
                type="file"
                accept=".csv"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file) handleCsvUpload(file);
                }}
                className="file-input file-input-bordered file-input-primary w-full max-w-xs"
                disabled={
                  props.isParsing ||
                  props.isValidating ||
                  !props.projectId ||
                  !props.dataSourceId
                }
              />
            </label>
            {props.csvFile && (
              <div className="mt-2 text-sm text-base-content/70 flex items-center gap-2">
                <span>
                  {t.translations.SELECTED}:{" "}
                  <span className="font-semibold">{props.csvFile.name}</span>
                </span>
                {(!props.projectId || !props.dataSourceId) && (
                  <div className="badge badge-warning badge-sm">
                    {t.translations.PLEASE_SELECT_PROJECT_AND_DATASOURCE}
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Validation Results */}
      {props.csvFile && <ValidationResults {...props} />}
    </>
  );
}
