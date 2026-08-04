"use client";

import { ObjectStorageResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";

interface AzureObjectConfig {
    AzureFilePath?: string;
}

interface StorageConfig {
    AzureObjectConfig?: AzureObjectConfig;
}

interface StorageFormData {
    name: string;
    config: StorageConfig;
    default: boolean;
    createContainerPerProject: boolean;
    existingStorage?: boolean;
}

interface EditStorageModalProps {
    isOpen: boolean;
    onToggle: (value: boolean) => void;
    storageFormData: StorageFormData;
    setStorageFormData: (value: StorageFormData) => void;
    onEdit: () => void;
    setEditingStorage: (value: ObjectStorageResponseDto | null) => void;
}

const EditStorageModal = ({
    isOpen,
    onToggle,
    storageFormData,
    setStorageFormData,
    onEdit,
    setEditingStorage,
}: EditStorageModalProps) => {
    const { t } = useLanguage();

    return (
        <>
            <input
                type="checkbox"
                id="edit_storage_modal"
                className="modal-toggle"
                checked={isOpen}
                onChange={() => onToggle(!isOpen)}
            />
            <div className="modal" role="dialog">
                <div className="modal-box">
                    <h3 className="text-lg font-bold mb-4">{t.translations.EDIT_STORAGE}</h3>

                    {/* Storage Name */}
                    <div className="form-control mb-4">
                        <label className="label">
                            <span className="label-text">{t.translations.STORAGE_NAME} *</span>
                        </label>
                        <input
                            type="text"
                            placeholder="e.g., Primary Storage"
                            className="input input-bordered"
                            value={storageFormData.name}
                            onChange={(e) =>
                                setStorageFormData({ ...storageFormData, name: e.target.value })
                            }
                        />
                    </div>

                    {/* Set as Default Storage */}
                    <div className="form-control mb-4">
                        <label className="cursor-pointer label flex items-center gap-2">
                            <input
                                type="checkbox"
                                className="checkbox checkbox-primary"
                                checked={storageFormData.default}
                                onChange={(e) =>
                                    setStorageFormData({
                                        ...storageFormData,
                                        default: e.target.checked,
                                    })
                                }
                            />
                            <span className="label-text">{t.translations.SET_AS_DEFAULT_STORAGE}</span>
                        </label>
                    </div>

                    {/* Create Container Per Project */}
                    <div className="form-control mb-4">
                        <label className="cursor-pointer label flex items-center gap-2">
                            <input
                                type="checkbox"
                                className="checkbox checkbox-primary"
                                checked={storageFormData.createContainerPerProject || false}
                                onChange={(e) =>
                                    setStorageFormData({
                                        ...storageFormData,
                                        createContainerPerProject: e.target.checked,
                                    })
                                }
                            />
                            <span className="label-text">{t.translations.CREATE_CONTAINER_PER_PROJECT} ({t.translations.AZURE_TYPE_ONLY})</span>
                        </label>
                    </div>

                    {/* Actions */}
                    <div className="modal-action">
                        <button
                            className="btn"
                            onClick={() => {
                                onToggle(false);
                                setEditingStorage(null);
                                setStorageFormData({ name: "", config: {}, default: false, createContainerPerProject: false });
                            }}
                        >
                            {t.translations.CANCEL}
                        </button>
                        <button className="btn btn-primary" onClick={onEdit}>
                            {t.translations.SAVE_CHANGES}
                        </button>
                    </div>
                </div>
                <label className="modal-backdrop" onClick={() => onToggle(false)}>
                    {t.translations.CLOSE}
                </label>
            </div>
        </>
    );
};

export default EditStorageModal;