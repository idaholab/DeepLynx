import { useLanguage } from "@/app/contexts/Language";
import { OrganizationResponseDto } from "../../types/responseDTOs";
import { useEffect, useState } from "react";
import { Column } from "../../types/types";
import { PencilIcon, PlusIcon, TrashIcon } from "@heroicons/react/24/outline";
import GenericTable from "../GenericTable";
import CreateOrganization from "./CreateOrganizationModal";
import EditOrganization from "./EditOrganizationModal";
import DeleteOrganization from "./DeleteOrganizationModal";
import {
  archiveOrganization,
  getAllOrganizations,
} from "@/app/lib/client_service/organization_services.client";
import { SiteManagementTable } from "./SiteManagementTable";

interface OrganizationManagementProps {
  initialOrganizations: OrganizationResponseDto[];
  onOrganizationsChange?: () => Promise<void>; // Add this prop
}

const SiteOrganizationManagement = ({
  initialOrganizations,
  onOrganizationsChange
}: OrganizationManagementProps) => {
  const { t } = useLanguage();
  const [data, setData] =
    useState<OrganizationResponseDto[]>(initialOrganizations);
  const [isOrganizationModalOpen, setIsOrganizationModalOpen] = useState(false);
  const [editOrganizationModal, setEditOrganizationModal] = useState(false);
  const [deleteOrganizationModal, setDeleteOrganizationModal] = useState(false);
  const [selectedOrganizationId, setSelectedOrganizationId] = useState<
    number | null
  >(null);
  const [selectedOrganizationName, setSelectedOrganizationName] =
    useState<string>("");
  const [selectedOrganizationDescription, setSelectedOrganizationDescription] =
    useState<string>("");
  const [selectedOrganizations, setSelectedOrganizations] = useState<boolean[]>(
    []
  );
  const [selectedForDeletion, setSelectedForDeletion] = useState<OrganizationResponseDto[] | null>(null);
  const [selectAll, setSelectAll] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setSelectedOrganizations(new Array(data.length).fill(false));
    setSelectAll(false);
  }, [data.length]);

  const refreshOrganizations = async () => {
    try {
      const updatedData = await getAllOrganizations();
      setData(updatedData);
      // Notify parent component to update its state too
      if (onOrganizationsChange) {
        await onOrganizationsChange();
      }
    } catch (err) {
      console.error("Failed to refresh organizations:", err);
      setError("Failed to refresh organizations.");
    }
  };

  const handleSelectAll = () => {
    const next = !selectAll;
    setSelectAll(next);
    setSelectedOrganizations(new Array(data.length).fill(next));
  };

  const handleCheckboxChange = (index: number) => {
    const next = [...selectedOrganizations];
    next[index] = !next[index];
    setSelectedOrganizations(next);
    setSelectAll(next.every(Boolean));
  };

  const multipleSelected = () =>
    selectedOrganizations.filter(Boolean).length > 1;

  const openEditModal = (
    organizationId: number,
    organizationName: string,
    organizationDescription: string
  ) => {
    setSelectedOrganizationId(organizationId);
    setSelectedOrganizationName(organizationName);
    setSelectedOrganizationDescription(organizationDescription);
    setEditOrganizationModal(true);
  };

  const openDeleteModal = (
    organizations: OrganizationResponseDto[] | null
  ) => {
    setSelectedForDeletion(organizations);
    setDeleteOrganizationModal(true);
  };

  const columns: Column<OrganizationResponseDto>[] = [
    {
      header: (
        <input
          type="checkbox"
          className="checkbox"
          checked={selectAll}
          onChange={handleSelectAll}
        />
      ),
      cell: (_row, index) => (
        <input
          type="checkbox"
          className="checkbox"
          checked={!!selectedOrganizations[index]}
          onChange={() => handleCheckboxChange(index)}
        />
      ),
      sortable: false,
    },
    {
      header: t.translations.NAME,
      cell: (row) => {
        const name = (row as OrganizationResponseDto).name;
        return (
          <span title={name}>
            {name.length > 20 ? name.slice(0, 20) + "..." : name}
          </span>
        );
      },
    },
    {
      header: t.translations.DESCRIPTION,
      data: "description" as keyof OrganizationResponseDto,
    },
    {
      header: "",
      cell: (row) => (
        <div className="flex">
          <button className="btn btn-ghost btn-sm"
            onClick={() =>
              openEditModal(
                row.id as number,
                row.name,
                row.description as string
              )
            }
          >
            <PencilIcon className="size-6 text-secondary" />
          </button>
        </div>
      ),
      sortable: false,
    },
    {
      header: (
        <div className="flex">
          {multipleSelected() && (
            <button className="btn btn-ghost btn-sm"
              onClick={() => {
                const selectedOrgs = data
                  .filter((_, i) => selectedOrganizations[i]);
                openDeleteModal(selectedOrgs)
              }}>
              <TrashIcon className="size-6 text-red-500" />
            </button>
          )}
        </div>
      ),
      cell: (_row, index) => (
        <div className="flex">
          <button className="btn btn-ghost btn-sm"
            onClick={() => {
              openDeleteModal([_row])
            }}>
            <TrashIcon className="size-6 text-red-500" />
          </button>
        </div>
      ),
      sortable: false,
    },
  ];

  return (
    <div className="p-6">
      {/* Header */}
      <div className="mb-6">
        <div className="flex items-center justify-between mb-2">
          <h1 className="text-2xl font-bold">Organization Management</h1>
        </div>
        <p className="text-base-content/70">
          Manage organizations and assign administrators to control access and
          oversee projects within each organizational unit.
        </p>
      </div>
      <div className="flex justify-end p-4 mr-4">
        <button
          className="btn btn-secondary btn-sm flex-1 sm:flex-initial"
          data-tour="create-project"
          onClick={() => setIsOrganizationModalOpen(true)}
        >
          <PlusIcon className="size-5" />
          <span>{t.translations.ORGANIZATION}</span>
        </button>
      </div>
      {error && <div className="p-4 text-red-500">{error}</div>}

      <SiteManagementTable columns={columns} data={data} expandableKey="description" rowKey="id" border />

      <CreateOrganization
        isOpen={isOrganizationModalOpen}
        onClose={() => setIsOrganizationModalOpen(false)}
        onOrganizationCreated={refreshOrganizations}
      />
      {selectedOrganizationId !== null && (
        <EditOrganization
          isOpen={editOrganizationModal}
          onClose={() => setEditOrganizationModal(false)}
          organizationId={selectedOrganizationId}
          organizationName={selectedOrganizationName}
          organizationDescription={selectedOrganizationDescription}
          onOrganizationUpdated={refreshOrganizations}
        />
      )}
      {selectedForDeletion !== null && (
        <DeleteOrganization
          isOpen={deleteOrganizationModal}
          onClose={() => setDeleteOrganizationModal(false)}
          organizations={selectedForDeletion}
          onOrganizationDeleted={refreshOrganizations}
        />
      )}
    </div>
  );
};

export default SiteOrganizationManagement;
