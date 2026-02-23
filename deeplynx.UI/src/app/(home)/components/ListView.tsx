"use client";

import { useLanguage } from "@/app/contexts/Language";
import { ChevronLeftIcon, ChevronRightIcon } from "@heroicons/react/24/outline";
import { useRouter } from "next/navigation";
import React, { useEffect, useState } from "react";
import { formatLocalDateTime } from "@/app/lib/date_time";
import { RecordTableRow } from "../types/types";
import { TagResponseDto } from "../types/responseDTOs";

interface ListViewProps {
  data: RecordTableRow[];
  activeSearchTerms?: string[];
  selectedProjects?: number[];
}

const RECORDS_PER_PAGE = 5;

const ListView: React.FC<ListViewProps> = ({
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
          className="font-bold text-info-content bg-info rounded px-1"
        >
          {part}
        </span>
      ) : (
        part
      ),
    );
    return { content, matched: true };
  };

  const renderTags = (tags: string | null | undefined) => {
    if (!tags) return null;

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

      return (
        <span className="inline-flex flex-wrap gap-2">
          {values.map((v, i) => (
            <span
              key={`${v}-${i}`}
              className="badge badge-sm badge-secondary badge-outline"
            >
              {v}
            </span>
          ))}
        </span>
      );
    } catch {
      return null;
    }
  };

  const filteredRecords = !selectedProjects?.length
    ? data
    : data.filter(
        (record) =>
          record.projectId !== undefined &&
          selectedProjects.includes(record.projectId),
      );

  const totalPages = Math.ceil(filteredRecords.length / RECORDS_PER_PAGE);
  const startIndex = (currentPage - 1) * RECORDS_PER_PAGE;
  const paginatedRecords = filteredRecords.slice(
    startIndex,
    startIndex + RECORDS_PER_PAGE,
  );

  useEffect(() => {
    if (totalPages === 0) {
      setCurrentPage(1);
      return;
    }
    if (currentPage > totalPages) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);

  return (
    <div className="bg-base-100 px-3 sm:px-6 lg:px-10 w-full mx-auto text-info-content">
      <ul className="list">
        {paginatedRecords.map((record, index) => {
          const name = getHighlightedCell(record.name, activeSearchTerms);
          const desc = getHighlightedCell(
            record.description,
            activeSearchTerms,
          );
          const className = getHighlightedCell(
            record.className,
            activeSearchTerms,
          );
          // const time = getHighlightedCell(record.timeseries, activeSearchTerms);
          const formattedDate = formatLocalDateTime(record.lastUpdatedAt);
          const date = getHighlightedCell(formattedDate, activeSearchTerms);
          return (
            <li
              key={index}
              className="py-4 mb-2 card cursor-pointer hover:bg-base-200/30 p-3 shadow-md rounded"
              onClick={() =>
                router.push(
                  `/record?recordId=${record.id}&projectId=${record.projectId}`,
                )
              }
            >
              <div className="mb-1 text-base sm:text-lg break-words">
                {name.content}
              </div>
              <span className="text-sm">{desc.content}</span>
              <div className="flex flex-col sm:flex-row sm:items-center gap-2 pt-2">
                {record.className && (
                  <span className="inline-flex items-center gap-2 flex-wrap">
                    {t.translations.CLASS}:{" "}
                    <div className="badge badge-sm badge-secondary">
                      {className.content}
                    </div>
                  </span>
                )}
                <div className="sm:ml-4">
                  <span className="font-bold">
                    {t.translations.LAST_EDIT}:{" "}
                  </span>
                  {date.content}
                </div>
              </div>
              <div className="pt-2">
                <span>{t.translations.TAGS}: </span>
                {renderTags(record.tags)}
              </div>
            </li>
          );
        })}
      </ul>
      {/* Pagination Controls */}
      {totalPages > 1 && (
        <div className="flex justify-center sm:justify-end gap-2 mt-4 p-2 sm:p-4">
          <button
            className="btn btn-sm btn-ghost"
            disabled={currentPage === 1}
            onClick={() => setCurrentPage((prev) => prev - 1)}
          >
            <ChevronLeftIcon className="size-6" />
          </button>
          <span className="px-2 text-sm">
            {t.translations.PAGE} {currentPage} {t.translations.OF} {totalPages}
          </span>
          <button
            className="btn btn-sm btn-ghost"
            disabled={currentPage === totalPages}
            onClick={() => setCurrentPage((prev) => prev + 1)}
          >
            <ChevronRightIcon className="size-6" />
          </button>
        </div>
      )}
    </div>
  );
};

export default ListView;
