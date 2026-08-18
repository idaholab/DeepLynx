import React from "react";
import SelectOrgClient from "./SelectOrgClient";
import { auth } from "../../../../auth";
import { redirect } from "next/navigation";
import { getAllUsersServer } from "@/app/lib/server_service/user_services.server";
import { getAllOrganizationsForUserServer } from "@/app/lib/server_service/organization_services.server";
import type { Session } from "next-auth";
import { UserResponseDto } from "@/app/(home)/types/responseDTOs";

const page = async () => {
  const isAuthDisabled =
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";

  if (isAuthDisabled) {
    const mockSession: Session = {
      user: {
        name: "Local Developer",
        email: "developer@localhost",
        image: undefined,
      },
      expires: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
    };

    const { items: organizations } = await getAllOrganizationsForUserServer(true);

    const usersByOrg: Record<number, UserResponseDto[]> = {};
    for (const org of organizations) {
      const users = await getAllUsersServer(undefined, org.id as number);
      usersByOrg[Number(org.id)] = users;
    }

    return <SelectOrgClient session={mockSession} organizations={organizations} initialUsersByOrg={usersByOrg} />;
  }

  const session = await auth();

  if (!session) {
    redirect("/login/signin");
  }

  const { items: organizations } = await getAllOrganizationsForUserServer(true);

  const usersByOrg: Record<number, UserResponseDto[]> = {};
  for (const org of organizations) {
    const users = await getAllUsersServer(undefined, org.id as number);
    usersByOrg[Number(org.id)] = users;
  }

  return <SelectOrgClient session={session} organizations={organizations} initialUsersByOrg={usersByOrg} />;
};

export default page;
