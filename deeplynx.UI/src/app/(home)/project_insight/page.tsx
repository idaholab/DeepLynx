import { redirect } from "next/navigation";
import { isInsightHidden } from "@/app/lib/feature_flags";
import ProjectInsightClientView from "./ProjectInsightClientView";

const ProjectInsight = () => {
  if (isInsightHidden()) {
    redirect("/");
  }

  return <ProjectInsightClientView />;
};

export default ProjectInsight;
