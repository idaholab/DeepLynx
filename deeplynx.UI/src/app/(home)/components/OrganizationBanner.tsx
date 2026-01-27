"use client";

import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { ExclamationTriangleIcon } from "@heroicons/react/24/outline";

export const OrganizationBanner = () => {
  const { organization } = useOrganizationSession();

  if (!organization?.banner || organization.banner.trim() === "") {
    return null;
  }

  return (
    <div>
      <div className="alert alert-warning rounded-none relative flex justify-center">
        <ExclamationTriangleIcon className="h-5 w-5 flex-shrink-0" />
        <p className="font-medium text-sm">{organization.banner}</p>
      </div>
    </div>
  );
};
