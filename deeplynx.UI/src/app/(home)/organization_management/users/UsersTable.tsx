// src/app/(home)/organization_management/users/UsersTable.tsx
"use client";

import { useEffect, useState } from "react";
import toast from "react-hot-toast";

import EditSysUser from "../../components/SiteManagementPortal/EditSysUser";
import { UserResponseDto } from "../../types/responseDTOs";

import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  removeUserFromOrganization,
  inviteUserToOrganization,
} from "@/app/lib/client_service/organization_services.client";
import { getAllUsers } from "@/app/lib/client_service/user_services.client";
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

const buildTableData = (users: UserResponseDto[]): UsersTableRow[] => {
  const activeUsers: UsersTableRow[] = users.map((user) => ({
    id: user.id,
    name: user.name || "",
    email: user.email || "",
    username: user.username,
    isActive: user.isActive,
    isArchived: user.isArchived,
    isSysAdmin: user.isSysAdmin,
    isPending: false,
  }));

  return [...activeUsers];
};

/* -------------------------------------------------------------------------- */
/*                           UsersTable Component                             */
/* -------------------------------------------------------------------------- */

const UsersTable = ({ members }: Props) => {
  /* ------------------------------------------------------------------------ */
  /*                               Core State                                */
  /* ------------------------------------------------------------------------ */

  const [tableData, setTableData] = useState<UsersTableRow[]>(() =>
    buildTableData(members)
  );
  const [loading, setLoading] = useState(false);
  const { t } = useLanguage();

  /* ------------------------------------------------------------------------ */
  /*                           Invite Modal State                             */
  /* ------------------------------------------------------------------------ */

  const [showInviteModal, setShowInviteModal] = useState(false);
  const [inviteEmail, setInviteEmail] = useState("");
  const [modalLoading, setModalLoading] = useState(false);

  /* ------------------------------------------------------------------------ */
  /*                            Edit User Modal State                         */
  /* ------------------------------------------------------------------------ */

  const [editingUserId, setEditingUserId] = useState<number | null>(null);
  const [editUserName, setEditUserName] = useState("");

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

  const loadAllData = async () => {
    if (!organization?.organizationId) return;

    try {
      const users: UserResponseDto[] = await getAllUsers(
        organization.organizationId
      );
      setTableData(buildTableData(users));
    } catch (error) {
      console.error("Failed to load data:", error);
    }
  };

  // When server-side members prop changes, sync local state
  useEffect(() => {
    setTableData(buildTableData(members));
  }, [members]);

  /* ------------------------------------------------------------------------ */
  /*                        Invite Flow: Open Modal                           */
  /* ------------------------------------------------------------------------ */

  const handleOpenInviteModal = () => {
    setShowInviteModal(true);
  };

  /* ------------------------------------------------------------------------ */
  /*                          Invite Flow: Send Invitation                    */
  /* ------------------------------------------------------------------------ */

  const handleInviteUser = async () => {
    if (!inviteEmail) {
      toast.error(t.translations.PLEASE_ENTER_EMAIL_ADDRESS);
      return;
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(inviteEmail)) {
      toast.error(t.translations.PLEASE_ENTER_VALID_EMAIL_ADDRESS);
      return;
    }

    if (!organization?.organizationId) {
      toast.error(t.translations.NO_ORG_SELECTED);
      return;
    }

    try {
      setModalLoading(true);

      // Prepare organization invite data
      const inviteData: InviteUserToOrganizationRequestDto = {
        userEmail: inviteEmail,
        userName: inviteEmail.split("@")[0], // Extract username from email
      };

      // Call organization invite API
      await inviteUserToOrganization(
        organization.organizationId as number,
        inviteData
      );

      toast.success(`${t.translations.INVITATION_SENT_TO_} ${inviteEmail}`);

      // Refresh the user list
      await loadAllData();

      // Close modal and reset form
      setShowInviteModal(false);
      setInviteEmail("");
    } catch (error) {
      console.error("Error inviting user:", error);
      toast.error(t.translations.FAILED_TO_SEND_INVITATION);
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
        inviteData
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
      } else {
        if (!organization?.organizationId) {
          throw new Error("No organization selected");
        }

        await removeUserFromOrganization(
          organization.organizationId as number,
          confirmModal.itemId
        );

        toast.success(t.translations.USER_REMOVED_FROM_ORG);
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
          : t.translations.FAILED_TO_REMOVE_USER
      );
    } finally {
      setLoading(false);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                               Derived Stats                              */
  /* ------------------------------------------------------------------------ */

  const activeUserCount = tableData.filter(
    (u) => !u.isPending && u.isActive && !u.isArchived
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
            loading={loading}
            onInviteClick={handleOpenInviteModal}
          />

          {/* Combined Users & Pending Invites Table */}
          <UsersListTable
            tableData={tableData}
            loading={loading}
            onResendInvite={handleResendInvite}
            onEditUser={(id: number, name: string) => {
              setEditingUserId(id);
              setEditUserName(name);
            }}
            onOpenConfirm={(item: ConfirmModalState) => setConfirmModal(item)}
          />
        </div>
      </div>

      {/* Invite User Modal */}
      <InviteUserModal
        isOpen={showInviteModal}
        inviteEmail={inviteEmail}
        modalLoading={modalLoading}
        onClose={() => {
          setShowInviteModal(false);
          setInviteEmail("");
        }}
        onInvite={handleInviteUser}
        onChangeEmail={setInviteEmail}
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
            : t.translations.REMOVE_USER
        }
        message={
          confirmModal.isPending
            ? `${t.translations.SURE_YOU_WANT_TO_CANCEL_INVITATION_FOR_} ${confirmModal.itemName}? ${t.translations.THEY_WILL_NOT_BE_ABLE_TO_JOIN_WITH_LINK}`
            : `${t.translations.ARE_YOU_SURE_YOU_WANT_TO_REMOVE_} ${confirmModal.itemName} ${t.translations.THEY_WILL_LOSE_ACCESS_FROM_ALL_PROJECTS}`
        }
        confirmText={
          confirmModal.isPending
            ? t.translations.CANCEL_INVITE
            : t.translations.REMOVE
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
