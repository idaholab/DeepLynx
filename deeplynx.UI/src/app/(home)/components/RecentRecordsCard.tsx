"use client";
import { useLanguage } from "@/app/contexts/Language";
import { useRouter } from "next/navigation";
import React, { useEffect, useState, useMemo } from "react";
import CatalogViewSkeleton from "./skeletons/catalogviewskeleton";
import { QueryRecordViewResponseDto } from "@/app/(home)/types/responseDTOs";
import { formatLocalDateTime } from "@/app/lib/date_time";
import PaginationControls from "./PaginationControls";
import { useRecordsPaginated } from "@/app/hooks/useRecordsPaginated";
import { getAllClasses } from "@/app/lib/client_service/class_services.client";

interface Props {
    selectedProjects: string[];
    border?: boolean;
}

type RecentRecordSortValue = "nameAZ" | "nameZA" | "dateNew" | "dateOld";

const RecentRecordsCard: React.FC<Props> = ({
    selectedProjects,
    border = true,
}) => {
    const { t } = useLanguage();

    const sortOptions = useMemo(
        () => [
            {
                value: "nameAZ",
                label: t.translations.SORT_NAME_A_TO_Z,
            },
            {
                value: "nameZA",
                label: t.translations.SORT_NAME_Z_TO_A,
            },
            {
                value: "dateNew",
                label: t.translations.SORT_DATE_NEWEST,
            },
            {
                value: "dateOld",
                label: t.translations.SORT_DATE_OLDEST,
            },
        ],
        [t],
    );

    const {
        records,
        totalRecords,
        totalPages,
        sortBy,
        setSortBy,
        currentPage,
        setCurrentPage,
        pageSize,
        setPageSize,
        isLoading,
        requestFailed,
        fetchRecords,
    } = useRecordsPaginated(selectedProjects);

    const [classStatusMap, setClassStatusMap] = useState<Map<string, boolean>>(new Map());

    useEffect(() => {
        if (!selectedProjects || selectedProjects.length === 0) {
            setClassStatusMap(new Map());
            return;
        }

        let cancelled = false;

        const fetchClasses = async () => {
            try {
                const projectIds = selectedProjects.map((id) => Number(id));
                const classesArrays = await Promise.all(
                    projectIds.map((projectId) => getAllClasses(projectId, false)) // Fetch all classes, including archived ones
                );

                if (cancelled) return;

                const allClasses = classesArrays.flat();
                const statusMap = new Map(
                    allClasses.map((cls) => [cls.name, !cls.isArchived]) // Map class name to its "active" status
                );
                setClassStatusMap(statusMap);
            } catch (error) {
                console.error("Failed to fetch classes:", error);
                if (!cancelled) setClassStatusMap(new Map());
            }
        };

        fetchClasses();

        return () => {
            cancelled = true;
        };
    }, [selectedProjects]);

    if (isLoading && records.length === 0) return <CatalogViewSkeleton />;

    return (
        <div
            className={border ? "shadow-md shadow-base-content/10 rounded-xl" : ""}
        >
            {/* Header and sort controls */}
            <div className="flex items-center justify-between p-4">
                <h2 className="text-lg font-semibold text-base-content">
                    {t.translations.RECENTLY_ADDED_RECORDS}
                </h2>

                <BasicSortSelect
                    value={sortBy}
                    options={sortOptions}
                    onChange={setSortBy}
                />
            </div>

            <div className="divider m-0"></div>

            {/* Error state */}
            {requestFailed && (
                <div className="p-4 text-error flex items-center justify-between">
                    <span>{t.translations.FAILED_TO_LOAD_RECENT_RECORDS}</span>
                    <button className="btn btn-sm btn-outline" onClick={fetchRecords}>
                        {t.translations.RETRY}
                    </button>
                </div>
            )}

            {/* Paginated records list */}
            <ul className="space-y-1 p-2">
                {records.map((record) => (
                    <RecordView
                        record={record}
                        key={record.id}
                        isActiveClass={record.className ? classStatusMap.get(record.className) ?? false : false}
                    />
                ))}
            </ul>

            {/* Empty state */}
            {!requestFailed && records.length === 0 && (
                <div className="text-center py-8 text-base-content/60">
                    {t.translations.NO_RECENT_RECORDS}
                </div>
            )}

            {/* Shared pagination controls */}
            <PaginationControls
                currentPage={currentPage}
                pageSize={pageSize}
                totalPages={totalPages}
                onPageChange={setCurrentPage}
                onPageSizeChange={setPageSize}
            />
        </div>
    );
};

interface BasicSortSelectProps {
    value: string;
    options: {
        value: string;
        label: string;
    }[];
    onChange: (value: string) => void;
}

/**
 * A select input for sorting values.
 */
function BasicSortSelect({ value, options, onChange }: BasicSortSelectProps) {
    const { t } = useLanguage();

    if (!options.length) return null;

    return (
        <div className="flex items-center gap-1">
            <div className="px-3 py-2 text-md font-semibold text-base-content/50">
                {t.translations.SORT_BY}
            </div>
            <div className="relative inline-block">
                <select
                    value={value}
                    onChange={(e) => onChange(e.target.value)}
                    className="select"
                >
                    {options.map((option) => (
                        <option key={option.value} value={option.value}>
                            {option.label}
                        </option>
                    ))}
                </select>
            </div>
        </div>
    );
}

/**
 * A view of a single record that redirects users to the record's page.
 */
function RecordView({
    record,
    isActiveClass,
}: {
    record: QueryRecordViewResponseDto;
    isActiveClass: boolean;
}) {
    const { t } = useLanguage();
    const router = useRouter();

    const handleRecordClick = () => {
        router.push(`/record?recordId=${record.id}&projectId=${record.projectId}`);
    };

    return (
        <li
            key={record.id}
            className="border-b border-base-content/40 cursor-pointer hover:bg-base-100/40 p-3 -mx-1 transition-colors"
            onClick={() => handleRecordClick()}
        >
            <div className="font-medium text-base-content mb-2 line-clamp-1 overflow-hidden break-all">
                {record.name}
            </div>

            <div className="text-sm text-base-content/60 flex flex-wrap gap-x-4 gap-y-1">
                <span className="flex items-center gap-1">
                    <span>{t.translations.CLASS}: </span>
                    <span className="badge badge-sm badge-secondary">
                        {isActiveClass ? record.className : t.translations.NO_CLASS}
                    </span>
                </span>

                <span>
                    <span className="text-base-content/50">
                        {t.translations.LAST_EDIT}:
                    </span>{" "}
                    {formatLocalDateTime(String(record.lastUpdatedAt))}
                </span>

                <span>
                    <span className="text-base-content/50">
                        {t.translations.DATA_SOURCE}:
                    </span>{" "}
                    {record.dataSourceName}
                </span>
            </div>
        </li>
    );
}

export default RecentRecordsCard;
