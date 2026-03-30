import { NextRequest, NextResponse } from "next/server";
import {
  fetchInsightIngestionStatus,
  getInsightErrorMessage,
} from "@/app/lib/server_service/insight_services.server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET(
  _request: NextRequest,
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

    const { upstreamResponse, responseBody } =
      await fetchInsightIngestionStatus(recordIdNum);

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
        message: getInsightErrorMessage(
          "Unexpected insight status proxy error",
          error,
        ),
      },
      { status: 500 },
    );
  }
}
