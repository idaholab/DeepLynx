export type BackendErrorTranslationOverrides = {
  objectStorageIdNotFoundInProject?: string;
  objectStorageIdNotFoundSuggestion?: string;
  originalIdAlreadyInUse?: string;
  originalIdAlreadyInUseSuggestion?: string;
  classIdNotFoundInProject?: string;
  classIdNotFoundSuggestion?: string;
  duplicateSuggestion?: string;
  permissionSuggestion?: string;
  validationSuggestion?: string;
  relationshipSuggestion?: string;
  invalidSelectedDataSource?: string;
  invalidSelectedDataSourceSuggestion?: string;
};

/**
 * Parses backend error messages and extracts user-friendly information
 * Handles C# stack traces and various error formats
 */
export function parseBackendError(
  error: string,
  translations?: BackendErrorTranslationOverrides,
): {
  message: string;
  type: "validation" | "not_found" | "permission" | "general";
  suggestion?: string;
} {
  // Remove C# stack trace (everything after "at deeplynx" or "\n at ")
  let cleanMessage = error
    .split(/\n\s*at\s+/)[0] // Remove stack trace
    .replace(/^["']|["']$/g, "") // Remove surrounding quotes
    .trim();

  // Extract the actual error message from common patterns
  const patterns = [
    /An error occurred while parsing metadata:\s*([^:]+):\s*(.+)/i,
    /System\.Collections\.Generic\.KeyNotFoundException:\s*(.+)/i,
    /System\.ArgumentException:\s*(.+)/i,
    /System\.InvalidOperationException:\s*(.+)/i,
    /Error:\s*(.+)/i,
  ];

  for (const pattern of patterns) {
    const match = cleanMessage.match(pattern);
    if (match) {
      cleanMessage = match[match.length - 1].trim();
      break;
    }
  }

  // Determine error type and add helpful suggestions
  let type: "validation" | "not_found" | "permission" | "general" = "general";
  let suggestion: string | undefined;

  // Object Storage errors
  if (/object storage.*does not exist/i.test(cleanMessage)) {
    type = "not_found";
    const idMatch = cleanMessage.match(/ID\s+(\d+)/i);
    const id = idMatch ? idMatch[1] : "specified";
    cleanMessage = translations?.objectStorageIdNotFoundInProject
      ? translations.objectStorageIdNotFoundInProject.replace("{id}", id)
      : `Object Storage ID ${id} does not exist in this project`;
    suggestion =
      translations?.objectStorageIdNotFoundSuggestion ??
      "Check that the selected object storage ID is valid for this project.";
  }
  // OriginalId uniqueness errors
  else if (
    /unique_record_original_id|duplicate key value violates unique constraint.*original_id|original_id/i.test(
      cleanMessage,
    )
  ) {
    type = "validation";
    cleanMessage =
      translations?.originalIdAlreadyInUse ??
      "OriginalId is already in use. Each uploaded file must have a unique OriginalId.";
    suggestion =
      translations?.originalIdAlreadyInUseSuggestion ??
      "Update the metadata with a unique OriginalId, or remove OriginalId to let the system generate one.";
  }
  // Class errors
  else if (/class.*does not exist|class.*not found/i.test(cleanMessage)) {
    type = "not_found";
    const idMatch = cleanMessage.match(/ID\s+(\d+)/i);
    const id = idMatch ? idMatch[1] : "specified";
    cleanMessage = translations?.classIdNotFoundInProject
      ? translations.classIdNotFoundInProject.replace("{id}", id)
      : `Class ID ${id} does not exist in this project`;
    suggestion =
      translations?.classIdNotFoundSuggestion ??
      "Verify that the class ID exists in the selected project.";
  }
  // Duplicate errors
  else if (/already exists|duplicate/i.test(cleanMessage)) {
    type = "validation";
    suggestion =
      translations?.duplicateSuggestion ??
      "Check for duplicate IDs, or verify that the record does not already exist in the system.";
  }
  // Permission errors
  else if (/permission|unauthorized|forbidden/i.test(cleanMessage)) {
    type = "permission";
    suggestion =
      translations?.permissionSuggestion ??
      "Contact your project administrator to request the necessary permissions.";
  }
  // Validation errors
  else if (/invalid|required|must be|cannot be/i.test(cleanMessage)) {
    type = "validation";
    suggestion =
      translations?.validationSuggestion ??
      "Review the error message and correct the affected fields.";
  }
  // Relationship/Edge errors
  else if (/relationship.*does not exist/i.test(cleanMessage)) {
    type = "not_found";
    suggestion =
      translations?.relationshipSuggestion ??
      "Verify that the relationship IDs exist in the selected project.";
  }
  // Data source errors
  else if (/data source.*does not exist/i.test(cleanMessage)) {
    type = "not_found";
    cleanMessage =
      translations?.invalidSelectedDataSource ??
      "The selected data source is invalid";
    suggestion =
      translations?.invalidSelectedDataSourceSuggestion ??
      "Try selecting a different data source from the dropdown.";
  }

  return {
    message: cleanMessage,
    type,
    suggestion,
  };
}

/**
 * Parses multiple backend errors
 */
export function parseBackendErrors(
  errors: string[],
  translations?: BackendErrorTranslationOverrides,
): Array<{
  message: string;
  type: "validation" | "not_found" | "permission" | "general";
  suggestion?: string;
}> {
  return errors.map((error) => parseBackendError(error, translations));
}
