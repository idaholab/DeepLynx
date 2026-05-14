import {
  getInsightErrorMessage,
  streamInsightQuery,
} from "@/app/lib/server_service/insight_services.server";
import { NextRequest, NextResponse } from "next/server";

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

    const queryRequestBody = await request.json();
    const upstreamResponse = await streamInsightQuery(
      organizationId,
      projectId,
      queryRequestBody,
      {
        languageModelConfigId: requestQueryParams.get(
          "languageModelConfigId",
        )
          ? Number(requestQueryParams.get("languageModelConfigId"))
          : undefined,
        embeddingModelConfigId: requestQueryParams.get(
          "embeddingModelConfigId",
        )
          ? Number(requestQueryParams.get("embeddingModelConfigId"))
          : undefined,
      },
    );

    if (!upstreamResponse.ok) {
      const upstreamErrorText = await upstreamResponse.text();
      return NextResponse.json(
        {
          message: "Insight query failed",
          status: upstreamResponse.status,
          details: upstreamErrorText || null,
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
        message: getInsightErrorMessage(
          "Unexpected insight proxy error",
          error,
        ),
      },
      { status: 500 },
    );
  }
}
