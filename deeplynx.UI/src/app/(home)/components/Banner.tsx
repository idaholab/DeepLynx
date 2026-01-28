"use client";

import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { ExclamationTriangleIcon } from "@heroicons/react/24/outline";

export const Banner = () => {
  const { organization } = useOrganizationSession();
  const { project } = useProjectSession();

  if (!organization?.banner || organization.banner.trim() === "") {
    return null;
  }

  // if (!project?.banner || project.banner.trim() === "") {
  //   return null;
  // }

  return (
    <div>
      <div className="alert alert-warning rounded-none relative flex justify-center">
        {organization.banner && (
          <>
            {/* <ExclamationTriangleIcon className="h-5 w-5 flex-shrink-0" /> */}
            <p className="font-medium text-sm">
              Project: {organization.banner}
            </p>
          </>
        )}
      </div>
    </div>
  );
};
