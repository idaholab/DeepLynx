import { z } from "zod";

type MetadataSchemaTranslations = {
  NAME_REQUIRED: string;
  DESCRIPTION_REQUIRED: string;
  ORIGINAL_ID_REQUIRED: string;
  CLASS_ID_MUST_BE_NUMBER_NOT_STRING: string;
  CLASS_ID_MUST_BE_INTEGER: string;
  CLASS_ID_MUST_BE_GREATER_THAN_ZERO: string;
};

export const createMetadataUploadSchema = (
  t: MetadataSchemaTranslations,
) =>
  z.object({
    Name: z.string().trim().min(1, t.NAME_REQUIRED),
    Description: z.string().trim().min(1, t.DESCRIPTION_REQUIRED),
    OriginalId: z.string().trim().min(1, t.ORIGINAL_ID_REQUIRED),
    ClassName: z.string().trim().min(1).optional(),
    ClassId: z
      .number({
        error: t.CLASS_ID_MUST_BE_NUMBER_NOT_STRING,
      })
      .int(t.CLASS_ID_MUST_BE_INTEGER)
      .positive(t.CLASS_ID_MUST_BE_GREATER_THAN_ZERO)
      .optional(),
    Properties: z
      .record(z.string(), z.unknown())
      .or(z.object({}).catchall(z.unknown())),
  });
