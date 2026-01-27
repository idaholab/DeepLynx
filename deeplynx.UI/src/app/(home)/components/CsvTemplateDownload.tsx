"use client";

import { useLanguage } from "@/app/contexts/Language";
import { ArrowDownTrayIcon } from "@heroicons/react/24/outline";

export default function CsvTemplateDownload() {
  const { t } = useLanguage();

  const downloadTemplate = () => {
    // CSV Header with field descriptions
    const headers = [
      "name (required)",
      "description (required)",
      "original_id (required)",
      "properties (required - JSON format)",
      "uri (optional)",
      "object_storage_id (optional)",
      "class_id (optional)",
      "class_name (optional)",
      "file_type (optional)",
      "tags (optional - comma-separated)",
      "sensitivity_labels (optional - comma-separated)",
    ];

    // Instructions (will be shown as comments in first rows)
    const instructions = [
      "# INSTRUCTIONS: Fill in the required fields for each record.",
      "# - Required fields: name, description, original_id, properties",
      '# - Properties must be valid JSON format. Example: {"key": "value", "number": 123}',
      "# - Tags and sensitivity_labels should be comma-separated. Example: tag1,tag2,tag3",
      "# - Optional fields can be left empty",
      "# - Delete these instruction rows before uploading",
      "#",
    ];

    // Example rows with realistic data
    const examples = [
      {
        name: "Assembly 33",
        description: "Assembly 33 Description",
        original_id: "assy-033",
        properties: JSON.stringify({
          description: "Assembly 33 Description",
          "diversion flag": false,
          height: 160.0,
          "number of fuel pins": 72,
          "number of heat pipes": 19,
          temperature: 1000.0,
        }),
        uri: "https://example.com/assembly/33",
        object_storage_id: "1",
        class_id: "5",
        class_name: "FuelAssembly",
        file_type: "csv",
        tags: "monitoring,critical,assembly",
        sensitivity_labels: "public,internal",
      },
      {
        name: "Sensor Reading Alpha",
        description: "Temperature sensor data from reactor core",
        original_id: "sensor-alpha-001",
        properties: JSON.stringify({
          sensor_type: "temperature",
          location: "reactor-core-1",
          measurement_unit: "celsius",
          accuracy: 0.1,
          calibration_date: "2025-01-01",
        }),
        uri: "",
        object_storage_id: "",
        class_id: "",
        class_name: "SensorData",
        file_type: "json",
        tags: "sensor,temperature,realtime",
        sensitivity_labels: "internal,restricted",
      },
      {
        name: "Maintenance Log 2025-01",
        description: "Monthly maintenance log for January 2025",
        original_id: "maint-log-2025-01",
        properties: JSON.stringify({
          month: "January",
          year: 2025,
          total_hours: 120,
          issues_found: 3,
          issues_resolved: 2,
        }),
        uri: "https://example.com/logs/2025-01",
        object_storage_id: "2",
        class_id: "10",
        class_name: "MaintenanceLog",
        file_type: "pdf",
        tags: "maintenance,monthly,2025",
        sensitivity_labels: "internal",
      },
    ];

    // Build CSV content
    let csvContent = "";

    // Add instructions
    csvContent += instructions.join("\n") + "\n";

    // Add headers
    csvContent += headers.join(",") + "\n";

    // Add example rows
    examples.forEach((example) => {
      const row = [
        escapeCsvField(example.name),
        escapeCsvField(example.description),
        escapeCsvField(example.original_id),
        escapeCsvField(example.properties),
        escapeCsvField(example.uri),
        escapeCsvField(example.object_storage_id),
        escapeCsvField(example.class_id),
        escapeCsvField(example.class_name),
        escapeCsvField(example.file_type),
        escapeCsvField(example.tags),
        escapeCsvField(example.sensitivity_labels),
      ];
      csvContent += row.join(",") + "\n";
    });

    // Create blob and trigger download
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `bulk_records_template_${
      new Date().toISOString().split("T")[0]
    }.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  };

  // Helper function to escape CSV fields (handle commas, quotes, newlines)
  const escapeCsvField = (field: string | number | undefined): string => {
    if (field === undefined || field === null || field === "") {
      return "";
    }

    const stringField = String(field);

    // If field contains comma, quote, or newline, wrap in quotes and escape internal quotes
    if (
      stringField.includes(",") ||
      stringField.includes('"') ||
      stringField.includes("\n")
    ) {
      return `"${stringField.replace(/"/g, '""')}"`;
    }

    return stringField;
  };

  return (
    <button
      onClick={downloadTemplate}
      className="btn btn-primary btn-sm gap-2"
      type="button"
    >
      <ArrowDownTrayIcon className="size-6" />
      {t.translations.DOWNLOAD_CSV_TEMPLATE}
    </button>
  );
}
