// app/(home)/project/[id]/page.tsx
import { notFound, redirect } from "next/navigation";
import ProjectDetailClient from "./ProjectDetailClient";
import { getAllProjectsServer } from "@/app/lib/server_service/projects_services.server";
import { ProjectResponseDto } from "@/app/(home)/types/responseDTOs";
import { cookies } from "next/headers";

function toProjectResponseDtos(p: ProjectResponseDto): ProjectResponseDto {
  return {
    id: String(p.id),
    name: p.name ?? "",
    description: p.description ?? "",
    abbreviation: p.abbreviation ?? "",
    lastUpdatedAt: p.lastUpdatedAt,
    lastUpdatedBy: p.lastUpdatedBy ?? "",
    isArchived: p.isArchived,
    organizationId: p.organizationId,
  };
}

type Props = {
  params: Promise<{ id?: string }>;
};

export default async function ProjectPage({ params }: Props) {
  const { id } = await params;
  if (!id) return notFound();

  // Get organization from cookies
  const cookieStore = await cookies();
  const orgSessionCookie = cookieStore.get("organizationSession");

  if (!orgSessionCookie) {
    redirect("/select-org");
  }

  let organizationId: string | number | undefined;
  try {
    const orgSession = JSON.parse(orgSessionCookie.value);
    organizationId = orgSession.organizationId;
  } catch (e) {
    console.error("Failed to parse organization session:", e);
    redirect("/select-org");
  }

  // Fetch projects filtered by organization
  const { items: ProjectResponseDtos } = (await getAllProjectsServer(
    organizationId as number,
    true
  ));
  const initialProjects = ProjectResponseDtos.map((p) =>
    toProjectResponseDtos(p)
  );
  const initialProject = initialProjects.find((p) => p.id === id);

  if (!initialProject) return notFound();

  return <ProjectDetailClient initialProject={initialProject} projectId={id} />;
}
