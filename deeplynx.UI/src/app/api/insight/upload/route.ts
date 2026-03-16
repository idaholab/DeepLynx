import { NextRequest, NextResponse } from "next/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

function getInsightBaseUrl(): string {
  const raw = process.env.INSIGHT_API_URL || "http://localhost:5009";
  return raw.replace(/\/+$/, "");
}

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const baseUrl = getInsightBaseUrl();
    const targetUrl = `${baseUrl}/upload_document`;

    const upstreamResponse = await fetch(targetUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
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
          message: "Insight upload failed",
          status: upstreamResponse.status,
          details: responseBody,
        },
        { status: upstreamResponse.status },
      );
    }

    return NextResponse.json(responseBody, { status: upstreamResponse.status });
  } catch (error) {
    console.error("Insight upload proxy error:", error);
    return NextResponse.json(
      {
        message:
          error instanceof Error
            ? error.message
            : "Unexpected insight upload proxy error",
      },
      { status: 500 },
    );
  }
}
