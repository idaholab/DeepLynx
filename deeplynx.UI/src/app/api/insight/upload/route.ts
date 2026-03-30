import { NextRequest, NextResponse } from "next/server";
import {
  getInsightErrorMessage,
  uploadInsightDocument,
} from "@/app/lib/server_service/insight_services.server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { upstreamResponse, responseBody } = await uploadInsightDocument(body);

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
        message: getInsightErrorMessage(
          "Unexpected insight upload proxy error",
          error,
        ),
      },
      { status: 500 },
    );
  }
}
