// src/app/(home)/organization_management/users/UsersTable.tsx
"use client";

import { useCallback, useEffect, useState } from "react";
import toast from "react-hot-toast";

import EditSysUser from "../../components/SiteManagementPortal/EditSysUser";
import {
  OrganizationResponseDto,
  UserActivityCountsDto,
  UserResponseDto,
} from "../../types/responseDTOs";

import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  removeUserFromOrganization,
  inviteUserToOrganization,
} from "@/app/lib/client_service/organization_services.client";
import {
  archiveUser,
  getActiveUserCounts,
  getAllUsers,
} from "@/app/lib/client_service/user_services.client";
import { InviteUserToOrganizationRequestDto } from "../../types/requestDTOs";
import DeleteModal from "./DeleteModal";
import InviteUserModal from "./InviteUserModal";
import UsersHeaderStats from "./UsersHeaderStats";
import UsersListTable from "./UsersListTable";
import { UsersTableRow } from "../../types/types";
import { useLanguage } from "@/app/contexts/Language";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

interface Props {
  members: UserResponseDto[];
  scope?: "org" | "site";
  availableOrganizations?: OrganizationResponseDto[];
}

type ConfirmModalState = {
  isOpen: boolean;
  itemId: number | null;
  itemName: string;
  isPending: boolean;
};

/* -------------------------------------------------------------------------- */
/*                            Helper: build table rows                        */
/* -------------------------------------------------------------------------- */

const parseLastLoginTime = (lastLogin?: string | null): number => {
  if (!lastLogin) return 0;

  const normalized = /Z$|[+-]\d{2}:\d{2}$/.test(lastLogin)
    ? lastLogin
    : `${lastLogin}Z`;
  const time = Date.parse(normalized);
  return Number.isNaN(time) ? 0 : time;
};

const buildActivityCounts = (users: UserResponseDto[]): UserActivityCountsDto => {
  const now = Date.now();
  const activeUsers = users.filter((user) => user.isActive && !user.isArchived);
  const countSince = (windowMs: number) =>
    activeUsers.filter((user) => {
      const lastLoginTime = parseLastLoginTime(user.lastLogin);
      return lastLoginTime > 0 && now - lastLoginTime <= windowMs;
    }).length;

  return {
    activeLast24Hours: countSince(24 * 60 * 60 * 1000),
    activeLast7Days: countSince(7 * 24 * 60 * 60 * 1000),
    activeLast30Days: countSince(30 * 24 * 60 * 60 * 1000),
    generatedAt: new Date(now).toISOString(),
  };
};

const buildTableData = (users: UserResponseDto[]): UsersTableRow[] => {
  const activeUsers: UsersTableRow[] = users.map((user) => ({
    id: user.id,
    name: user.name || "",
    email: user.email || "",
    username: user.username,
    isActive: user.isActive,
    isArchived: user.isArchived,
    isSysAdmin: user.isSysAdmin,
    isOrgAdmin: user.isOrgAdmin,
    lastLogin: user.lastLogin ?? null,
    isPending: false,
  }));

  return [...activeUsers].sort(
    (a, b) => parseLastLoginTime(b.lastLogin) - parseLastLoginTime(a.lastLogin),
  );
};

/* -------------------------------------------------------------------------- */
/*                           UsersTable Component                             */
/* -------------------------------------------------------------------------- */

