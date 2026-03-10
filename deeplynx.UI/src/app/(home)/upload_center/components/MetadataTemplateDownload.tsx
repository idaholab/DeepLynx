"use client";

import { useLanguage } from "@/app/contexts/Language";
import { ArrowDownTrayIcon } from "@heroicons/react/24/outline";

const TEMPLATE = {
  Name: "example_file_name",
  Description: "Describe this file",
  OriginalId: "unique-original-id",
  ClassName: "optional: Class Example",
  ClassId: "optional: 123",
  Properties: {
    exampleKey: "exampleValue",
  },
};

export default function MetadataTemplateDownload() {
  const { t } = useLanguage();

  const downloadTemplate = () => {
    const json = `${JSON.stringify(TEMPLATE, null, 2)}\n`;
    const blob = new Blob([json], { type: "application/json;charset=utf-8;" });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "file_upload_metadata_template.json";
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  };

  return (
    <button
      type="button"
      className="btn btn-xs btn-outline gap-1"
      onClick={downloadTemplate}
    >
      <ArrowDownTrayIcon className="size-4" />
      {t.translations.DOWNLOAD_METADATA_TEMPLATE}
    </button>
  );
}
