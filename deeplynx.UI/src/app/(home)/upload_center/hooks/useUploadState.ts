"use client";

import { useState, useCallback } from "react";
import { FileMetadata, UploadType, UploadProgressEvent } from "../../types/types";

export type UploadMode = "file" | "bulk";

export function useUploadState() {
  // File Upload Mode State
  const [multi, setMulti] = useState(false);
  const [showMultiFileWarning, setShowMultiFileWarning] = useState(false);
  const [uploadType, setUploadType] = useState<UploadType>("new");
  const [targetFileId, setTargetFileId] = useState("");
  const [destination, setDestination] = useState("");
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [filesMetadata, setFilesMetadata] = useState<Record<number, FileMetadata>>({});
  const [dropKey, setDropKey] = useState(0);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState<UploadProgressEvent | null>(null);
  const [currentUploadId, setCurrentUploadId] = useState<string | null>(null);
  const [isCancelling, setIsCancelling] = useState(false);

  // Upload Mode Toggle
  const [uploadMode, setUploadMode] = useState<UploadMode>("file");

  // Metadata handler
  const handleMetadataChange = useCallback(
    (fileIndex: number, metadata: FileMetadata) => {
      setFilesMetadata((prev) => ({ ...prev, [fileIndex]: metadata }));
    },
    []
  );

  // File management
  const removeAt = (idx: number) =>
    setSelectedFiles((prev) => prev.filter((_, i) => i !== idx));
  
  const clearAll = () => setSelectedFiles([]);

  // Reset form
  const resetFileUpload = () => {
    setSelectedFiles([]);
    setUploadType("new");
    setDestination("");
    setTargetFileId("");
    setMulti(false);
    setFilesMetadata({});
    setDropKey((k) => k + 1);
    setUploadProgress(null);
    setIsCancelling(false);
  };

  return {
    // State
    multi,
    showMultiFileWarning,
    uploadType,
    targetFileId,
    destination,
    selectedFiles,
    filesMetadata,
    dropKey,
    uploadMode,
    isUploading,
    uploadProgress,
    currentUploadId,
    isCancelling,
    
    
    // Setters
    setMulti,
    setShowMultiFileWarning,
    setUploadType,
    setTargetFileId,
    setDestination,
    setSelectedFiles,
    setUploadMode,
    setIsUploading,
    setUploadProgress,
    setCurrentUploadId,
    setIsCancelling,
    
    // Methods
    handleMetadataChange,
    removeAt,
    clearAll,
    resetFileUpload,
  };
}