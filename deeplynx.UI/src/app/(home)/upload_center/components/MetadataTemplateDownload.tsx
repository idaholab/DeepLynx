"use client";

import { useLanguage } from "@/app/contexts/Language";
import { ArrowDownTrayIcon } from "@heroicons/react/24/outline";

export default function MetadataTemplateDownload() {
  const { t } = useLanguage();
  const template = {
    Name: "example_file_name",
    Description: "Describe this file",
    OriginalId: "unique-original-id",
    ClassName: "optional: Class Example",
    ClassId: null,
    Properties: {
      exampleKey: "exampleValue",
    },
  };

  const downloadTemplate = () => {
    const json = `${JSON.stringify(template, null, 2)}\n`;
    const blob = new Blob([json], { type: "application/json;charset=utf-8;" });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = t.translations.METADATA_TEMPLATE_FILENAME;
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
