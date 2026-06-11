"use client";

import SearchInput from "@/app/(home)/components/SearchInput";
import React from "react";

type Props = {
  searchTerm: string;
  setSearchTerm: React.Dispatch<React.SetStateAction<string>>;
  placeholder: string;
  searchLoading?: boolean;
  onSearch: () => void;
  action?: React.ReactNode;
};

export default function CollectionRecordSearchControls({
  searchTerm,
  setSearchTerm,
  placeholder,
  searchLoading = false,
  onSearch,
  action,
}: Props) {
  return (
    <div className="flex flex-col gap-3 lg:flex-row">
      <div
        className="min-w-0 flex-1"
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            event.preventDefault();
            onSearch();
          }
        }}
      >
        <SearchInput
          className="w-full"
          placeholder={placeholder}
          value={searchTerm}
          onChange={(event) => setSearchTerm(event.target.value)}
        />
      </div>
      <button
        type="button"
        className="btn btn-outline"
        disabled={searchLoading}
        onClick={onSearch}
      >
        Search
      </button>
      {action}
    </div>
  );
}
