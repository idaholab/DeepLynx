// src/app/lib/client_service/csv_parser.ts

import Papa from "papaparse";
import { ParsedCsvRow } from "@/app/(home)/types/bulk_upload_types";

export interface CsvParseResult {
  success: boolean;
  data: ParsedCsvRow[];
  errors: string[];
  fileName: string;
}

/**
 * Parses a CSV file and returns structured data
 * Skips instruction rows (starting with #)
 * Handles empty optional fields
 */
export async function parseCsvFile(file: File): Promise<CsvParseResult> {
  return new Promise((resolve) => {
    const errors: string[] = [];

    // Validate file type
    if (!file.name.endsWith(".csv")) {
      resolve({
        success: false,
        data: [],
        errors: ["File must be a CSV file (.csv extension)"],
        fileName: file.name,
      });
      return;
    }

    Papa.parse<Record<string, string>>(file, {
      header: true,
      skipEmptyLines: true,
      transformHeader: (header: string) => {
        // Clean up header by removing everything in parentheses and after dashes
        return header
            .split("(")[0]
            .trim();
        },
      complete: (results) => {
        try {
            // DEBUG: Log the headers we're receiving
            console.log("CSV Headers:", Object.keys(results.data[0] || {}));
            console.log("First row:", results.data[0]);

            // Filter out instruction rows (rows that start with #)
            const dataRows = results.data.filter((row) => {
            const firstValue = Object.values(row)[0];
            return firstValue && !String(firstValue).startsWith("#");
            });

          if (dataRows.length === 0) {
            resolve({
              success: false,
              data: [],
              errors: [
                "No data rows found in CSV. Make sure to delete instruction rows and include at least one data row.",
              ],
              fileName: file.name,
            });
            return;
          }

          // Transform each row to match our ParsedCsvRow type
          const parsedData: ParsedCsvRow[] = dataRows.map((row, index) => {
            // Helper to get value or empty string
            const getValue = (key: string): string => {
              return row[key]?.trim() || "";
            };

            return {
              name: getValue("name"),
              description: getValue("description"),
              original_id: getValue("original_id"),
              properties: getValue("properties"),
              uri: getValue("uri"),
              object_storage_id: getValue("object_storage_id"),
              class_id: getValue("class_id"),
              class_name: getValue("class_name"),
              file_type: getValue("file_type"),
              tags: getValue("tags"),
              sensitivity_labels: getValue("sensitivity_labels"),
            };
          });

          // Check if we have required headers
          const requiredHeaders = [
            "name",
            "description",
            "original_id",
            "properties",
          ];
          const firstRow = results.data[0];
          const missingHeaders = requiredHeaders.filter(
            (header) => !(header in firstRow)
          );

          if (missingHeaders.length > 0) {
            errors.push(
              `Missing required columns: ${missingHeaders.join(", ")}`
            );
          }

          resolve({
            success: errors.length === 0,
            data: parsedData,
            errors,
            fileName: file.name,
          });
        } catch (error) {
          resolve({
            success: false,
            data: [],
            errors: [
              `Error processing CSV: ${
                error instanceof Error ? error.message : "Unknown error"
              }`,
            ],
            fileName: file.name,
          });
        }
      },
      error: (error) => {
        resolve({
          success: false,
          data: [],
          errors: [`CSV parsing error: ${error.message}`],
          fileName: file.name,
        });
      },
    });
  });
}

/**
 * Helper function to convert empty strings to null for optional fields
 */
export function normalizeEmptyValues(value: string | undefined): string | null {
  if (!value || value.trim() === "") {
    return null;
  }
  return value.trim();
}

/**
 * Helper function to parse comma-separated values into an array
 */
export function parseCommaSeparated(
  value: string | undefined
): string[] | undefined {
  if (!value || value.trim() === "") {
    return undefined;
  }
  return value
    .split(",")
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

/**
 * Helper function to validate and parse JSON string
 */
export function parseJsonString(
  jsonString: string
): {
  valid: boolean;
  data: Record<string, unknown> | null;
  error: string | null;
} {
  if (!jsonString || jsonString.trim() === "") {
    return {
      valid: false,
      data: null,
      error: "Properties field is required and cannot be empty",
    };
  }

  try {
    const parsed = JSON.parse(jsonString);
    
    if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
      return {
        valid: false,
        data: null,
        error: "Properties must be a valid JSON object",
      };
    }

    if (Object.keys(parsed).length === 0) {
      return {
        valid: false,
        data: null,
        error: "Properties object cannot be empty",
      };
    }

    return { valid: true, data: parsed, error: null };
  } catch (error) {
    return {
      valid: false,
      data: null,
      error: `Invalid JSON format: ${
        error instanceof Error ? error.message : "Unknown error"
      }`,
    };
  }
}
