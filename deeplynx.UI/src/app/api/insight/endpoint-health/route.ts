import { NextRequest, NextResponse } from "next/server";
import {
    fetchInsightEndpointHealth,
    getInsightErrorMessage,
} from "@/app/lib/server_service/insight_services.server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(request: NextRequest)  {
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
        
        const endpointHealthRequestBody = await request.json();
        const {upstreamResponse, responseBody: upstreamResponseBody} = await fetchInsightEndpointHealth(
            organizationId,
            projectId,
            endpointHealthRequestBody,
        );
        
        if (!upstreamResponse.ok) {
            return NextResponse.json(
                {
                    message: "Insight endpoint health check failed",
                    status: upstreamResponse.status,
                    details: upstreamResponseBody,
                },
                {status: upstreamResponse.status},
            );
        }
        
        return NextResponse.json(upstreamResponseBody, {
            status: upstreamResponse.status,
        });
    } catch (error) {
        console.error("Insight endpoint health proxy error:", error);
        return NextResponse.json(
            {
                message: getInsightErrorMessage(
                    "Unexpected insight endpoint health proxy error",
                    error,
                ),
            },
            {status: 500},
        );
    }
}