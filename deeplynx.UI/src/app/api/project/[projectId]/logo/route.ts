// src/app/api/project/[projectId]/logo/route.ts

import { NextRequest, NextResponse } from "next/server";
import { writeFile, unlink, access } from "fs/promises";
import path from "path";
import { constants } from "fs";

// Maximum file size: 5MB
const MAX_FILE_SIZE = 5 * 1024 * 1024;

// Allowed MIME types
const ALLOWED_MIME_TYPES = [
  "image/png",
  "image/jpeg",
  "image/jpg",
  "image/svg+xml",
  "image/webp",
];

// Get file extension from MIME type
const getExtensionFromMimeType = (mimeType: string): string => {
  const mimeToExt: Record<string, string> = {
    "image/png": "png",
    "image/jpeg": "jpg",
    "image/jpg": "jpg",
    "image/svg+xml": "svg",
    "image/webp": "webp",
  };
  return mimeToExt[mimeType] || "png";
};

/**
 * POST /api/project/[projectId]/logo
 * Upload project logo
 */
export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ projectId: string }> }
) {
  try {
    const { projectId } = await params;

    // Validate projectId
    if (!projectId || isNaN(Number(projectId))) {
      return NextResponse.json(
        { success: false, message: "Invalid project ID" },
        { status: 400 }
      );
    }

    // Parse form data
    const formData = await request.formData();
    const file = formData.get("file") as File;

    if (!file) {
      return NextResponse.json(
        { success: false, message: "No file provided" },
        { status: 400 }
      );
    }

    // Validate file size
    if (file.size > MAX_FILE_SIZE) {
      return NextResponse.json(
        {
          success: false,
          message: `File size exceeds maximum limit of ${MAX_FILE_SIZE / 1024 / 1024}MB`,
        },
        { status: 400 }
      );
    }

    // Validate MIME type
    if (!ALLOWED_MIME_TYPES.includes(file.type)) {
      return NextResponse.json(
        {
          success: false,
          message: "Invalid file type. Only PNG, JPG, SVG, and WebP are allowed.",
        },
        { status: 400 }
      );
    }

    // Convert file to buffer
    const bytes = await file.arrayBuffer();
    const buffer = Buffer.from(bytes);

    // Determine file extension
    const extension = getExtensionFromMimeType(file.type);

    // Define file path in public/images directory
    const fileName = `project-${projectId}-logo.${extension}`;
    const publicDir = path.join(process.cwd(), "public", "images");
    const filePath = path.join(publicDir, fileName);

    // Remove old logo files with different extensions (if any)
    const possibleExtensions = ["png", "jpg", "jpeg", "svg", "webp"];
    for (const ext of possibleExtensions) {
      if (ext !== extension) {
        const oldFilePath = path.join(publicDir, `project-${projectId}-logo.${ext}`);
        try {
          await unlink(oldFilePath);
        } catch (error) {
          // File doesn't exist, ignore error
        }
      }
    }

    // Write the new file
    await writeFile(filePath, buffer);

    const logoUrl = `/images/${fileName}`;

    return NextResponse.json(
      {
        success: true,
        logoUrl,
        message: "Logo uploaded successfully",
      },
      { status: 200 }
    );
  } catch (error) {
    console.error("Error uploading project logo:", error);
    return NextResponse.json(
      {
        success: false,
        message: error instanceof Error ? error.message : "Failed to upload logo",
      },
      { status: 500 }
    );
  }
}

/**
 * DELETE /api/project/[projectId]/logo
 * Remove project logo
 */
export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ projectId: string }> }
) {
  try {
    const { projectId } = await params;

    // Validate projectId
    if (!projectId || isNaN(Number(projectId))) {
      return NextResponse.json(
        { success: false, message: "Invalid project ID" },
        { status: 400 }
      );
    }

    const publicDir = path.join(process.cwd(), "public", "images");

    // Try to delete all possible logo file extensions
    const possibleExtensions = ["png", "jpg", "jpeg", "svg", "webp"];
    let fileDeleted = false;

    for (const ext of possibleExtensions) {
      const filePath = path.join(publicDir, `project-${projectId}-logo.${ext}`);
      try {
        await unlink(filePath);
        fileDeleted = true;
      } catch (error) {
        // File doesn't exist, continue to next extension
        continue;
      }
    }

    if (!fileDeleted) {
      return NextResponse.json(
        { success: false, message: "No logo file found to delete" },
        { status: 404 }
      );
    }

    return NextResponse.json(
      {
        success: true,
        message: "Logo removed successfully",
      },
      { status: 200 }
    );
  } catch (error) {
    console.error("Error removing project logo:", error);
    return NextResponse.json(
      {
        success: false,
        message: error instanceof Error ? error.message : "Failed to remove logo",
      },
      { status: 500 }
    );
  }
}

/**
 * GET /api/project/[projectId]/logo
 * Check if project logo exists
 */
export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ projectId: string }> }
) {
  try {
    const { projectId } = await params;

    // Validate projectId
    if (!projectId || isNaN(Number(projectId))) {
      return NextResponse.json(
        { exists: false, message: "Invalid project ID" },
        { status: 400 }
      );
    }

    const publicDir = path.join(process.cwd(), "public", "images");
    const possibleExtensions = ["png", "jpg", "jpeg", "svg", "webp"];

    // Check if any logo file exists
    for (const ext of possibleExtensions) {
      const filePath = path.join(publicDir, `project-${projectId}-logo.${ext}`);
      try {
        await access(filePath, constants.F_OK);
        return NextResponse.json(
          {
            exists: true,
            logoUrl: `/images/project-${projectId}-logo.${ext}`,
          },
          { status: 200 }
        );
      } catch {
        // File doesn't exist, continue to next extension
        continue;
      }
    }

    return NextResponse.json(
      { exists: false, logoUrl: null },
      { status: 200 }
    );
  } catch (error) {
    console.error("Error checking project logo:", error);
    return NextResponse.json(
      {
        exists: false,
        message: error instanceof Error ? error.message : "Failed to check logo",
      },
      { status: 500 }
    );
  }
}