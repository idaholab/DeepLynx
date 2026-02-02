"use client";

import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { ExclamationTriangleIcon } from "@heroicons/react/24/outline";

export const Banner = () => {
  const { organization } = useOrganizationSession();

  if (!organization?.banner || organization.banner.trim() === "") {
    return null;
  }

  return (
    <div className="alert alert-warning rounded-none relative flex justify-center">
      {organization.banner && (
        <>
          <ExclamationTriangleIcon className="h-5 w-5 flex-shrink-0" />
          <p className="font-medium text-sm">{organization.banner}</p>
        </>
      )}
    </div>
  );
};
