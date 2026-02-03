// src/app/api/organization/[organizationId]/logo/file/route.ts

import { NextRequest, NextResponse } from "next/server";
import { access, readFile } from "fs/promises";
import { constants } from "fs";
import path from "path";

const getLogoDir = (): string => {
  if (process.env.LOGO_STORAGE_DIRECTORY) {
    return process.env.LOGO_STORAGE_DIRECTORY;
  }

  if (process.env.STORAGE_DIRECTORY) {
    return path.join(process.env.STORAGE_DIRECTORY, "images");
  }

  return path.join(process.cwd(), "public", "images");
};

const extensionToMime: Record<string, string> = {
  png: "image/png",
  jpg: "image/jpeg",
  jpeg: "image/jpeg",
  svg: "image/svg+xml",
  webp: "image/webp",
};

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ organizationId: string }> }
) {
  try {
    const { organizationId } = await params;

    if (!organizationId || isNaN(Number(organizationId))) {
      return NextResponse.json(
        { message: "Invalid organization ID" },
        { status: 400 }
      );
    }

    const logoDir = getLogoDir();
    const possibleExtensions = ["png", "jpg", "jpeg", "svg", "webp"];

    for (const ext of possibleExtensions) {
      const filePath = path.join(logoDir, `org-${organizationId}-logo.${ext}`);
      try {
        await access(filePath, constants.F_OK);
        const buffer = await readFile(filePath);
        return new NextResponse(buffer, {
          status: 200,
          headers: {
            "Content-Type": extensionToMime[ext] ?? "application/octet-stream",
            "Cache-Control": "public, max-age=3600",
          },
        });
      } catch {
        continue;
      }
    }

    return NextResponse.json({ message: "Logo not found" }, { status: 404 });
  } catch (error) {
    console.error("Error serving organization logo:", error);
    return NextResponse.json(
      {
        message:
          error instanceof Error ? error.message : "Failed to serve logo",
      },
      { status: 500 }
    );
  }
}
