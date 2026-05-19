import { useLanguage } from "@/app/contexts/Language";
import { OauthApplicationResponseDto } from "../../types/responseDTOs";
import { useEffect, useState } from "react";
import { Column } from "../../types/types";
import { PencilIcon, PlusIcon, TrashIcon } from "@heroicons/react/24/outline";
import GenericTable from "../GenericTable";
import CreateOAuthModal from "./CreateOauthModal";
import EditOAuthApplication from "./EditOAuthApplicationModal";
import {
  getAllOauthApplications,
  archiveOauthApplication,
} from "@/app/lib/client_service/oauth_services.client";
import { SiteManagementTable } from "./SiteManagementTable";

interface Props {
  initialApplications: OauthApplicationResponseDto[];
  onApplicationsChange?: () => Promise<void>; // Add this prop
}

const OAuthManagement = ({ initialApplications, onApplicationsChange }: Props) => {
  const { t } = useLanguage();
  const [data, setData] = useState<OauthApplicationResponseDto[]>(initialApplications);
  const [isOAuthApplicationModalOpen, setIsOAuthApplicationModalOpen] =
    useState(false);
  const [editOAuthApplicationModal, setEditOAuthApplicationModal] =
    useState(false);
  const [selectedOAuthApplicationId, setSelectedOAuthApplicationId] = useState<
    number | null
  >(null);
  const [selectedOAuthApplicationName, setSelectedOAuthApplicationName] =
    useState<string>("");
  const [
    selectedOAuthApplicationDescription,
    setSelectedOAuthApplicationDescription,
  ] = useState<string>("");
  const [
    selectedOAuthApplicationCallBackURL,
    setSelectedOAuthApplicationCallBackURL,
  ] = useState<string>("");
  const [selectedOAuthApplicationBaseURL, setSelectedOAuthApplicationBaseURL] =
    useState<string>("");
  const [
    selectedOAuthApplicationAppOwnerEmail,
    setSelectedOAuthApplicationAppOwnerEmail,
  ] = useState<string>("");
  const [selectedOAuthApplications, setSelectedOAuthApplications] = useState<
    boolean[]
  >([]);
  const [selectAll, setSelectAll] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setSelectedOAuthApplications(new Array(data.length).fill(false));
    setSelectAll(false);
  }, [data.length]);

  const refreshOAuthApplications = async () => {
    try {
      const updatedData = await getAllOauthApplications();
      setData(updatedData);
      // Notify parent component to update its state too
      if (onApplicationsChange) {
        await onApplicationsChange();
      }
    } catch (err) {
      console.error("Failed to refresh organizations:", err);
      setError("Failed to refresh organizations.");
    }
  };

  const handleSelectAll = () => {
    const next = !selectAll;
    setSelectAll(next);
    setSelectedOAuthApplications(new Array(data.length).fill(next));
  };

  const handleCheckboxChange = (index: number) => {
    const next = [...selectedOAuthApplications];
    next[index] = !next[index];
    setSelectedOAuthApplications(next);
    setSelectAll(next.every(Boolean));
  };

  const handleDelete = async (index: number) => {
    const oauthId = data[index].id as number;
    try {
      await archiveOauthApplication(oauthId);
      setData((prev) => prev.filter((_, i) => i !== index));
    } catch (err) {
      console.error("Failed to delete organization:", err);
      setError("Failed to delete organization.");
    }
  };

  const handleDeleteSelected = async () => {
    const selectedOAuthIds = data
      .filter((_, i) => selectedOAuthApplications[i])
      .map((app) => app.id);
    try {
      await Promise.all(
        selectedOAuthIds.map((appId) =>
          archiveOauthApplication(appId as number)
        )
      );
      setData((prev) => prev.filter((_, i) => !selectedOAuthApplications[i]));
    } catch (err) {
      console.error("Failed to delete selected organizations:", err);
      setError("Failed to delete selected organizations.");
    }
  };

  const multipleSelected = () =>
    selectedOAuthApplications.filter(Boolean).length > 1;

  const openEditModal = (
    id: number,
    name: string,
    description: string,
    callbackUrl: string,
    baseUrl: string,
    appOwnerEmail: string
  ) => {
    setSelectedOAuthApplicationId(id);
    setSelectedOAuthApplicationName(name);
    setSelectedOAuthApplicationDescription(description);
    setSelectedOAuthApplicationCallBackURL(callbackUrl);
    setSelectedOAuthApplicationBaseURL(baseUrl);
    setSelectedOAuthApplicationAppOwnerEmail(appOwnerEmail);
    setEditOAuthApplicationModal(true);
  };
  console.log("Data",
    data
  )

  const columns: Column<OauthApplicationResponseDto>[] = [
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
          checked={!!selectedOAuthApplications[index]}
          onChange={() => handleCheckboxChange(index)}
        />
      ),
      sortable: false,
    },
    {
      header: t.translations.NAME,
      cell: (row) => {
        const name = (row as OauthApplicationResponseDto).name;
        return (
          <span title={name}>
            {name.length > 20 ? name.slice(0, 20) + "..." : name}
          </span>
        );
      },
    },
    {
      header: t.translations.DESCRIPTION,
      data: "description" as keyof OauthApplicationResponseDto,
    },
    {
      header: t.translations.CALLBACK_URL,
      data: "callbackUrl" as keyof OauthApplicationResponseDto,
    },
    {
      header: t.translations.BASE_URL,
      data: "baseUrl" as keyof OauthApplicationResponseDto,
    },
    {
      header: t.translations.APP_OWNER_EMAIL,
      data: "appOwnerEmail" as keyof OauthApplicationResponseDto,
    },
    {
      header: "",
      cell: (row) => (
        <div className="flex">
          <button
            onClick={() =>
              openEditModal(
                row.id as number,
                row.name,
                row.description as string,
                row.callbackUrl,
                row.baseUrl as string,
                row.appOwnerEmail as string
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
            <button onClick={handleDeleteSelected}>
              <TrashIcon className="size-6 text-red-500" />
            </button>
          )}
        </div>
      ),
      cell: (_row, index) => (
        <div className="flex">
          <button onClick={() => handleDelete(index)}>
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
          <h1 className="text-2xl font-bold">OAuth Application</h1>
        </div>
        <p className="text-base-content/70">
          Register and manage OAuth 2.0 applications for secure third-party
          integrations with your organization's resources.
        </p>
      </div>
      <div className="flex justify-end p-4 mr-4">
        <button
          className="btn btn-secondary btn-sm flex-1 sm:flex-initial"
          data-tour="create-project"
          onClick={() => setIsOAuthApplicationModalOpen(true)}
        >
          <PlusIcon className="size-5" />
          <span>{t.translations.OAUTH_APPLICATION}</span>
        </button>
      </div>
      {error && <div className="p-4 text-red-500">{error}</div>}

      <SiteManagementTable columns={columns} data={data} expandableKey="description" rowKey="id" border />

      <CreateOAuthModal
        isOpen={isOAuthApplicationModalOpen}
        onClose={() => setIsOAuthApplicationModalOpen(false)}
        onOAuthApplicationCreated={refreshOAuthApplications}
      />
      {selectedOAuthApplicationId !== null && (
        <EditOAuthApplication
          isOpen={editOAuthApplicationModal}
          onClose={() => setEditOAuthApplicationModal(false)}
          oAuthApplicationId={selectedOAuthApplicationId}
          oAuthApplicationName={selectedOAuthApplicationName}
          oAuthApplicationCallbackURL={selectedOAuthApplicationCallBackURL}
          oAuthApplicationDescription={selectedOAuthApplicationDescription}
          oAuthApplicationBaseURL={selectedOAuthApplicationBaseURL}
          oAuthApplicationAppOwnerEmail={selectedOAuthApplicationAppOwnerEmail}
          onOAuthApplicationUpdated={refreshOAuthApplications}
        />
      )}
    </div>
  );
};

export default OAuthManagement;
