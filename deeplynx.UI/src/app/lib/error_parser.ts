/**
 * Parses backend error messages and extracts user-friendly information
 * Handles C# stack traces and various error formats
 */
export function parseBackendError(error: string): {
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
    cleanMessage = `Object Storage ID ${id} does not exist in this project`;
    suggestion = "Check that the object_storage_id values in your CSV are valid for the selected project, or leave them empty if not needed.";
  }
  // Class errors
  else if (/class.*does not exist|class.*not found/i.test(cleanMessage)) {
    type = "not_found";
    const idMatch = cleanMessage.match(/ID\s+(\d+)/i);
    const id = idMatch ? idMatch[1] : "specified";
    cleanMessage = `Class ID ${id} does not exist in this project`;
    suggestion = "Verify that the class_id values in your CSV match existing classes in your project.";
  }
  // Duplicate errors
  else if (/already exists|duplicate/i.test(cleanMessage)) {
    type = "validation";
    suggestion = "Check your CSV for duplicate original_id values, or verify that these records don't already exist in the system.";
  }
  // Permission errors
  else if (/permission|unauthorized|forbidden/i.test(cleanMessage)) {
    type = "permission";
    suggestion = "Contact your project administrator to request the necessary permissions.";
  }
  // Validation errors
  else if (/invalid|required|must be|cannot be/i.test(cleanMessage)) {
    type = "validation";
    suggestion = "Review the error message and correct the affected fields in your CSV.";
  }
  // Relationship/Edge errors
  else if (/relationship.*does not exist/i.test(cleanMessage)) {
    type = "not_found";
    suggestion = "Verify that relationship IDs in your CSV exist in the selected project.";
  }
  // Data source errors
  else if (/data source.*does not exist/i.test(cleanMessage)) {
    type = "not_found";
    cleanMessage = "The selected data source is invalid";
    suggestion = "Try selecting a different data source from the dropdown.";
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
export function parseBackendErrors(errors: string[]): Array<{
  message: string;
  type: "validation" | "not_found" | "permission" | "general";
  suggestion?: string;
}> {
  return errors.map(parseBackendError);
}