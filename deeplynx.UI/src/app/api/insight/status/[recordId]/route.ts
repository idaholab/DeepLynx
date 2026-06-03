import { NextRequest, NextResponse } from "next/server";
import {
  fetchInsightIngestionStatus,
  getInsightErrorMessage,
} from "@/app/lib/server_service/insight_services.server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

function isInsightUnavailableMessage(message: string): boolean {
  return (
      message.includes("Connection refused") ||
      message.includes("ECONNREFUSED")
  );
}

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ recordId: string }> },
) {
  try {
    const { searchParams: requestQueryParams } = new URL(request.url);
    const organizationId = Number(requestQueryParams.get("organizationId"));
    const projectId = Number(requestQueryParams.get("projectId"));

    if (!Number.isFinite(organizationId) || organizationId <= 0) {
      return NextResponse.json(
        { message: "Invalid organizationId" },
        { status: 400 },
      );
    }

    if (!Number.isFinite(projectId) || projectId <= 0) {
      return NextResponse.json(
        { message: "Invalid projectId" },
        { status: 400 },
      );
    }

    const { recordId: recordIdParam } = await params;
    const recordId = Number(recordIdParam);

    if (!Number.isFinite(recordId) || recordId <= 0) {
      return NextResponse.json(
        { message: "Invalid recordId" },
        { status: 400 },
      );
    }

    const { upstreamResponse, responseBody: upstreamResponseBody } =
      await fetchInsightIngestionStatus(organizationId, projectId, recordId);

    if (!upstreamResponse.ok) {
      const errorMessage = getInsightErrorMessage(
          "Insight status check failed",
          upstreamResponseBody,
      );
      
      if (isInsightUnavailableMessage(String(upstreamResponseBody))) {
        console.warn("Insight service unavailable:", errorMessage);
        
        return NextResponse.json({
          available: false,
          status: "unavailable",
          indexed: false,
          chunk_count: 0,
          page_count: 0,
        });
      }
      
      return NextResponse.json(
        {
          message: "Insight status check failed",
          status: upstreamResponse.status,
          details: upstreamResponseBody,
        },
        { status: upstreamResponse.status },
      );
    }

    return NextResponse.json(upstreamResponseBody, {
      status: upstreamResponse.status,
    });
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
