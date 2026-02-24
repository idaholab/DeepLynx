"use client";

import { translations } from "@/app/lib/translations";
import { MagnifyingGlassIcon, XMarkIcon } from "@heroicons/react/24/outline";
import React, { useRef, useState } from "react";

interface Filter {
  id: number;
  term: string;
}

interface AdvancedSearchBarProps {
  placeholder?: string;
  className?: string;
  onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void; // existing
  onEnter?: (value: string) => void;                            // existing (fallback)
  /** New: full submit payload so backend can vary by option */
  onSubmit?: (payload: { query: string; option?: string }) => void;

  value?: string;

  /** Dropdown options = array of strings (e.g. ["Title", "Author", "Tag"]) */
  options?: { name: string; value: string }[];

  /** Optional controlled dropdown value */
  selectedOption?: string;
  /** Optional controlled dropdown setter */
  onOptionChange?: (option: string) => void;

  // existing extras, unchanged
  activeFilters?: Filter[];
  onRemoveFilter?: (id: number) => void;
  onClearAll?: () => void;
  resultCount?: number;
  showResultsMessage?: boolean;
}

const AdvancedSearchBar: React.FC<AdvancedSearchBarProps> = ({
  placeholder,
  className = "",
  onChange,
  onEnter,
  onSubmit,
  value,
  options = [{ name: "Anywhere", value: "Anywhere" }, { name: "Time Range", value: "Time Range" }, { name: "Class", value: "ClassName" }, { name: "Tag", value: "Tags" }, { name: "Original Data ID", value: "OriginalId" }, { name: "Data Source", value: "DataSourceName" }, { name: "Properties", value: "Properties" }],
  selectedOption,
  onOptionChange,
  activeFilters = [],
  onRemoveFilter,
  onClearAll,
  resultCount,
  showResultsMessage,
}) => {
  const locale = "en";
  const t = translations[locale];

  // Input controlled/uncontrolled
  const [internalValue, setInternalValue] = useState<string>("");
  const isControlled = value !== undefined && onChange !== undefined;
  const currentValue = isControlled ? (value as string) : internalValue;

  const setValue = (next: string) => {
    if (isControlled && onChange) {
      const event = { target: { value: next } } as React.ChangeEvent<HTMLInputElement>;
      onChange(event);
    } else {
      setInternalValue(next);
    }
  };

  // Dropdown controlled/uncontrolled
  const [internalOption, setInternalOption] = useState<string | undefined>(options[0].value);
  const optionControlled = selectedOption !== undefined && onOptionChange !== undefined;
  const currentOption = optionControlled ? selectedOption : internalOption;

  const inputRef = useRef<HTMLInputElement>(null);

  const handleClear = () => {
    setValue("");
    inputRef.current?.focus();
  };

  const handleSubmit = () => {
    if (onSubmit) onSubmit({ query: currentValue.trim(), option: currentOption });
    else if (onEnter) onEnter(currentValue.trim());
  };

  return (
    <div className={`${className}`}>
      <div className="flex gap-2 w-full">
        {/* Text input */}
        <div className="relative flex-1 min-w-0">
          {/* Magnifying glass inside the input */}
          <MagnifyingGlassIcon
            className="absolute left-4 top-5 transform -translate-y-1/2 w-5 h-5 text-neutral size-6"
          />

          <input
            ref={inputRef}
            type="text"
            placeholder={placeholder}
            className="w-full pl-12 pr-4 py-2 rounded-full border border-base-300 bg-base-100 shadow-sm focus:outline-none focus:ring-2 focus:ring-primary text-info-content" // add left padding so text doesn't overlap icon
            onChange={isControlled ? onChange : (e) => setInternalValue(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") handleSubmit();
            }}
            value={currentValue}
          />

          {currentValue && (
            <button
              type="button"
              onClick={handleClear}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-base-content opacity-70 hover:opacity-100"
              aria-label={t.translations?.CLEAR_SEARCH ?? "Clear search"}
            >
              <XMarkIcon className="size-6" />
            </button>
          )}
        </div>
      </div>

      {/* Active filters UI below stays intact (if you still want it) */}
      {activeFilters?.length ? (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          {activeFilters.map((f) => (
            <div key={f.id} className="badge badge-neutral gap-1">
              <span>{f.term}</span>
              {onRemoveFilter && (
                <button
                  type="button"
                  className="ml-1 hover:opacity-80"
                  onClick={() => onRemoveFilter(f.id)}
                  aria-label={t.translations?.REMOVE ?? "Remove"}
                >
                  ×
                </button>
              )}
            </div>
          ))}
          {onClearAll && (
            <button type="button" className="btn btn-ghost btn-xs" onClick={onClearAll}>
              {t.translations?.CLEAR_ALL ?? "Clear all"}
            </button>
          )}
        </div>
      ) : null}

      {showResultsMessage && (
        <div className="mt-4 ml-1">
          {activeFilters?.length && resultCount === 0 ? (
            <p>{t.translations.NO_RESULTS_FOUND}</p>
          ) : (
            <div className="border-b border-base-200">
              <h2>
                {t.translations.FOUND} {resultCount} {t.translations.MATCHES}
              </h2>
            </div>
          )}
        </div>
      )}
    </div>
  );
};

export default AdvancedSearchBar;
