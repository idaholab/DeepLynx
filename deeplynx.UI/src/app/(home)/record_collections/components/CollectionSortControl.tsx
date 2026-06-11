"use client";

import { useLanguage } from "@/app/contexts/Language";
import {
  CheckIcon,
  ChevronDownIcon,
} from "@heroicons/react/24/outline";
import React from "react";
import { CollectionSortOption } from "./recordCollections.types";

type Props = {
  collectionSort: CollectionSortOption;
  collectionSortMenuOpen: boolean;
  collectionSortMenuRef: React.RefObject<HTMLDivElement | null>;
  options: CollectionSortOption[];
  onToggleMenu: () => void;
  onSelectOption: (option: CollectionSortOption) => void;
  renderLabel: (option: CollectionSortOption) => React.ReactNode;
};

export default function CollectionSortControl({
  collectionSort,
  collectionSortMenuOpen,
  collectionSortMenuRef,
  options,
  onToggleMenu,
  onSelectOption,
  renderLabel,
}: Props) {
  const { t } = useLanguage();

  return (
    <label className="form-control w-full lg:w-64">
      <span className="label py-0 pb-1">
        <span className="label-text text-xs font-semibold uppercase text-base-content/60">
          {t.translations.SORT_BY}
        </span>
      </span>
      <div className="relative" ref={collectionSortMenuRef}>
        <button
          type="button"
          className="btn btn-outline w-full justify-between font-normal"
          aria-haspopup="listbox"
          aria-expanded={collectionSortMenuOpen}
          onClick={onToggleMenu}
        >
          <span className="inline-flex items-center gap-1.5">
            {renderLabel(collectionSort)}
          </span>
          <ChevronDownIcon
            className={`size-4 transition-transform ${
              collectionSortMenuOpen ? "rotate-180" : ""
            }`}
          />
        </button>

        {collectionSortMenuOpen ? (
          <div className="absolute right-0 z-20 mt-2 w-full rounded-xl border border-base-300 bg-base-100 p-1 shadow-lg">
            <ul role="listbox" className="space-y-1">
              {options.map((option) => {
                const isSelected = collectionSort === option;
                return (
                  <li key={option}>
                    <button
                      type="button"
                      role="option"
                      aria-selected={isSelected}
                      className={`flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-sm transition ${
                        isSelected
                          ? "bg-info/20 text-base-content"
                          : "hover:bg-base-200 text-base-content/80"
                      }`}
                      onClick={() => onSelectOption(option)}
                    >
                      <span className="inline-flex items-center gap-1.5">
                        {renderLabel(option)}
                      </span>
                      {isSelected ? (
                        <CheckIcon className="size-4 flex-shrink-0" />
                      ) : null}
                    </button>
                  </li>
                );
              })}
            </ul>
          </div>
        ) : null}
      </div>
    </label>
  );
}
