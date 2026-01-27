import React from "react";
import SelectOrgClient from "./SelectOrgClient";
import { auth } from "../../../../auth";
import { redirect } from "next/navigation";

const page = async () => {
  const session = await auth();

  if (!session) {
    redirect("/login/signin");
  }

  return <SelectOrgClient session={session} />;
};

export default page;
