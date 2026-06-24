import {
  ProjectMemberResponseDto,
  RoleResponseDto,
} from "@/app/(home)/types/responseDTOs";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

export type MemberType = "user" | "group";

export type ProjectMemberTableRow = {
  memberId: number;
  name: string;
  email: string | null;
  role: string | null;
  roleId: number | null;
  memberType: MemberType;
  isProjectAdmin: boolean;
};

export type ConfirmModalState = {
  isOpen: boolean;
  memberId: number | null;
  memberName: string;
  memberType: MemberType | null;
  isPending: boolean;
};

export type AddMemberModalState = {
  isOpen: boolean;
  memberType: MemberType;
};

export type EditRoleModalState = {
  isOpen: boolean;
  memberId: number | null;
  memberName: string;
  memberType: MemberType | null;
  currentRoleId: number | null;
  currentIsProjectAdmin: boolean;
};

/* -------------------------------------------------------------------------- */
/*                            Helper: build table rows                        */
/* -------------------------------------------------------------------------- */

export const buildTableData = (
  members: ProjectMemberResponseDto[]
): ProjectMemberTableRow[] => {
  return members.map((m) => {
    const memberType: MemberType = m.email ? "user" : "group";

    return {
      memberId: m.memberId as number,
      name: m.name ?? "",
      email: m.email ?? null,
      role: m.role ?? null,
      roleId: m.roleId ?? null,
      memberType,
      isProjectAdmin: m.isProjectAdmin ?? false,
    };
  });
};
