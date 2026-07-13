import { redirect } from "next/navigation";
import { isRunHidden } from "@/app/lib/feature_flags";
import RunClient from "./RunClient";

export default function Page() {
  if (isRunHidden()) {
    redirect("/");
  }

  return <RunClient />;
}
