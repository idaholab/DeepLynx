import { NextRequest, NextResponse } from "next/server";
import {
  getInsightErrorMessage,
  queryInsight,
} from "@/app/lib/server_service/insight_services.server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const upstreamResponse = await queryInsight(body);

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
        message: getInsightErrorMessage("Unexpected insight proxy error", error),
      },
      { status: 500 },
    );
  }
}
