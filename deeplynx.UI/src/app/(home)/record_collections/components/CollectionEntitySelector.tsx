"use client";

import SearchInput from "@/app/(home)/components/SearchInput";
import { useLanguage } from "@/app/contexts/Language";
import { XCircleIcon } from "@heroicons/react/24/outline";
import React from "react";
import { interpolateTemplate } from "@/app/lib/record_helpers";

type NamedItem = {
  id: number | string;
  name: string;
};

type Props = {
  title: string;
  selectedItems: NamedItem[];
  searchTerm: string;
  setSearchTerm: React.Dispatch<React.SetStateAction<string>>;
  searchPlaceholder: string;
  options: NamedItem[];
  loading: boolean;
  loadingText: string;
  emptyOptionsText: string;
  addDisabled: boolean;
  addButtonLoading?: boolean;
  selectedItemClassName: (item: NamedItem) => string;
  addTypedItem: () => void | Promise<void>;
  selectOption: (item: NamedItem) => void;
  removeItem: (item: NamedItem) => void;
};

export default function CollectionEntitySelector({
  title,
  selectedItems,
  searchTerm,
  setSearchTerm,
  searchPlaceholder,
  options,
  loading,
  loadingText,
  emptyOptionsText,
  addDisabled,
  addButtonLoading = false,
  selectedItemClassName,
  addTypedItem,
  selectOption,
  removeItem,
}: Props) {
  const { t } = useLanguage();

  return (
    <div>
      <p className="text-sm font-medium text-base-content">{title}</p>
      <div className="mt-2 flex flex-wrap gap-2">
        {selectedItems.map((item) => (
          <span key={item.id} className={selectedItemClassName(item)}>
            {item.name}
            <button
              type="button"
              className="group ml-1 rounded-full px-1 leading-none text-base-content/70 transition-colors hover:bg-base-100/70 hover:text-error focus-visible:bg-base-100/70 focus-visible:text-error"
              onClick={() => removeItem(item)}
              title={interpolateTemplate(
                t.translations.RECORD_COLLECTIONS_REMOVE_ITEM,
                { name: item.name },
              )}
            >
              <XCircleIcon
                className="size-4 transition-colors group-hover:text-error group-focus-visible:text-error"
                aria-hidden="true"
              />
            </button>
          </span>
        ))}
      </div>
      <div className="mt-3 flex flex-col gap-2 sm:flex-row">
        <div
          className="min-w-0 flex-1"
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              void addTypedItem();
            }
          }}
        >
          <SearchInput
            className="w-full"
            placeholder={searchPlaceholder}
            value={searchTerm}
            size="sm"
            onChange={(event) => setSearchTerm(event.target.value)}
          />
        </div>
        <button
          type="button"
          className="btn btn-primary btn-sm"
          disabled={addDisabled}
          onClick={() => void addTypedItem()}
        >
          {addButtonLoading ? (
            <span className="loading loading-spinner loading-xs" />
          ) : (
            t.translations.ADD
          )}
        </button>
      </div>
      <div className="mt-3 max-h-48 space-y-2 overflow-auto rounded-xl border border-base-300/50 bg-base-100 p-3">
        {loading ? (
          <div className="flex items-center gap-2 text-sm text-base-content/70">
            <span className="loading loading-spinner loading-sm" />
            {loadingText}
          </div>
        ) : options.length ? (
          options.map((item) => (
            <button
              type="button"
              key={item.id}
              className="flex w-full items-center justify-between rounded-lg px-2 py-1 text-left text-sm hover:bg-base-200"
              onClick={() => selectOption(item)}
            >
              <span className="truncate">{item.name}</span>
              <span className="btn btn-primary btn-xs">
                {t.translations.ADD}
              </span>
            </button>
          ))
        ) : (
          <p className="text-sm text-base-content/60">{emptyOptionsText}</p>
        )}
      </div>
    </div>
  );
}
