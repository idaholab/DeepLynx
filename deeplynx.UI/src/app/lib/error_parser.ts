export type BackendErrorTranslationOverrides = {
  objectStorageIdNotFoundInProject?: string;
  objectStorageIdNotFoundSuggestion?: string;
  originalIdAlreadyInUse?: string;
  originalIdAlreadyInUseSuggestion?: string;
  classIdClassNameMismatch?: string;
  classIdClassNameMismatchSuggestion?: string;
  classIdNotFoundInProject?: string;
  classIdNotFoundSuggestion?: string;
  jsonDepthExceeded?: string;
  jsonDepthExceededSuggestion?: string;
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
  // Class name / Class Id mismatch errors
  else if (/class name .* does not match class id|does not match class id/i.test(cleanMessage)) {
    type = "validation";
    const idMatch = cleanMessage.match(/ID\s+(\d+)/i);
    const id = idMatch ? idMatch[1] : "specified";
    const nameMatch = cleanMessage.match(/Class Name\s+(.+?)\s+does not match/i);
    const name = nameMatch ? nameMatch[1] : "specified";
    cleanMessage = translations?.classIdClassNameMismatch 
      ? translations.classIdClassNameMismatch.replace("{id}", id).replace("{name}", name)
      : `Class Name ${name} does not match Class ID ${id}.`;
    suggestion =
      translations?.classIdClassNameMismatchSuggestion ??
      "Update the metadata so the Class Name matches the Class Id."
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
  // Max Depth errors
  else if (/depth of the json structure exceeds the maximum allowed depth/i.test(cleanMessage)) {
    type = "validation";
    const allowedMatch = cleanMessage.match(/maximum allowed depth of (\d+)/i);
    const currentMatch = cleanMessage.match(/current depth of properties is (\d+)/i);
    const allowedDepth = allowedMatch ? allowedMatch[1]: "specified";
    const currentDepth = currentMatch ? currentMatch[1]: "specified";
    cleanMessage = translations?.jsonDepthExceeded
      ? translations.jsonDepthExceeded.replace("{allowedDepth}", allowedDepth).replace("{currentDepth}", currentDepth)
      : `The JSON structure exceeds the maximum allowed depth of ${allowedDepth}. Current depth of properties is ${currentDepth}.`
    suggestion =
      translations?.jsonDepthExceededSuggestion ??
      "Reduce the nesting of objects or arrays in the properties section of the metadata.";
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
