"use client";

import { useLanguage } from "@/app/contexts/Language";
import { XCircleIcon } from "@heroicons/react/24/outline";
import React from "react";
import { FacetOption } from "./recordCollections.types";
import { interpolateTemplate } from "@/app/lib/record_helpers";

type Props = {
  labelFacets: FacetOption[];
  tagFacets: FacetOption[];
  getSensitivityClass: (label: string) => string;
  onRemoveLabel?: (labelName: string) => void;
  onRemoveTag?: (tagName: string) => void;
};

function RemoveFacetButton({
  title,
  onClick,
}: {
  title: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      className="group ml-1 rounded-full px-1 leading-none text-base-content/70 transition-colors hover:bg-base-100/70 hover:text-error focus-visible:bg-base-100/70 focus-visible:text-error"
      onClick={onClick}
      title={title}
    >
      <XCircleIcon
        className="size-4 transition-colors group-hover:text-error group-focus-visible:text-error"
        aria-hidden="true"
      />
    </button>
  );
}

export default function CollectionFacetSummary({
  labelFacets,
  tagFacets,
  getSensitivityClass,
  onRemoveLabel,
  onRemoveTag,
}: Props) {
  const { t } = useLanguage();

  return (
    <div className="space-y-5">
      <div>
        <p className="text-sm font-medium text-base-content">
          {t.translations.SENSITIVITY_LABELS}
        </p>
        <div className="mt-2 flex flex-wrap gap-2">
          {labelFacets.map((label) => (
            <span
              key={label.label}
              className={`badge badge-sm gap-1 ${getSensitivityClass(label.label)}`}
            >
              {label.label} ({label.count})
              {onRemoveLabel ? (
                <RemoveFacetButton
                  title={interpolateTemplate(
                    t.translations.RECORD_COLLECTIONS_DESELECT_RECORDS_WITH,
                    { name: label.label },
                  )}
                  onClick={() => onRemoveLabel(label.label)}
                />
              ) : null}
            </span>
          ))}
        </div>
      </div>

      <div>
        <p className="text-sm font-medium text-base-content">
          {t.translations.TAGS}
        </p>
        <div className="mt-2 flex flex-wrap gap-2">
          {tagFacets.map((tag) => (
            <span
              key={tag.label}
              className="badge badge-sm badge-outline badge-secondary gap-1"
            >
              {tag.label} ({tag.count})
              {onRemoveTag ? (
                <RemoveFacetButton
                  title={interpolateTemplate(
                    t.translations.RECORD_COLLECTIONS_DESELECT_RECORDS_WITH,
                    { name: tag.label },
                  )}
                  onClick={() => onRemoveTag(tag.label)}
                />
              ) : null}
            </span>
          ))}
        </div>
      </div>
    </div>
  );
}
