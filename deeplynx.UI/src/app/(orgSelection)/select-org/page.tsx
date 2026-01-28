import React from "react";
import SelectOrgClient from "./SelectOrgClient";
import { auth } from "../../../../auth";
import { redirect } from "next/navigation";
import type { Session } from "next-auth";

const page = async () => {
  const isAuthDisabled =
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";

  if (isAuthDisabled) {
    const mockSession: Session = {
      user: {
        id: "local-dev-user",
        name: "Local Dev User",
        email: "dev@localhost",
        image: undefined,
      },
      expires: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
    };

    return <SelectOrgClient session={mockSession} />;
  }

  const session = await auth();

  if (!session) {
    redirect("/login/signin");
  }

  return <SelectOrgClient session={session} />;
};

export default page;
