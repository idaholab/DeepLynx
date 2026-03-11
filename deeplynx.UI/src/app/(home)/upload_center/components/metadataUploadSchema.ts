import { z } from "zod";

export const metadataUploadSchema = z.object({
    Name: z.string().trim().min(1, "Name is required"),
    Description: z.string().trim().min(1, "Description is required"),
    OriginalId: z.string().trim().min(1, "OriginalId is required"),
    ClassName: z.string().trim().min(1).optional(),
    ClassId: z.number({
        error: "ClassId must be a number, not a string.",
    })
    .int("ClassId must be an integer")
    .positive("ClassId must be greater than 0")
    .optional(),
    Properties: z.record(z.string(), z.unknown()).or(z.object({}).catchall(z.unknown()))
})