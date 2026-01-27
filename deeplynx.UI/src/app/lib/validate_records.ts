import { CsvRowSchema } from "../schemas/bulk_record_schema";
import { ParsedCsvRow, ValidationResult, ValidationError, BulkRecord } from "../(home)/types/bulk_upload_types";

/**
 * Validates parsed CSV rows using Zod schema
 * Returns validated records and detailed error information
 */
export function validateCsvRecords(
  parsedRows: ParsedCsvRow[],
  projectId: string,
  dataSourceId: string,
  organizationId: number
): ValidationResult {
  const validRecords: BulkRecord[] = [];
  const errors: ValidationError[] = [];

  parsedRows.forEach((row, index) => {
    const rowNumber = index + 1; // Human-readable row number (1-indexed)

    try {
      // Validate with Zod
      const result = CsvRowSchema.safeParse(row);

      if (!result.success) {
        // Collect all Zod validation errors for this row
        const rowErrors = result.error.issues.map((err) => {
          const field = err.path.join(".");
          return `${field}: ${err.message}`;
        });

        errors.push({
          row: rowNumber,
          recordName: row.name || `Row ${rowNumber}`,
          errors: rowErrors,
        });
      } else {
        // Transform validated data to API format (snake_case)
        const validatedData = result.data;

        const apiRecord: BulkRecord = {
          name: validatedData.name,
          description: validatedData.description,
          original_id: validatedData.original_id,
          properties: validatedData.properties,
          uri: validatedData.uri,
          object_storage_id: validatedData.object_storage_id,
          class_id: validatedData.class_id,
          class_name: validatedData.class_name,
          file_type: validatedData.file_type,
          tags: validatedData.tags,
          sensitivity_labels: validatedData.sensitivity_labels,
          data_source_id: Number(dataSourceId),
          project_id: Number(projectId),
          organization_id: organizationId,
          is_archived: false,
          last_updated_at: new Date().toISOString(),
        };

        validRecords.push(apiRecord);
      }
    } catch (error) {
      // Catch any unexpected errors during validation
      errors.push({
        row: rowNumber,
        recordName: row.name || `Row ${rowNumber}`,
        errors: [
          `Unexpected error: ${
            error instanceof Error ? error.message : "Unknown error"
          }`,
        ],
      });
    }
  });

  return {
    isValid: errors.length === 0,
    validRecords,
    errors,
    totalRows: parsedRows.length,
    validCount: validRecords.length,
    invalidCount: errors.length,
  };
}

/**
 * Format validation errors for display
 */
export function formatValidationErrors(errors: ValidationError[]): string {
  return errors
    .map((error) => {
      const errorList = error.errors.join(", ");
      return `Row ${error.row} (${error.recordName}): ${errorList}`;
    })
    .join("\n");
}

/**
 * Export validation errors as CSV for users to download
 */
export function exportValidationErrorsCsv(errors: ValidationError[]): string {
  const headers = ["Row", "Record Name", "Errors"];
  const rows = errors.map((error) => [
    error.row.toString(),
    error.recordName,
    error.errors.join("; "),
  ]);

  const csvContent = [
    headers.join(","),
    ...rows.map((row) =>
      row.map((cell) => `"${cell.replace(/"/g, '""')}"`).join(",")
    ),
  ].join("\n");

  return csvContent;
}
