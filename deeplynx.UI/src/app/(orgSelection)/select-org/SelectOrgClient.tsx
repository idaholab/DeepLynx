// src/app/(orgSelection)/select-org/SelectOrgClient.tsx
"use client";

import AvatarCell from "@/app/(home)/components/Avatar";
import { RoleGate } from "@/app/(home)/rbac/RBACComponents";
import { CreateOrganizationRequestDto } from "@/app/(home)/types/requestDTOs";
import { OrganizationResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  createOrganization,
  getAllOrganizationsForUser,
} from "@/app/lib/client_service/organization_services.client";
import { getAllProjects } from "@/app/lib/client_service/projects_services.client";
import { getAllUsers } from "@/app/lib/client_service/user_services.client";
import {
  ArrowRightIcon,
  Cog6ToothIcon,
  PlusIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import type { Session } from "next-auth";
import Image from "next/image";
import { useRouter } from "next/navigation";
import React, { useEffect, useState } from "react";

interface OrgWithCounts extends OrganizationResponseDto {
  projectCount: number;
  userCount: number;
}

interface Props {
  session: Session;
}

const SelectOrgClient = ({ session }: Props) => {
  const router = useRouter();
  const { t } = useLanguage();
  const { setOrganization } = useOrganizationSession();
  const [organizations, setOrganizations] = useState<OrgWithCounts[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  // Form state
  const [formData, setFormData] = useState<CreateOrganizationRequestDto>({
    name: "",
    description: "",
  });

  useEffect(() => {
    fetchOrganizationsWithCounts();
  }, []);

  const fetchOrganizationsWithCounts = async () => {
    try {
      setLoading(true);

      // Fetch all organizations
      const orgs = await getAllOrganizationsForUser(true);

      // Fetch project and user counts for each organization
      const orgsWithCounts = await Promise.all(
        orgs.map(async (org) => {
          try {
            const [projects, users] = await Promise.all([
              getAllProjects(org.id as number, true),
              getAllUsers(org.id),
            ]);

            return {
              ...org,
              projectCount: projects.length,
              userCount: users.length,
            };
          } catch (err) {
            console.error(`Failed to fetch data for org ${org.id}:`, err);
            return {
              ...org,
              projectCount: 0,
              userCount: 0,
            };
          }
        }),
      );

      setOrganizations(orgsWithCounts);
    } catch (err) {
      console.error("Failed to fetch organizations:", err);
      setError(t.translations.FAILED_TO_LOAD_ORGANIZATIONS_TRY_AGAIN);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateOrganization = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreateError(null);
    setIsCreating(true);

    try {
      await createOrganization(formData);

      // Reset form and close modal
      setFormData({ name: "", description: "" });
      setIsModalOpen(false);

      // Refresh the organizations list
      await fetchOrganizationsWithCounts();
    } catch (err) {
      console.error("Failed to create organization:", err);
      setCreateError(t.translations.FAILED_TO_CREATE_ORGANIZATION_TRY_AGAIN);
    } finally {
      setIsCreating(false);
    }
  };

  const handleLaunchOrganization = (org: OrgWithCounts) => {
    // Set the organization in the session provider
    setOrganization({
      organizationId: org.id,
      organizationName: org.name,
      themeName: org.theme,
    });

    router.push("/");
  };

  const formatUserName = (fullName?: string | null): string => {
    if (!fullName) return "";
    const parts = fullName.trim().split(/\s+/);
    const firstName = parts[0] ?? "";
    const lastName = parts[parts.length - 1] ?? "";
    return [firstName, lastName].filter(Boolean).join(" ");
  };

  if (loading) {
    return (
      <div className="app-header min-h-screen flex items-center justify-center">
        <span className="loading loading-spinner loading-lg"></span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="app-header min-h-screen flex items-center justify-center">
        <div className="alert alert-error max-w-md">
          <span>{error}</span>
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="app-header min-h-screen h-[700px] flex flex-col items-center justify-center p-4">
        <div className="flex flex-col items-center gap-4 max-w-4xl w-full flex-1 justify-center">
          <Image
            src="/assets/nexusWhite.png"
            alt={t.translations.DEEPLYNX_LOGO}
            width={365.8}
            height={213.9}
            priority
          />
          <div className="card bg-base-100 shadow-xl w-full">
            {/* Header */}
            <div className="card-body p-6">
              <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 mb-4">
                <div className="flex items-center gap-3">
                  <h2 className="text-xl font-semibold text-base-content">
                    {t.translations.WELCOME_BACK_COMMA}{" "}
                    {formatUserName(session.user.name)}
                  </h2>
                </div>
                <RoleGate role="sysAdmin">
                  <button
                    className="btn btn-primary btn-sm"
                    onClick={() => setIsModalOpen(true)}
                  >
                    <PlusIcon className="size-5" />
                    <span>{t.translations.ORGANIZATION}</span>
                  </button>
                </RoleGate>
              </div>

              <div className="divider my-0"></div>

              {/* Organization List */}
              <div className="space-y-3 mt-4">
                {organizations.length === 0 ? (
                  <div className="text-center py-8 text-base-content/70">
                    <p>{t.translations.NO_ORGANIZATIONS_FOUND}</p>
                    <p className="text-sm mt-2">
                      {
                        t.translations
                          .CREATE_FIRST_ORGANIZATION_TO_GET_STARTED
                      }
                    </p>
                  </div>
                ) : (
                  organizations.map((org) => (
                    <div
                      key={org.id}
                      className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 p-4 hover:bg-base-200 rounded-lg transition-colors"
                    >
                      {/* Left side - Logo and info */}
                      <div className="flex items-start sm:items-center gap-4">
                        <AvatarCell name={org.name} />
                        <div className="min-w-0">
                          <h3 className="font-semibold text-lg text-base-content break-words">
                            {org.name}
                          </h3>
                          {org.description && (
                            <p className="text-xs text-base-content/50 mt-1">
                              {org.description}
                            </p>
                          )}
                          <p className="text-sm text-base-content/70 mt-1">
                            <span className="font-semibold">
                              {org.projectCount}
                            </span>{" "}
                            {org.projectCount === 1
                              ? t.translations.PROJECT
                              : t.translations.PROJECTS}
                            {" • "}
                            <span className="font-semibold">
                              {org.userCount}
                            </span>{" "}
                            {org.userCount === 1
                              ? t.translations.MEMBER
                              : t.translations.MEMBERS}
                          </p>
                        </div>
                      </div>

                      {/* Right side - Actions */}
                      <div className="flex items-center gap-2 self-start sm:self-auto">
                        <RoleGate role="sysAdmin">
                          <button className="btn btn-ghost btn-sm btn-circle">
                            <Cog6ToothIcon className="size-6" />
                          </button>
                        </RoleGate>
                        <button
                          className="btn btn-primary btn-sm"
                          onClick={() => handleLaunchOrganization(org)}
                        >
                          {t.translations.LAUNCH}
                          <ArrowRightIcon className="size-5" />
                        </button>
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>
        </div>

        {/* Footer links at bottom */}
        {/* TODO: Show this when we have things to attach. */}
        {/* <div className="flex gap-30 pb-15">
          <div className="text-primary-content text-xl flex items-center">
            ABOUT
            <ArrowRightIcon className="size-8 ml-4" />
          </div>
          <div className="text-primary-content text-xl flex items-center">
            DOCS
            <ArrowRightIcon className="size-8 ml-4" />
          </div>
          <div className="text-primary-content text-xl flex items-center">
            CONTACT US
            <ArrowRightIcon className="size-8 ml-4" />
          </div>
        </div> */}
      </div>

      {/* Create Organization Modal */}
      {isModalOpen && (
        <dialog className="modal modal-open">
          <div className="modal-box max-w-lg">
            <div className="flex items-center justify-between mb-4">
              <h3 className="font-bold text-lg text-base-content">
                {t.translations.CREATE_NEW_ORGANIZATION}
              </h3>
              <button
                className="btn btn-sm btn-circle btn-ghost"
                onClick={() => {
                  setIsModalOpen(false);
                  setCreateError(null);
                  setFormData({ name: "", description: "" });
                }}
              >
                <XMarkIcon className="size-5" />
              </button>
            </div>

            {createError && (
              <div className="alert alert-error mb-4">
                <span>{createError}</span>
              </div>
            )}

            <form
              onSubmit={handleCreateOrganization}
              className="flex flex-col gap-4"
            >
              <input
                type="text"
                placeholder={t.translations.ORGANIZATION_NAME}
                className="input input-bordered input-primary bg-base-100 text-base-content placeholder:text-base-content/40 w-full"
                value={formData.name}
                onChange={(e) =>
                  setFormData({ ...formData, name: e.target.value })
                }
                required
                disabled={isCreating}
              />
              <textarea
                className="textarea textarea-bordered textarea-primary bg-base-100 text-base-content placeholder:text-base-content/40 min-h-[100px] w-full"
                placeholder={t.translations.DESCRIPTION_OPTIONAL}
                value={formData.description || ""}
                onChange={(e) =>
                  setFormData({ ...formData, description: e.target.value })
                }
                disabled={isCreating}
              />

              <div className="modal-action mt-6">
                <button
                  type="button"
                  className="btn btn-ghost"
                  onClick={() => {
                    setIsModalOpen(false);
                    setCreateError(null);
                    setFormData({ name: "", description: "" });
                  }}
                  disabled={isCreating}
                >
                  {t.translations.CANCEL}
                </button>
                <button
                  type="submit"
                  className="btn btn-primary"
                  disabled={isCreating || !formData.name.trim()}
                >
                  {isCreating ? (
                    <>
                      <span className="loading loading-spinner loading-sm"></span>
                      {t.translations.CREATING}
                    </>
                  ) : (
                    t.translations.CREATE_ORGANIZATION
                  )}
                </button>
              </div>
            </form>
          </div>
        </dialog>
      )}
    </>
  );
};

export default SelectOrgClient;
