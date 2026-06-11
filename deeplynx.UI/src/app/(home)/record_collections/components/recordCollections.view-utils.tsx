"use client";

import { CollectionSortOption } from "./recordCollections.types";

export function renderCollectionSortLabel(
  option: CollectionSortOption,
  t: { translations: Record<string, string> },
) {
  switch (option) {
    case "updatedDesc":
      return t.translations.RECORD_COLLECTIONS_SORT_UPDATED_DESC;
    case "updatedAsc":
      return t.translations.RECORD_COLLECTIONS_SORT_UPDATED_ASC;
    case "alphabeticalAsc":
      return t.translations.RECORD_COLLECTIONS_SORT_ALPHABETICAL_ASC;
    case "alphabeticalDesc":
      return t.translations.RECORD_COLLECTIONS_SORT_ALPHABETICAL_DESC;
    case "recordCountDesc":
      return t.translations.RECORD_COLLECTIONS_SORT_RECORD_COUNT_DESC;
    case "recordCountAsc":
      return t.translations.RECORD_COLLECTIONS_SORT_RECORD_COUNT_ASC;
    default:
      return option;
  }
}
