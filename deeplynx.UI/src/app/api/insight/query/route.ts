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
    const targetUrl = `${baseUrl}/query`;

    const upstreamResponse = await fetch(targetUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "text/plain",
      },
      body: JSON.stringify(body),
      cache: "no-store",
    });

    if (!upstreamResponse.ok) {
      const errorBody = await upstreamResponse.text();
      return NextResponse.json(
        {
          message: "Insight query failed",
          status: upstreamResponse.status,
          details: errorBody || null,
        },
        { status: upstreamResponse.status },
      );
    }

    if (!upstreamResponse.body) {
      return NextResponse.json(
        { message: "Insight returned no response body" },
        { status: 502 },
      );
    }

    return new Response(upstreamResponse.body, {
      status: upstreamResponse.status,
      headers: {
        "Content-Type":
          upstreamResponse.headers.get("content-type") ||
          "text/plain; charset=utf-8",
        "Cache-Control": "no-store",
      },
    });
  } catch (error) {
    console.error("Insight query proxy error:", error);
    return NextResponse.json(
      {
        message:
          error instanceof Error
            ? error.message
            : "Unexpected insight proxy error",
      },
      { status: 500 },
    );
  }
}
