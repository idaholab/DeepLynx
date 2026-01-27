// src/app/(orgSelection)/select-org/page.tsx

import React from "react";
import SelectOrgClient from "./SelectOrgClient";
import { auth } from "../../../../auth";
import { redirect } from "next/navigation";

const page = async () => {
  const isAuthDisabled =
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";

  const session = await auth();

  if (!isAuthDisabled && !session) {
    redirect("/login/signin");
  }

  return <SelectOrgClient session={session} />;
};

export default page;
