import { z } from "zod";

// Helper schema for validating that a string is valid JSON
const jsonStringSchema = z.string().refine(
  (val) => {
    try {
      JSON.parse(val);
      return true;
    } catch {
      return false;
    }
  },
  { message: "Must be valid JSON format" }
);

// Schema for validating parsed CSV rows (before transformation)
export const CsvRowSchema = z.object({
  name: z
    .string()
    .min(1, "Name is required and cannot be empty")
    .trim(),
  
  description: z
    .string()
    .min(1, "Description is required and cannot be empty")
    .trim(),
  
  original_id: z
    .string()
    .min(1, "Original ID is required and cannot be empty")
    .trim(),
  
  properties: jsonStringSchema.transform((str) => {
    try {
      return JSON.parse(str);
    } catch {
      return {};
    }
  }),
  
  uri: z
    .string()
    .url("Must be a valid URL")
    .optional()
    .or(z.literal(""))
    .transform((val) => (val === "" ? null : val)),
  
  object_storage_id: z
    .string()
    .optional()
    .transform((val) => {
      if (!val || val === "") return null;
      const num = Number(val);
      return isNaN(num) ? null : num;
    }),
  
  class_id: z
    .string()
    .optional()
    .transform((val) => {
      if (!val || val === "") return null;
      const num = Number(val);
      return isNaN(num) ? null : num;
    }),
  
  class_name: z
    .string()
    .optional()
    .transform((val) => (val === "" ? null : val)),
  
  file_type: z
    .string()
    .optional()
    .transform((val) => (val === "" ? null : val)),
  
  tags: z
    .string()
    .optional()
    .transform((val) => {
      if (!val || val.trim() === "") return [];
      return val
        .split(",")
        .map((tag) => tag.trim())
        .filter((tag) => tag.length > 0);
    }),
  
  sensitivity_labels: z
    .string()
    .optional()
    .transform((val) => {
      if (!val || val.trim() === "") return [];
      return val
        .split(",")
        .map((label) => label.trim())
        .filter((label) => label.length > 0);
    }),
});

// Schema for validating the final API record (after transformation)
export const ApiRecordSchema = z.object({
  name: z.string().min(1, "Name is required"),
  description: z.string().min(1, "Description is required"),
  original_id: z.string().min(1, "Original ID is required"),
  properties: z.record(z.string(), z.any()).refine(
    (obj) => Object.keys(obj).length > 0,
    { message: "Properties object cannot be empty" }
  ),
  uri: z.string().url().nullable().optional(),
  object_storage_id: z.number().nullable().optional(),
  class_id: z.number().nullable().optional(),
  class_name: z.string().nullable().optional(),
  file_type: z.string().nullable().optional(),
  tags: z.array(z.string()).optional(),
  sensitivity_labels: z.array(z.string()).optional(),
  data_source_id: z.number().optional(),
  project_id: z.number().optional(),
  organization_id: z.number().optional(),
  is_archived: z.boolean().optional(),
  last_updated_at: z.string().optional(),
});

// Type inference from schemas
export type ValidatedCsvRow = z.infer<typeof CsvRowSchema>;
export type ValidatedApiRecord = z.infer<typeof ApiRecordSchema>;
