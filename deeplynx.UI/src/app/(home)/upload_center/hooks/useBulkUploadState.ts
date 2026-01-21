"use client";

import { useState } from "react";
import { ParsedCsvRow, ValidationResult } from "../../types/bulk_upload_types";

export function useBulkUploadState() {
  // CSV Upload State
  const [csvFile, setCsvFile] = useState<File | null>(null);
  const [parsedCsvData, setParsedCsvData] = useState<ParsedCsvRow[]>([]);
  const [csvParseErrors, setCsvParseErrors] = useState<string[]>([]);
  const [isParsing, setIsParsing] = useState(false);
  
  // Validation State
  const [validationResult, setValidationResult] = useState<ValidationResult | null>(null);
  const [isValidating, setIsValidating] = useState(false);
  
  // Upload State
  const [isUploading, setIsUploading] = useState(false);
  const [showUploadConfirm, setShowUploadConfirm] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  
  // Backend Errors
  const [backendErrors, setBackendErrors] = useState
    <Array<{
      message: string;
      type: "validation" | "not_found" | "permission" | "general";
      suggestion?: string;
    }>
  >([]);
  
  // Preview
  const [showPreview, setShowPreview] = useState(false);

  // Reset bulk upload
  const resetBulkUpload = () => {
    setCsvFile(null);
    setParsedCsvData([]);
    setValidationResult(null);
    setCsvParseErrors([]);
    setBackendErrors([]);
    setShowPreview(false);
    setUploadProgress(0);
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    if (fileInput) fileInput.value = "";
  };

  return {
    // CSV State
    csvFile,
    parsedCsvData,
    csvParseErrors,
    isParsing,
    
    // Validation State
    validationResult,
    isValidating,
    
    // Upload State
    isUploading,
    showUploadConfirm,
    uploadProgress,
    backendErrors,
    showPreview,
    
    // Setters
    setCsvFile,
    setParsedCsvData,
    setCsvParseErrors,
    setIsParsing,
    setValidationResult,
    setIsValidating,
    setIsUploading,
    setShowUploadConfirm,
    setUploadProgress,
    setBackendErrors,
    setShowPreview,
    
    // Methods
    resetBulkUpload,
  };
}