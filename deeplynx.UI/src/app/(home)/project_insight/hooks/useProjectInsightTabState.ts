"use client";

import type { Dispatch, SetStateAction } from "react";
import type {
  ProjectInsightTabKey,
  TabFilterState,
} from "../components/projectInsight.view-utils";

type SetTabFilterState = Dispatch<SetStateAction<TabFilterState>>;

interface UseProjectInsightTabStateParams {
  activeTabKey: ProjectInsightTabKey;
  setLibraryState: SetTabFilterState;
  setPendingState: SetTabFilterState;
}

export function useProjectInsightTabState({
  activeTabKey,
  setLibraryState,
  setPendingState,
}: UseProjectInsightTabStateParams) {
  function setTabState(
    tabKey: ProjectInsightTabKey,
    updater: (current: TabFilterState) => TabFilterState,
  ) {
    setLibraryState(updater);
    setPendingState(updater);
  }

  function setActiveTabState(
    updater: (current: TabFilterState) => TabFilterState,
  ) {
    setTabState(activeTabKey, updater);
  }

  function updateActiveSearchQuery(searchQuery: string) {
    setActiveTabState((current) => ({
      ...current,
      searchQuery,
    }));
  }

  function clearActiveSearchQuery() {
    updateActiveSearchQuery("");
  }

  function removeActiveFilterPill(
    pill: { type: "class" | "tag"; optionId: number },
  ) {
    setActiveTabState((current) => ({
      ...current,
      classIds:
        pill.type === "class"
          ? current.classIds.filter((id) => id !== pill.optionId)
          : current.classIds,
      tagIds:
        pill.type === "tag"
          ? current.tagIds.filter((id) => id !== pill.optionId)
          : current.tagIds,
    }));
  }

  return {
    clearActiveSearchQuery,
    removeActiveFilterPill,
    setActiveTabState,
    setTabState,
    updateActiveSearchQuery,
  };
}
