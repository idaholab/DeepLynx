// src/app/(home)/components/SearchBar.tsx
"use client";

import { translations } from "@/app/lib/translations";
import { MagnifyingGlassIcon, XMarkIcon } from "@heroicons/react/24/outline";
import React, { useRef, useState } from "react";

interface Filter {
  id: number;
  term: string;
}

interface SearchBarProps {
  placeholder?: string;
  className?: string;
  onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onEnter?: (value: string) => void;
  onSubmit?: (payload: { query: string; option?: string }) => void;
  value?: string;
  options?: { name: string; value: string }[];
  selectedOption?: string;
  onOptionChange?: (option: string) => void;
  activeFilters?: Filter[];
  onRemoveFilter?: (id: number) => void;
  onClearAll?: () => void;
  resultCount?: number;
  showResultsMessage?: boolean;
  aditionalFilters?: boolean;
}

const SearchBar: React.FC<SearchBarProps> = ({
  placeholder,
  className = "",
  onChange,
  onEnter,
  onSubmit,
  value,
  options = [
    { name: "Anywhere", value: "Anywhere" },
    { name: "Time Range", value: "Time Range" },
    { name: "Class", value: "ClassName" },
    { name: "Tag", value: "Tags" },
    { name: "Original Data ID", value: "OriginalId" },
    { name: "Data Source", value: "DataSourceName" },
    { name: "Properties", value: "Properties" },
  ],
  selectedOption,
  onOptionChange,
  activeFilters = [],
  onRemoveFilter,
  onClearAll,
  resultCount,
  showResultsMessage,
  aditionalFilters = true,
}) => {
  const locale = "en";
  const t = translations[locale];
  const inputRef = useRef<HTMLInputElement>(null);

  // Handle controlled/uncontrolled input
  const [internalValue, setInternalValue] = useState<string>("");
  const isControlled = value !== undefined && onChange !== undefined;
  const currentValue = isControlled ? value : internalValue;

  // Handle controlled/uncontrolled dropdown
  const [internalOption, setInternalOption] = useState<string | undefined>(
    options[0].value,
  );
  const optionControlled =
    selectedOption !== undefined && onOptionChange !== undefined;
  const currentOption = optionControlled ? selectedOption : internalOption;

  const handleSubmit = () => {
    const trimmedValue = currentValue.trim();
    if (onSubmit) {
      onSubmit({ query: trimmedValue, option: currentOption });
    } else if (onEnter) {
      onEnter(trimmedValue);
    }
  };

  const handleClearInput = () => {
    if (isControlled && onChange) {
      onChange({
        target: { value: "" },
      } as React.ChangeEvent<HTMLInputElement>);
    } else {
      setInternalValue("");
    }
    onClearAll?.();
  };


  return (
    <div className={className}>
      <div className="flex gap-2 w-full">
        {/* Search Input */}
        <div className="relative flex-1 min-w-0">
          <MagnifyingGlassIcon className="absolute left-4 top-5 transform -translate-y-1/2 w-5 h-5 text-base-content" />

          <input
            ref={inputRef}
            type="text"
            placeholder={placeholder}
            className="w-full pl-12 pr-4 py-2 rounded-full border border-base-content/25 bg-base-100 shadow-sm shadow-base-content/10 focus:outline-none focus:ring-2 focus:ring-primary text-info-content"
            onChange={
              isControlled ? onChange : (e) => setInternalValue(e.target.value)
            }
            onKeyDown={(e) => e.key === "Enter" && handleSubmit()}
            value={currentValue}
          />

          {currentValue && (
            <button
              type="button"
              onClick={handleClearInput}
              className="absolute right-4 top-5 transform -translate-y-1/2 text-base-content opacity-70 hover:opacity-100"
              aria-label={t.translations?.CLEAR_SEARCH ?? "Clear search"}
            >
              <XMarkIcon className="size-6" />
            </button>
          )}

          <div className="text-right mt-1">
            <a
              href="/data_catalog/query_builder"
              className="text-sm underline text-primary hover:underline"
            >
              {aditionalFilters && t.translations.ADDITIONAL_FILTERS}
            </a>
          </div>
        </div>
      </div>
    </div>
  );
};

export default SearchBar;
