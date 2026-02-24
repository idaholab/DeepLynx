import { NextRequest, NextResponse } from "next/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

function getInsightBaseUrl(): string {
  const raw = process.env.INSIGHT_API_URL || "http://localhost:5009";
  return raw.replace(/\/+$/, "");
}

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ recordId: string }> },
) {
  try {
    const { recordId } = await params;
    const recordIdNum = Number(recordId);

    if (!Number.isFinite(recordIdNum) || recordIdNum <= 0) {
      return NextResponse.json(
        { message: "Invalid recordId" },
        { status: 400 },
      );
    }

    const baseUrl = getInsightBaseUrl();
    const targetUrl = `${baseUrl}/ingestion_status/${recordIdNum}`;

    const upstreamResponse = await fetch(targetUrl, {
      method: "GET",
      headers: { Accept: "application/json" },
      cache: "no-store",
    });

    const responseText = await upstreamResponse.text();
    let responseBody: unknown = null;

    if (responseText) {
      try {
        responseBody = JSON.parse(responseText);
      } catch {
        responseBody = { message: responseText };
      }
    }

    if (!upstreamResponse.ok) {
      return NextResponse.json(
        {
          message: "Insight status check failed",
          status: upstreamResponse.status,
          details: responseBody,
        },
        { status: upstreamResponse.status },
      );
    }

    return NextResponse.json(responseBody, { status: upstreamResponse.status });
  } catch (error) {
    console.error("Insight status proxy error:", error);
    return NextResponse.json(
      {
        message:
          error instanceof Error
            ? error.message
            : "Unexpected insight status proxy error",
      },
      { status: 500 },
    );
  }
}
