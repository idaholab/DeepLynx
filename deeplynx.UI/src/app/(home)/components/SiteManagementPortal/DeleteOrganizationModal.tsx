import { useLanguage } from "@/app/contexts/Language";
import { archiveOrganization } from "@/app/lib/client_service/organization_services.client";
import toast from "react-hot-toast";
import { OrganizationResponseDto } from "../../types/responseDTOs";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";

interface DeleteOrganizationProps {
    isOpen: boolean;
    onClose: () => void;
    organizations: OrganizationResponseDto[] | null;
    onOrganizationDeleted: () => void;
}

const DeleteOrganization = ({
    isOpen,
    onClose,
    organizations,
    onOrganizationDeleted,
}: DeleteOrganizationProps) => {
    const { t } = useLanguage();
    const activeOrgSession = useOrganizationSession();
    const activeOrg = activeOrgSession.organization;
    
    const handleDelete = async () => {
        try {
            if (!organizations?.length) return;

            const orgsToDelete = organizations.filter((org) => Number(org.id) !== Number(activeOrg?.organizationId)) // not the current organization
            const skippedOrg = organizations.filter((org) => Number(org.id) === Number(activeOrg?.organizationId)) // the active organization

            await Promise.all(
                orgsToDelete
                    .map(async (org) => {
                        await archiveOrganization(Number(org.id));
                    }
                )
            );
            
            onOrganizationDeleted();

            if (orgsToDelete.length > 0) {
                toast.success(`Successfully deleted ${orgsToDelete.length} organization(s)`);
            }
            if (skippedOrg.length > 0) {
                toast.error("You cannot delete the active organization.");
            }
        } catch (error) {
            console.error("Error deleting organization: ", error);
            toast.error("An error occurred while deleting the organization.");
        }
        onClose();
    };

    function organizationList() {
        return (
            <ul className="p-3">
                {organizations?.map((org) => (
                    <li key={org.id}>{org.name}</li>
                ))}
            </ul>
        )
    }

    return (
        <>
            {isOpen && (
            <dialog className="modal modal-open">
            <div className="modal-box max-w-lg">
                <h3 className="font-bold text-lg mb-4 text-neutral">
                {t.translations.DELETE_ORGANIZATION}
                </h3>
                <h4 className="text-center">
                    Are you sure you want to delete: 
                        <strong>
                            {organizationList()}
                        </strong>
                    {/* <span className="font-bold text-red-500">This action cannot be undone.</span> */}
                </h4>
                <div className="modal-action">
                <button type="button" className="btn" onClick={onClose}>
                    {t.translations.CANCEL}
                </button>
                <button
                    type="submit"
                    className="btn bg-red-600 text-gray-200 font-semibold border-red-100 hover:bg-red-700"
                    onClick={handleDelete}
                >
                    {t.translations.DELETE}
                </button>
                </div>
            </div>
            </dialog>
        )}
        </>
    )
}

export default DeleteOrganization;