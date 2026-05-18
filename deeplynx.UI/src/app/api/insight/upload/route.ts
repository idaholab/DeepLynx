import { NextRequest, NextResponse } from "next/server";
import { getInsightErrorMessage } from "@/app/lib/server_service/insight_services.server";
import { queueInsightUpload } from "@/app/lib/server_service/insight_services.server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
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

    const uploadRequestBody = await request.json();
    const { upstreamResponse, responseBody: upstreamResponseBody } =
      await queueInsightUpload(
      organizationId,
      projectId,
      uploadRequestBody,
      {
        vlmModelConfigId: requestQueryParams.get("vlmModelConfigId")
          ? Number(requestQueryParams.get("vlmModelConfigId"))
          : undefined,
        embeddingModelConfigId: requestQueryParams.get(
          "embeddingModelConfigId",
        )
          ? Number(requestQueryParams.get("embeddingModelConfigId"))
          : undefined,
      },
    );

    if (!upstreamResponse.ok) {
      return NextResponse.json(
        {
          message: "Insight upload failed",
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
