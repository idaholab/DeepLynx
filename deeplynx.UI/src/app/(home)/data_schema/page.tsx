import React from "react";

import DataSchema from "./components/DataSchema";
import { cookies } from "next/headers";
import { auth } from "../../../../auth";

export default async function DataSchemaPage() {
  // Get organization ID - prioritize cookie over session for real-time updates
  const cookieStore = await cookies();
  const orgSessionCookie = cookieStore.get("organizationSession");

  let organizationId: number | undefined;

  if (orgSessionCookie) {
    try {
      const orgSession = JSON.parse(orgSessionCookie.value);
      organizationId = orgSession.organizationId;
    } catch (e) {
      console.error("Failed to parse organization cookie:", e);
      // Fallback to session if cookie parsing fails
      const session = await auth();
      organizationId = session?.user?.organizationId;
    }
  } else {
    // No cookie, fallback to session
    const session = await auth();
    organizationId = session?.user?.organizationId;
  }

  return <DataSchema mode="tabs" organizationId={organizationId} />;
}
