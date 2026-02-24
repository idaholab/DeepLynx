// src/app/(home)/components/NewFileUploadCard.tsx

"use client";
import { FileMetadata } from "../types/types";
import { useLanguage } from "@/app/contexts/Language";
import { useEffect, useState } from "react";

type UploadType = "new" | "version" | "properties" | "";


interface NewFileUploadCardProps {
  defaultName?: string;
  uploadType: UploadType;
  fileIndex: number;
  onMetadataChange: (fileIndex: number, metadata: FileMetadata) => void;
}

export default function NewFileUploadCard({
  defaultName = "",
  uploadType,
  fileIndex,
  onMetadataChange,
}: NewFileUploadCardProps) {
  const { t } = useLanguage();
  const [updateAction, setUpdateAction] = useState<"" | "merge" | "overwrite">(
    ""
  );
  const [description] = useState("");
  const [isTimeSeries, setIsTimeSeries] = useState(false);
  const fileBaseName = (filename: string) => filename.replace(/\.[^/.]+$/, "");
  const [name, setName] = useState(fileBaseName(defaultName));
  const showUpdate = uploadType === "version" || uploadType === "properties";

  useEffect(() => {
    if (!showUpdate && updateAction) setUpdateAction("");
    setName(fileBaseName(defaultName));
  }, [defaultName, showUpdate, updateAction]);

  useEffect(() => {
    const metadata: FileMetadata = {
      name,
      description,
      isTimeSeries,
      ...(showUpdate &&
        updateAction && {
        updateAction: updateAction as "merge" | "overwrite",
      }),
    };
    onMetadataChange(fileIndex, metadata);
  }, [
    name,
    description,
    isTimeSeries,
    updateAction,
    showUpdate,
    fileIndex,
    onMetadataChange,
  ]);

  return (
    <div>
      {/* Show original file name above the card */}
      <h4 className="px-2">{defaultName}</h4>

      <div className="card card-border">
        <div className="card-body w-full space-y-4">
          {/* Row 1: Time Series toggle + Name input */}
          <div className="grid grid-cols-[auto,1fr] items-center gap-4">
            <div className="flex items-center">
              <span className="label-text mr-2">
                {t.translations.TIMESERIES}
              </span>
              <input
                type="checkbox"
                className="toggle toggle-secondary"
                checked={isTimeSeries}
                onChange={(e) => setIsTimeSeries(e.target.checked)}
              />
              <label className="flex items-center gap-2 flex-1">
                <span className="label-text ml-4">{t.translations.ALIAS}</span>
                <input
                  type="text"
                  className="input input-sm w-full"
                  placeholder="metadata.a"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </label>
            </div>
          </div>

          {/* Row 2: Description textarea */}
          {/* <div className="grid grid-cols-[auto,1fr] items-start gap-4">
            <div className="flex">
              <span className="label-text mr-2">
                {t.translations.DESCRIPTION}
              </span>
              <textarea
                className="textarea textarea-bordered w-full"
                placeholder="Example: This file contains ..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              ></textarea>
            </div> */}
          {/* Row 3: Update Existing */}
          {/* {showUpdate && (
              <fieldset>
                <label className="label">
                  {t.translations.UPDATE_EXISTING}
                  <select
                    className="select select-info select-sm mt-2"
                    value={updateAction}
                    onChange={(e) =>
                      setUpdateAction(e.target.value as "merge" | "overwrite")
                    }
                    required
                  >
                    <option value="" disabled>
                      {t.translations.CHOOSE_OPTION}
                    </option>
                    <option value="nexus">{t.translations.MERGE}</option>
                    <option value="remote-db">
                      {t.translations.OVERWRITE}
                    </option>
                  </select>
                </label>
              </fieldset>
            )}
          </div> */}
        </div>
      </div>
    </div>
  );
}
