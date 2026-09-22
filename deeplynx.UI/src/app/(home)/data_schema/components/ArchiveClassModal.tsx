// src/app/(home)/data_schema/components/ArchiveClassModal.tsx
"use client";

import { useLanguage } from "@/app/contexts/Language";

interface ArchiveClassModalProps {
    isOpen: boolean;
    onToggle: (value: boolean) => void;
    archiveAction: boolean;
    onArchive: () => void;
    recordsForClass: number | undefined;
    relationshipCount: number | undefined;
}

const ArchiveClassModal = ({
    isOpen,
    onToggle,
    archiveAction,
    onArchive,
    recordsForClass,
    relationshipCount
}: ArchiveClassModalProps) => {
    const { t } = useLanguage();

    return (
        <>
            <input
                type="checkbox"
                id="archive_class_modal"
                className="modal-toggle"
                checked={isOpen}
                onChange={() => onToggle(!isOpen)}
            />
            <div className="modal" role="dialog">
                <div className="modal-box">
                <h3 className="text-lg font-bold">
                    {archiveAction ? t.translations.ARCHIVE : t.translations.UNARCHIVE}{" "}
                    {t.translations.CLASS}
                </h3>
                <p className="py-4">
                    {t.translations.ARE_YOU_SURE_YOU_WANT_TO_}{" "}
                    {archiveAction ? t.translations.ARCHIVE : t.translations.UNARCHIVE}{" "}
                    {t.translations._THIS_CLASS}
                </p>
                {archiveAction &&
                    <div className="text-sm">
                        {(recordsForClass ?? 0) > 0 &&
                        <div className="py-2">
                            <p>{t.translations.THERE_ARE} {recordsForClass} {t.translations.RECORDS_WITH_THIS_CLASS}</p>
                            <p>{t.translations.ANY_RECORD_CLASS_REMOVED}</p>
                        </div>
                        }
                        {(relationshipCount ?? 0) > 0 && 
                        <div className="py-2">    
                            <p>{t.translations.THERE_ARE} {relationshipCount} {t.translations.RELATIONSHIPS_WITH_THIS_CLASS}</p>
                            <p>{t.translations.ANY_RELATIONSHIP_CLASS_UNABLE_TO_EDIT}</p>
                        </div>
                        }
                    </div>
                }
                <div className="modal-action">
                    <button className="btn" onClick={() => onToggle(false)}>
                    {t.translations.CANCEL}
                    </button>
                    <button className="btn btn-warning" onClick={onArchive}>
                    {archiveAction
                        ? t.translations.ARCHIVE
                        : t.translations.UNARCHIVE}
                    </button>
                </div>
                </div>
                <label className="modal-backdrop" onClick={() => onToggle(false)}>
                {t.translations.CLOSE}
                </label>
            </div>
        </>
    );
}

export default ArchiveClassModal;