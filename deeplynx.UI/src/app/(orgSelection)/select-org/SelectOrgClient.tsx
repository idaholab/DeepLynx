// src/app/(orgSelection)/select-org/SelectOrgClient.tsx
"use client";

import AvatarCell from "@/app/(home)/components/Avatar";
import { RoleGate } from "@/app/(home)/rbac/RBACComponents";
import { CreateOrganizationRequestDto } from "@/app/(home)/types/requestDTOs";
import { OrganizationResponseDto, UserResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  createOrganization,
} from "@/app/lib/client_service/organization_services.client";
import { getAllProjects } from "@/app/lib/client_service/projects_services.client";
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
  organizations: OrganizationResponseDto[];
  initialUsersByOrg: Record<number, UserResponseDto[]>;
}

const SelectOrgClient = ({ session, organizations, initialUsersByOrg }: Props) => {

  const router = useRouter();
  const { t } = useLanguage();
  const { setOrganization } = useOrganizationSession();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const [orgsWithCounts, setOrgsWithCounts] = useState<OrgWithCounts[]>([]);

  useEffect(() => {
    async function fetchProjectCounts() {
      try {
        setLoading(true);
        setError(null);

        const orgsWithProjectCounts = await Promise.all(
          organizations.map(async (org) => {
            try {
              const projects = await getAllProjects(org.id as number, true);
              return {
                ...org,
                projectCount: projects.length,
                userCount: initialUsersByOrg[Number(org.id)]?.length || 0,
              };
            } catch (innerError) {
              console.error(`Error fetching projects for org ${org.id}`, innerError);
              return {
                ...org,
                projectCount: 0,
                userCount: initialUsersByOrg[Number(org.id)]?.length || 0,
              };
            }
          })
        );

        setOrgsWithCounts(orgsWithProjectCounts);
      } catch (error) {
        console.error("Failed to fetch project counts:", error);
        setError(t.translations.FAILED_TO_LOAD_ORGANIZATIONS_TRY_AGAIN);
      } finally {
        setLoading(false);
      }
    }
    fetchProjectCounts();
  }, [organizations, initialUsersByOrg, t]);

  // Form state
  const [formData, setFormData] = useState<CreateOrganizationRequestDto>({
    name: "",
    description: "",
    disableFileTransfer: false,
  });

  const handleCreateOrganization = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreateError(null);
    setIsCreating(true);

    try {
      await createOrganization(formData);

      setFormData({ name: "", description: "", disableFileTransfer: false });
      setIsModalOpen(false);

      router.refresh();
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
                  orgsWithCounts.map((org) => (
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
                  setFormData({ name: "", description: "", disableFileTransfer: false });
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

              {/* Disable File Transfer Checkbox */}
              <div className="form-control">
                <label className="cursor-pointer label flex items-center justify-start w-fit gap-3">
                  <input
                    type="checkbox"
                    className="checkbox checkbox-primary"
                    checked={formData.disableFileTransfer || false}
                    onChange={(e) =>
                      setFormData({
                        ...formData,
                        disableFileTransfer: e.target.checked,
                      })
                    }
                    disabled={isCreating}
                  />
                  <span className="font-bold text-lg text-base-content/60">
                    {t.translations.DISABLE_FILE_TRANSFER}
                  </span>
                </label>
                <span className="text-xs text-base-content/60 mt-1">
                  {t.translations.DISABLE_FILE_TRANSFER_HELPER}
                </span>
              </div>

              <div className="modal-action mt-6">
                <button
                  type="button"
                  className="btn btn-ghost"
                  onClick={() => {
                    setIsModalOpen(false);
                    setCreateError(null);
                    setFormData({ name: "", description: "", disableFileTransfer: false });
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