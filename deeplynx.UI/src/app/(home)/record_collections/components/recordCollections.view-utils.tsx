"use client";

import { ArrowRightIcon } from "@heroicons/react/24/outline";
import React from "react";
import { CollectionSortOption } from "./recordCollections.types";

export function renderCollectionSortLabel(option: CollectionSortOption) {
  switch (option) {
    case "updatedDesc":
      return "Last Updated (Newest)";
    case "updatedAsc":
      return "Last Updated (Oldest)";
    case "alphabeticalAsc":
      return (
        <>
          Alphabetical (A
          <ArrowRightIcon className="size-3" />
          Z)
        </>
      );
    case "alphabeticalDesc":
      return (
        <>
          Alphabetical (Z
          <ArrowRightIcon className="size-3" />
          A)
        </>
      );
    case "recordCountDesc":
      return "# of Records (Highest)";
    case "recordCountAsc":
      return "# of Records (Lowest)";
    default:
      return option;
  }
}
