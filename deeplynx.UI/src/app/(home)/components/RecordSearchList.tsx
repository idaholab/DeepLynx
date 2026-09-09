"use client";

import { useLanguage } from "@/app/contexts/Language";
import { ClockIcon, TagIcon } from "@heroicons/react/24/outline";
import { useRouter } from "next/navigation";
import React, { useEffect, useState } from "react";
import Skeleton from "react-loading-skeleton";
import { QueryRecordViewResponseDto, TagResponseDto } from "../types/responseDTOs";
import PaginationControls from "./PaginationControls";

const times = (n: number) => Array.from({ length: n }, (_, i) => i);

interface ListViewProps {
    data: QueryRecordViewResponseDto[];
    activeSearchTerms?: string[];
    selectedProjects?: number[];
    currentPage?: number;
    pageSize?: number;
    totalCount?: number;
    totalPages?: number;
    isLoading?: boolean;
    onPageChange?: (page: number) => void;
    onPageSizeChange?: (pageSize: number) => void;
}

const DEFAULT_RECORDS_PER_PAGE = 10;

const RecordSearchList: React.FC<ListViewProps> = ({
    data,
    activeSearchTerms = [],
    selectedProjects,
    currentPage,
    pageSize,
    totalCount,
    totalPages,
    isLoading = false,
    onPageChange,
    onPageSizeChange,
}) => {
    const { t } = useLanguage();
    const [localCurrentPage, setLocalCurrentPage] = useState(1);
    const [localPageSize, setLocalPageSize] = useState(DEFAULT_RECORDS_PER_PAGE);
    const router = useRouter();

    const isServerPaginated =
        typeof currentPage === "number" && typeof onPageChange === "function";
    const activePage = currentPage ?? localCurrentPage;
    const activePageSize = pageSize ?? localPageSize;

    useEffect(() => {
        if (!isServerPaginated) setLocalCurrentPage(1);
    }, [data, isServerPaginated]);

    const getHighlightedCell = (text: unknown, queries: string[]) => {
        const safeText = String(text);
        if (!queries.length) return { content: safeText, matched: false };

        const lowerText = safeText.toLowerCase();
        const match = queries.find((q) => lowerText.includes(q.toLowerCase()));

        if (!match) return { content: safeText, matched: false };

        const regex = new RegExp(`(${match})`, "gi");
        const parts = safeText.split(regex);

        const content = parts.map((part, index) =>
            regex.test(part) ? (
                <span
                    key={index}
                    className="font-bold text-primary bg-primary/20 rounded px-1"
                >
                    {part}
                </span>
            ) : (
                part
            )
        );
        return { content, matched: true };
    };

    const renderTags = (tags: string | null | undefined) => {
        if (!tags) return <span className="text-base-content/50 text-sm">No tags</span>;

        try {
            const parsed = JSON.parse(tags);
            const arr = Array.isArray(parsed) ? parsed : [parsed];

            const values = arr.flatMap((item: TagResponseDto) => {
                if (item && typeof item === "object") {
                    if (typeof item.name === "string") return [item.name];
                    return Object.values(item).filter((v) => typeof v === "string");
                }
                return [];
            });

            if (values.length === 0) {
                return <span className="text-base-content/50 text-sm">No tags</span>;
            }

            return (
                <div className="inline-flex flex-wrap gap-2">
                    {values.map((v, i) => (
                        <span key={`${v}-${i}`} className="badge badge-sm badge-success badge-outline">
                            {v}
                        </span>
                    ))}
                </div>
            );
        } catch {
            return <span className="text-base-content/50 text-sm">No tags</span>;
        }
    };

    const filteredRecords = !selectedProjects?.length
        ? data
        : data.filter(
            (record) =>
                record.projectId !== undefined &&
                selectedProjects.includes(Number(record.projectId))
        );

    const totalRecords = totalCount ?? filteredRecords.length;
    const resolvedTotalPages = Math.max(
        1,
        totalPages ?? Math.ceil(totalRecords / activePageSize),
    );
    const startIndex = (activePage - 1) * activePageSize;
    const endIndex = Math.min(startIndex + activePageSize, totalRecords);
    const paginatedRecords = isServerPaginated
        ? filteredRecords
        : filteredRecords.slice(startIndex, startIndex + activePageSize);

    const handlePageChange = (page: number) => {
        const nextPage = Math.min(Math.max(1, page), resolvedTotalPages);
        if (isServerPaginated) {
            onPageChange?.(nextPage);
            return;
        }
        setLocalCurrentPage(nextPage);
    };

    const handlePageSizeChange = (nextPageSize: number) => {
        if (isServerPaginated) {
            onPageSizeChange?.(nextPageSize);
            return;
        }
        setLocalPageSize(nextPageSize);
        setLocalCurrentPage(1);
    };

    return (
        <div className="w-full">
            {/* Results Header */}
            <div className="bg-base-200 rounded-t-xl px-6 py-4">
                <div className="flex items-center justify-between">
                    <div>
                        <h3 className="font-bold text-lg">Search Results</h3>
                        <p className="text-sm text-base-content/60">
                            Found {totalRecords} record{totalRecords !== 1 ? "s" : ""}
                        </p>
                    </div>

                    {/* Pagination Info */}
                    {resolvedTotalPages > 1 && (
                        <div className="text-sm text-base-content/60">
                            Showing {totalRecords === 0 ? 0 : startIndex + 1}-{endIndex} of {totalRecords}
                        </div>
                    )}
                </div>
            </div>

            {/* Results List */}
            <div className="max-h-[705px] overflow-y-auto rounded-b-xl border border-t-0 border-base-300/50 bg-base-100 shadow-sm">
                {isLoading ? (
                    <div className="divide-y divide-base-300">
                        {times(6).map((i) => (
                            <div key={i} className="p-6">
                                <Skeleton height={20} width="48%" />
                                <div className="mt-2">
                                    <Skeleton height={14} width="76%" />
                                </div>
                                <div className="mt-3 flex flex-wrap items-center gap-4">
                                    <Skeleton width={70} height={20} />
                                    <Skeleton width={150} height={16} />
                                    {i % 2 === 0 && <Skeleton width={90} height={20} />}
                                </div>
                                <div className="mt-3">
                                    <Skeleton width={160} height={18} />
                                </div>
                            </div>
                        ))}
                    </div>
                ) : paginatedRecords.length === 0 ? (
                    <div className="text-center py-16 text-base-content/60">
                        <p className="text-lg font-medium mb-2">No records found</p>
                        <p className="text-sm">Try adjusting your search criteria</p>
                    </div>
                ) : (
                    <div className="divide-y divide-base-300">
                        {paginatedRecords.map((record, index) => {
                            const name = getHighlightedCell(record.name, activeSearchTerms);
                            const desc = getHighlightedCell(
                                record.description,
                                activeSearchTerms
                            );
                            const className = getHighlightedCell(
                                record.className,
                                activeSearchTerms
                            );
                            const date = getHighlightedCell(
                                record.lastUpdatedAt,
                                activeSearchTerms
                            );

                            return (
                                <div
                                    key={record.id || index}
                                    className="p-6 hover:bg-base-200/50 cursor-pointer transition-colors"
                                    onClick={() =>
                                        router.push(
                                            `/record?recordId=${record.id}&projectId=${record.projectId}`
                                        )
                                    }
                                >
                                    {/* Record Name */}
                                    <h4 className="text-lg font-semibold mb-2 text-base-content">
                                        {name.content}
                                    </h4>

                                    {/* Description */}
                                    {record.description && (
                                        <p className="text-sm text-base-content/70 mb-3">
                                            {desc.content}
                                        </p>
                                    )}

                                    {/* Metadata Row */}
                                    <div className="flex flex-wrap items-center gap-4 mb-3">
                                        {/* Class Badge */}
                                        {record.className && (
                                            <div className="flex items-center gap-2">
                                                <span className="text-xs font-medium text-base-content/60 uppercase">
                                                    {t.translations.CLASS}
                                                </span>
                                                <span className="badge badge-sm badge-primary">
                                                    {className.content}
                                                </span>
                                            </div>
                                        )}

                                        {/* Last Updated */}
                                        {record.lastUpdatedAt && (
                                            <div className="flex items-center gap-2 text-sm text-base-content/60">
                                                <ClockIcon className="w-4 h-4" />
                                                <span className="font-medium">{t.translations.LAST_EDIT}:</span>
                                                <span>{date.content}</span>
                                            </div>
                                        )}

                                        {/* Data Source */}
                                        {record.dataSourceName && (
                                            <div className="flex items-center gap-2">
                                                <span className="text-xs font-medium text-base-content/60 uppercase">
                                                    Source
                                                </span>
                                                <span className="badge badge-sm badge-secondary">
                                                    {record.dataSourceName}
                                                </span>
                                            </div>
                                        )}
                                    </div>

                                    {/* Tags Row */}
                                    <div className="flex items-start gap-2">
                                        <div className="flex items-center gap-2 mt-0.5">
                                            <TagIcon className="w-4 h-4 text-base-content/60" />
                                            <span className="text-xs font-medium text-base-content/60 uppercase">
                                                {t.translations.TAGS}:
                                            </span>
                                        </div>
                                        {renderTags(record.tags)}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>

            {/* Pagination Controls */}
            <div className="mt-3">
                <PaginationControls
                    currentPage={activePage}
                    pageSize={activePageSize}
                    totalPages={resolvedTotalPages}
                    onPageChange={handlePageChange}
                    onPageSizeChange={handlePageSizeChange}
                />
            </div>
        </div>
    );
};

export default RecordSearchList;