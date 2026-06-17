"use client";

import { useLanguage } from "@/app/contexts/Language";
import { ChevronLeftIcon, ChevronRightIcon, ClockIcon, TagIcon } from "@heroicons/react/24/outline";
import { useRouter } from "next/navigation";
import React, { useState } from "react";
import { QueryRecordViewResponseDto, TagResponseDto } from "../types/responseDTOs";

interface ListViewProps {
    data: QueryRecordViewResponseDto[];
    activeSearchTerms?: string[];
    selectedProjects?: number[];
}

const RECORDS_PER_PAGE = 10;

const RecordSearchList: React.FC<ListViewProps> = ({
    data,
    activeSearchTerms = [],
    selectedProjects,
}) => {
    const { t } = useLanguage();
    const [currentPage, setCurrentPage] = useState(1);
    const router = useRouter();

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

    const totalPages = Math.ceil(filteredRecords.length / RECORDS_PER_PAGE);
    const startIndex = (currentPage - 1) * RECORDS_PER_PAGE;
    const paginatedRecords = filteredRecords.slice(
        startIndex,
        startIndex + RECORDS_PER_PAGE
    );

    return (
        <div className="w-full">
            {/* Results Header */}
            <div className="bg-base-200 rounded-t-xl px-6 py-4">
                <div className="flex items-center justify-between">
                    <div>
                        <h3 className="font-bold text-lg">Search Results</h3>
                        <p className="text-sm text-base-content/60">
                            Found {filteredRecords.length} record{filteredRecords.length !== 1 ? 's' : ''}
                        </p>
                    </div>

                    {/* Pagination Info */}
                    {totalPages > 1 && (
                        <div className="text-sm text-base-content/60">
                            Showing {startIndex + 1}-{Math.min(startIndex + RECORDS_PER_PAGE, filteredRecords.length)} of {filteredRecords.length}
                        </div>
                    )}
                </div>
            </div>

            {/* Results List */}
            <div className="bg-base-100 rounded-b-xl overflow-hidden">
                {paginatedRecords.length === 0 ? (
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

                {/* Pagination Controls */}
                {totalPages > 1 && (
                    <div className="bg-base-200 border-t-2 border-base-300 px-6 py-4">
                        <div className="flex items-center justify-between">
                            <div className="text-sm text-base-content/60">
                                Page {currentPage} of {totalPages}
                            </div>

                            <div className="flex gap-2">
                                <button
                                    className="btn btn-sm btn-ghost"
                                    disabled={currentPage === 1}
                                    onClick={() => setCurrentPage((prev) => prev - 1)}
                                >
                                    <ChevronLeftIcon className="w-5 h-5" />
                                    Previous
                                </button>

                                {/* Page Numbers */}
                                <div className="flex gap-1">
                                    {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                                        let pageNum;
                                        if (totalPages <= 5) {
                                            pageNum = i + 1;
                                        } else if (currentPage <= 3) {
                                            pageNum = i + 1;
                                        } else if (currentPage >= totalPages - 2) {
                                            pageNum = totalPages - 4 + i;
                                        } else {
                                            pageNum = currentPage - 2 + i;
                                        }

                                        return (
                                            <button
                                                key={pageNum}
                                                className={`btn btn-sm ${currentPage === pageNum
                                                    ? "btn-primary"
                                                    : "btn-ghost"
                                                    }`}
                                                onClick={() => setCurrentPage(pageNum)}
                                            >
                                                {pageNum}
                                            </button>
                                        );
                                    })}
                                </div>

                                <button
                                    className="btn btn-sm btn-ghost"
                                    disabled={currentPage === totalPages}
                                    onClick={() => setCurrentPage((prev) => prev + 1)}
                                >
                                    Next
                                    <ChevronRightIcon className="w-5 h-5" />
                                </button>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

export default RecordSearchList;