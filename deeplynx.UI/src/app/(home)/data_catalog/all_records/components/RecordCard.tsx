"use client";

import Link from "next/link";
import {
  ArrowTopRightOnSquareIcon,
  ArchiveBoxIcon,
  ClockIcon,
  FolderIcon,
  TagIcon,
} from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";
import { RecordTableRow } from "@/app/(home)/types/types";
import { formatLocalDateTime } from "@/app/lib/date_time";
import { getHighlightedContent, parseRecordTags } from "./utils";

type Props = {
  record: RecordTableRow;
  /**
   * Active search terms passed down as lowercase strings so getHighlightedContent
   * can do a case-insensitive match without re-lowercasing on every render.
   */
  activeSearchTerms: string[];
  isBulkMode: boolean;
  isSelected: boolean;
  onToggleSelected?: (record: RecordTableRow) => void;
};

/**
 * Renders a single record row in the All Records catalog list.
 *
 * Every visible text field is run through getHighlightedContent so that any
 * portion matching an active search term is wrapped in a <mark> element.
 * This gives users immediate visual feedback on why a record was returned.
 *
 * Tags are capped at 3 visible labels with an overflow count (+N) to prevent
 * heavily tagged records from making the row disproportionately tall in the
 * list view. The full tag set is visible on the record detail page.
 *
 * The record name and the arrow button are both links to the same detail URL.
 * The name link takes up most of the row width (better click target for
 * mouse users) while the icon button provides a clear affordance and a
 * descriptive aria-label for keyboard/screen reader users.
 *
 * "use client" is required because this component calls useLanguage, which
 * reads from a React context that is only available in the browser.
 */
export default function RecordCard({ record, activeSearchTerms, isBulkMode=false, isSelected=false, onToggleSelected }: Props) {
  const { t } = useLanguage();

  // Highlight matching search terms in every visible text field.
  const name = getHighlightedContent(
    record.name || `Record ${record.id}`,
    activeSearchTerms,
  );
  const description = getHighlightedContent(
    record.description || t.translations.NO_RECORDS_FOUND,
    activeSearchTerms,
  );
  const className = getHighlightedContent(
    record.className || t.translations.NO_CLASS,
    activeSearchTerms,
  );
  const updatedAt = getHighlightedContent(
    record.lastUpdatedAt ? formatLocalDateTime(record.lastUpdatedAt) : "",
    activeSearchTerms,
  );

  const tags = parseRecordTags(record.tags);
  const recordHref = `/record?recordId=${record.id}&projectId=${record.projectId}`;

  return (
    <article className={`group grid grid-cols-1 gap-3 p-4 transition hover:bg-base-200/60 ${
        isBulkMode
            ? "md:grid-cols-[auto_minmax(0,1fr)_auto]"
            : "md:grid-cols-[minmax(0,1fr)_auto]"}
            `}
    >
      {isBulkMode && (
          <div className="flex items-start pt-1">
            <input
              type="checkbox"
              className="checkbox checkbox-primary checkbox-sm"
              checked={isSelected}
              onChange={() => onToggleSelected?.(record)}
              onClick={(event) => event.stopPropagation()}
              aria-label={`Select ${record.name || `record ${record.id}`}`}
            />
          </div>
      )}
      <div className="min-w-0">
        {/* Badge row: record ID, class name, and optional archived warning */}
        <div className="mb-2 flex flex-wrap items-center gap-2">
          <span className="badge badge-primary badge-soft">{record.id}</span>
          <span className="badge badge-secondary badge-outline">
            {className.content}
          </span>
          {record.isArchived && (
            <span className="badge badge-warning badge-outline">
              <ArchiveBoxIcon className="size-3" />
              {t.translations.ARCHIVED_BADGE}
            </span>
          )}
        </div>

        {/* Record name — primary navigation target for the row */}
        <Link
          href={recordHref}
          className="block truncate text-base font-semibold text-base-content group-hover:text-primary"
        >
          {name.content}
        </Link>

        {/* Description is clamped to one line to keep the list scannable */}
        <p className="mt-1 line-clamp-1 text-sm text-base-content/65">
          {description.content}
        </p>

        {/* Metadata row: project, last-updated timestamp, and tag preview */}
        <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-base-content/55">
          <span className="inline-flex min-w-0 items-center gap-1">
            <FolderIcon className="size-3.5 shrink-0" />
            <span className="truncate">
              {record.projectName || t.translations.NO_PROJECT}
            </span>
          </span>
          <span className="inline-flex items-center gap-1">
            <ClockIcon className="size-3.5 shrink-0" />
            {updatedAt.content}
          </span>
          {/* Show up to 3 tags; surface the overflow count so users know
              more tags exist without inflating the row height. */}
          {tags.length > 0 && (
            <span className="inline-flex items-center gap-1">
              <TagIcon className="size-3.5 shrink-0" />
              {tags.slice(0, 3).join(", ")}
              {tags.length > 3 ? ` +${tags.length - 3}` : ""}
            </span>
          )}
        </div>
      </div>

      {/* Icon-only link button on the right — visible on hover via group class */}
      <div className="flex items-center justify-end">
        <Link
          href={recordHref}
          className="btn btn-sm btn-ghost group-hover:btn-primary"
          aria-label={`Open ${record.name || "record"}`}
        >
          <ArrowTopRightOnSquareIcon className="size-4" />
        </Link>
      </div>
    </article>
  );
}
