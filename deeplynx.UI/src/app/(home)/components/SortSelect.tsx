"use client";

import { useLanguage } from "@/app/contexts/Language";
import type { SortOptionConfig } from "../hooks/useSortedItems";

type SortSelectProps<TValue extends string = string> = {
  value: TValue | "";
  options: SortOptionConfig<TValue>[];
  onChange: (value: TValue) => void;
  containerClassName?: string;
};

export default function SortSelect<TValue extends string = string>({
  value,
  options,
  onChange,
  containerClassName = "flex items-center gap-1",
}: SortSelectProps<TValue>) {
  const { t } = useLanguage();

  if (!options.length) return null;

  return (
    <div className={containerClassName}>
      <div className="px-3 py-2 text-md font-semibold text-base-content/50">
        {t.translations.SORT_BY}
      </div>
      <div className="relative inline-block">
        <select
          value={value}
          onChange={(e) => onChange(e.target.value as TValue)}
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
