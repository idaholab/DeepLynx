"use client";

import { useState, useCallback } from "react";
import { FileMetadata, UploadType, UploadProgressEvent } from "../../types/types";

export type UploadMode = "file" | "bulk";

export function useUploadState() {
  // File Upload Mode State
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
  const [uploadErrorByFileIndex, setUploadErrorByFileIndex] = useState<Record<number, string>>({});

  // Upload Mode Toggle
  const [uploadMode, setUploadMode] = useState<UploadMode>("file");

  // Metadata handler
  const handleMetadataChange = useCallback(
    (fileIndex: number, metadata: FileMetadata) => {
      setFilesMetadata((prev) => ({ ...prev, [fileIndex]: metadata }));
    },
    []
  );

  const setUploadError = useCallback((fileindex: number, message: string) =>{
    setUploadErrorByFileIndex(prev => ({...prev, [fileindex]: message}));
  }, []);

  const cleanUploadError = useCallback((fileIndex: number) => {
    setUploadErrorByFileIndex(prev => {
      const next = {...prev};
      delete next[fileIndex];
      return next;
    })
  }, []);

  const setAllFilesMetadata = useCallback(
    (metadata: Record<number, FileMetadata>) => {
      setFilesMetadata(metadata);
    }, []);

  const setAllUploadErrors = useCallback(
    (errors: Record<number, string>) => {
      setUploadErrorByFileIndex(errors);
    }, []);

  // File management
  const removeAt = useCallback((idx: number) => {
    setSelectedFiles((prev) => prev.filter((_, i) => i !== idx));
    setFilesMetadata((prev) => {
      const next: Record<number, FileMetadata> = {};
      Object.entries(prev).forEach(([index, metadata]) => {
        const numericIndex = Number(index);
        if (numericIndex < idx) next[numericIndex] = metadata;
        if (numericIndex > idx) next[numericIndex - 1] = metadata;
      });
      return next;
    });
    setUploadErrorByFileIndex((prev) => {
      const next: Record<number, string> = {};
      Object.entries(prev).forEach(([index, message]) => {
        const numericIndex = Number(index);
        if (numericIndex < idx) next[numericIndex] = message;
        if (numericIndex > idx) next[numericIndex - 1] = message;
      });
      return next;
    });
  }, []);

  const clearAll = useCallback(() => {
    setSelectedFiles([]);
    setFilesMetadata({});
    setUploadErrorByFileIndex({});
  }, []);

  // Reset form
  const resetFileUpload = () => {
    setSelectedFiles([]);
    setUploadType("new");
    setDestination("");
    setTargetFileId("");
    setFilesMetadata({});
    setUploadErrorByFileIndex({});
    setDropKey((k) => k + 1);
    setUploadProgress(null);
    setIsCancelling(false);
  };

  return {
    // State
    uploadType,
    targetFileId,
    destination,
    selectedFiles,
    filesMetadata,
    uploadErrorByFileIndex,
    dropKey,
    uploadMode,
    isUploading,
    uploadProgress,
    currentUploadId,
    isCancelling,
    
    
    // Setters
    setUploadType,
    setTargetFileId,
    setDestination,
    setSelectedFiles,
    setUploadMode,
    setIsUploading,
    setUploadProgress,
    setCurrentUploadId,
    setIsCancelling,
    setUploadError,
    setAllFilesMetadata,
    setAllUploadErrors,
    
    // Methods
    handleMetadataChange,
    removeAt,
    clearAll,
    resetFileUpload,
    cleanUploadError,
  };
}