const UsersTable = ({
  members,
  scope = "org",
  availableOrganizations = [],
}: Props) => {
  /* ------------------------------------------------------------------------ */
  /*                               Core State                                */
  /* ------------------------------------------------------------------------ */

  const [tableData, setTableData] = useState<UsersTableRow[]>(() =>
    buildTableData(members),
  );
  const [activityCounts, setActivityCounts] =
    useState<UserActivityCountsDto>(() => buildActivityCounts(members));
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<"active" | "archived">("active");
  const [archivedUsers, setArchivedUsers] = useState<UsersTableRow[]>([]);
  const { t } = useLanguage();

  /* ------------------------------------------------------------------------ */
  /*                           Invite Modal State                             */
  /* ------------------------------------------------------------------------ */

  const [showInviteModal, setShowInviteModal] = useState(false);
  const [modalLoading, setModalLoading] = useState(false);
  const [selectedInviteOrganizationId, setSelectedInviteOrganizationId] =
    useState("");

  /* ------------------------------------------------------------------------ */
  /*                            Edit User Modal State                         */
  /* ------------------------------------------------------------------------ */

  const [editingUserId, setEditingUserId] = useState<number | null>(null);
  const [editUserName, setEditUserName] = useState("");
  const [editUserIsOrgAdmin, setEditUserIsOrgAdmin] = useState(false);
  const [editUserIsSysAdmin, setEditUserIsSysAdmin] = useState(false);

  /* ------------------------------------------------------------------------ */
  /*                         Confirm Remove/Cancel State                      */
  /* ------------------------------------------------------------------------ */

  const [confirmModal, setConfirmModal] = useState<ConfirmModalState>({
    isOpen: false,
    itemId: null,
    itemName: "",
    isPending: false,
  });

  /* ------------------------------------------------------------------------ */
  /*                           Organization Context                           */
  /* ------------------------------------------------------------------------ */

  const { organization } = useOrganizationSession();

  /* ------------------------------------------------------------------------ */
  /*                        Data Loading / Normalization                      */
  /* ------------------------------------------------------------------------ */

  const loadActivityCounts = useCallback(async () => {
    const organizationId = organization?.organizationId;

    if (scope === "org" && !organizationId) return;

    try {
      const counts =
        scope === "org"
          ? await getActiveUserCounts(organizationId)
          : await getActiveUserCounts();
      setActivityCounts(counts);
    } catch (error) {
      console.error("Failed to load active user counts:", error);
    }
  }, [organization?.organizationId, scope]);

  const loadAllData = useCallback(async () => {
    const organizationId = organization?.organizationId;

    if (scope === "org" && !organizationId) return;

    try {
      const usersRequest =
        scope === "org" ? getAllUsers(organizationId) : getAllUsers();
      const countsRequest =
        scope === "org"
          ? getActiveUserCounts(organizationId)
          : getActiveUserCounts();
      const [users, counts] = await Promise.all([usersRequest, countsRequest]);

      setTableData(buildTableData(users));
      setActivityCounts(counts);
    } catch (error) {
      console.error("Failed to load data:", error);
    }
  }, [organization?.organizationId, scope]);


  const loadArchivedUsers = useCallback(async () => {
  const organizationId = organization?.organizationId;
  if (scope === "org" && !organizationId) return;

  try {
    const users = scope === "org"
      ? await getAllUsers(organizationId, undefined, true)
      : await getAllUsers(undefined, undefined, true);
    setArchivedUsers(buildTableData(users).filter((u) => u.isArchived));
  } catch (error) {
    console.error("Failed to load archived users:", error);
  }
  }, [organization?.organizationId, scope]);

  // When server-side members prop changes, sync local state
  useEffect(() => {
    setTableData(buildTableData(members));
    setActivityCounts(buildActivityCounts(members));
  }, [members]);

  useEffect(() => {
    void loadActivityCounts();
    const intervalId = window.setInterval(() => {
      void loadActivityCounts();
    }, 60_000);

    return () => window.clearInterval(intervalId);
  }, [loadActivityCounts]);

  useEffect(() => {
  if (activeTab === "archived") {
    void loadArchivedUsers();
  }
  }, [activeTab, loadArchivedUsers]);

  /* ------------------------------------------------------------------------ */
  /*                        Invite Flow: Open Modal                           */
  /* ------------------------------------------------------------------------ */

  const handleOpenInviteModal = () => {
    if (scope === "site") {
      if (availableOrganizations.length === 0) {
        toast.error(t.translations.ORGANIZATION_NOT_FOUND);
        return;
      }

      setSelectedInviteOrganizationId((current) =>
        current || String(availableOrganizations[0].id),
      );
    }

    setShowInviteModal(true);
  };

  /* ------------------------------------------------------------------------ */
  /*                          Invite Flow: Send Invitation                    */
  /* ------------------------------------------------------------------------ */

  type InviteResults = {
    successful: string[];
    failed: { email: string; error: string }[];
  };

  const handleInviteUsers = async (
    emails: string[],
  ): Promise<InviteResults> => {
    const targetOrganizationId =
      scope === "org"
        ? organization?.organizationId
        : selectedInviteOrganizationId;

    if (!targetOrganizationId) {
      toast.error(t.translations.NO_ORG_SELECTED);
      return {
        successful: [],
        failed: emails.map((email) => ({
          email,
          error: t.translations.NO_ORG_SELECTED,
        })),
      };
    }

    // (InviteUserModal already validates format, but keep this as a safety net)
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    setModalLoading(true);

    const results: InviteResults = { successful: [], failed: [] };

    try {
      for (const email of emails) {
        const trimmed = email.trim();

        if (!emailRegex.test(trimmed)) {
          results.failed.push({
            email: trimmed,
            error: t.translations.PLEASE_ENTER_VALID_EMAIL_ADDRESS,
          });
          continue;
        }

        try {
          const inviteData: InviteUserToOrganizationRequestDto = {
            userEmail: trimmed,
            userName: trimmed.split("@")[0],
          };

          await inviteUserToOrganization(
            Number(targetOrganizationId),
            inviteData,
          );

          results.successful.push(trimmed);
        } catch (err: any) {
          // Try to extract a useful message; fall back to generic translation
          const message =
            err?.response?.data?.message ||
            err?.message ||
            t.translations.FAILED_TO_SEND_INVITATION;

          results.failed.push({ email: trimmed, error: String(message) });
        }
      }

      // Toast summary (optional but nice UX)
      if (results.successful.length > 0) {
        toast.success(
          `${t.translations.INVITATION_SENT_TO_} ${results.successful.length}`,
        );
        await loadAllData(); // refresh once
      }

      if (results.failed.length > 0) {
        toast.error(t.translations.FAILED_TO_SEND_INVITATION);
      }

      return results;
    } finally {
      setModalLoading(false);
    }
  };

  const handleResendInvite = async (email: string) => {
    if (!organization?.organizationId) {
      toast.error(t.translations.NO_ORG_SELECTED);
      return;
    }

    try {
      setLoading(true);

      const inviteData: InviteUserToOrganizationRequestDto = {
        userEmail: email,
        userName: email.split("@")[0], // Extract username from email
      };

      await inviteUserToOrganization(
        organization.organizationId as number,
        inviteData,
      );

      toast.success(`${t.translations.INVITATION_RESENT_TO_} ${email}`);
    } catch (error) {
      console.error("Failed to resend invite:", error);
      toast.error(t.translations.FAILED_TO_RESEND_INVITATION);
    } finally {
      setLoading(false);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                  Remove User / Cancel Invite Confirmation                */
  /* ------------------------------------------------------------------------ */

  const handleRemoveOrCancel = async () => {
    if (!confirmModal.itemId) return;

    try {
      setLoading(true);

      if (confirmModal.isPending) {
        // TODO: API call to cancel invite
        toast.success(t.translations.INVITATION_CANCELED);
      } else if (scope === "org") {
        if (!organization?.organizationId) {
          throw new Error("No organization selected");
        }

        await removeUserFromOrganization(
          organization.organizationId as number,
          confirmModal.itemId,
        );

        toast.success(t.translations.USER_REMOVED_FROM_ORG);
      } else if (scope === "site") {
        await archiveUser(confirmModal.itemId, true);
        toast.success(t.translations.USER_ARCHIVED_SUCCESSFULLY);
      }

      // Refresh org-scoped list
      await loadAllData();

      setConfirmModal({
        isOpen: false,
        itemId: null,
        itemName: "",
        isPending: false,
      });
    } catch (error) {
      console.error("Failed to remove/cancel:", error);
      toast.error(
        confirmModal.isPending
          ? t.translations.FAILED_TO_CANCEL_INVITATION
          : scope === "org"
            ? t.translations.FAILED_TO_REMOVE_USER
            : t.translations.FAILED_TO_ARCHIVE_USER,
      );
    } finally {
      setLoading(false);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                               Derived Stats                              */
  /* ------------------------------------------------------------------------ */

  const activeUserCount = tableData.filter(
    (u) => !u.isPending && u.isActive && !u.isArchived,
  ).length;
  const pendingCount = tableData.filter((u) => u.isActive === false).length;
  const totalCount = activeUserCount + pendingCount;

  /* ------------------------------------------------------------------------ */
  /*                               Main Render                                */
  /* ------------------------------------------------------------------------ */

  return (
    <div className="p-6">
      <div className="card bg-base-100 border border-primary">
        <div className="card-body">
          {/* Page header & stats */}
          <UsersHeaderStats
            activeUserCount={activeUserCount}
            pendingCount={pendingCount}
            totalCount={totalCount}
            activityCounts={activityCounts}
            loading={loading}
            onInviteClick={handleOpenInviteModal}
            scope={scope}
          />

          {/* Tabs */}
          <div role="tablist" className="tabs tabs-bordered mb-4">
            <button
              role="tab"
              className={`tab ${activeTab === "active" ? "tab-active" : ""}`}
              onClick={() => setActiveTab("active")}
            >
              {t.translations.ACTIVE_USERS}
            </button>
            <button
              role="tab"
              className={`tab ${activeTab === "archived" ? "tab-active" : ""}`}
              onClick={() => setActiveTab("archived")}
          >
              Archived Users
          </button>
          </div>

          {/* Combined Users & Pending Invites Table */}
          <UsersListTable
            tableData={activeTab === "active" ? tableData : archivedUsers}
            scope={scope}
            loading={loading}
            onResendInvite={handleResendInvite}
            onEditUser={(
              id: number,
              name: string,
              isOrgAdmin: boolean,
              isSysAdmin: boolean,
            ) => {
              setEditingUserId(id);
              setEditUserName(name);
              setEditUserIsOrgAdmin(isOrgAdmin);
              setEditUserIsSysAdmin(isSysAdmin);
            }}
            onOpenConfirm={(item: ConfirmModalState) => setConfirmModal(item)}
          />
        </div>
      </div>

      {/* Invite User Modal */}
      <InviteUserModal
        isOpen={showInviteModal}
        modalLoading={modalLoading}
        onClose={() => setShowInviteModal(false)}
        onInvite={handleInviteUsers}
        scope={scope}
        availableOrganizations={availableOrganizations}
        selectedOrganizationId={selectedInviteOrganizationId}
        onSelectedOrganizationChange={setSelectedInviteOrganizationId}
      />

      {/* Edit User Modal */}
      {editingUserId !== null && (
        <EditSysUser
          isOpen={true}
          onClose={() => {
            setEditingUserId(null);
            setEditUserName("");
          }}
          userId={editingUserId}
          userName={editUserName}
          onUserUpdated={loadAllData}
          currentOrgAdminStatus={editUserIsOrgAdmin}
          currentSysAdminStatus={editUserIsSysAdmin}
          scope={scope}
          organizationId={organization?.organizationId as number}
        />
      )}

      {/* Confirm Delete/Cancel Modal */}
      <DeleteModal
        isOpen={confirmModal.isOpen}
        onClose={() =>
          setConfirmModal({
            isOpen: false,
            itemId: null,
            itemName: "",
            isPending: false,
          })
        }
        onConfirm={handleRemoveOrCancel}
        title={
          confirmModal.isPending
            ? t.translations.CANCEL_INVITATION
            : scope === "org"
              ? t.translations.REMOVE_USER
              : t.translations.ARCHIVE_USER
        }
        message={
          confirmModal.isPending
            ? `${t.translations.SURE_YOU_WANT_TO_CANCEL_INVITATION_FOR_} ${confirmModal.itemName}? ${t.translations.THEY_WILL_NOT_BE_ABLE_TO_JOIN_WITH_LINK}`
            : scope === "org"
              ? `${t.translations.ARE_YOU_SURE_YOU_WANT_TO_REMOVE_} ${confirmModal.itemName} ${t.translations.THEY_WILL_LOSE_ACCESS_FROM_ALL_PROJECTS}`
              : `${t.translations.ARE_YOU_SURE_YOU_WANT_TO_ARCHIVE_} ${confirmModal.itemName}? ${t.translations.THEY_WILL_NO_LONGER_BE_ABLE_TO_SIGN_IN_UNTIL_UNARCHIVED}`
        }
        confirmText={
          confirmModal.isPending
            ? t.translations.CANCEL_INVITE
            : scope === "org"
              ? t.translations.REMOVE
              : t.translations.ARCHIVE
        }
        cancelText={
          confirmModal.isPending
            ? t.translations.KEEP_INVITE
            : t.translations.CANCEL
        }
        isDestructive={true}
        loading={loading}
      />
    </div>
  );
};

export default UsersTable;
