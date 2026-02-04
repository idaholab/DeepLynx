// src/app/(home)/record/components/AddEdgeModal.tsx

import React, { useState, useEffect } from "react";
import {
  ArrowLeftIcon,
  ArrowRightIcon,
  MagnifyingGlassIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import SearchBar from "../../components/SearchBar";
import { useLanguage } from "@/app/contexts/Language";

// ============================================================================
// TYPE DEFINITIONS
// ============================================================================

export interface RecordSearchResult {
  id: number;
  name: string;
  description?: string;
  className?: string;
  dataSourceName?: string;
  originalId?: string;
  uri?: string;
}

interface AddEdgeModalProps {
  isOpen: boolean;
  onClose: () => void;
  currentRecord: {
    id: number;
    name?: string | null;
    description?: string | null;
    dataSourceName?: string;
  };
  relationship: string;
  direction: "outgoing" | "incoming";
  projectId: number;
  organizationId: number;
  onSearchRecords: (
    query: string,
    option?: string,
  ) => Promise<RecordSearchResult[]>;
  onCreateRelationships: (data: {
    records: RecordSearchResult[];
    relationship: string;
    direction: "outgoing" | "incoming";
  }) => Promise<void>;
}

type RecordCardRecord = {
  id: number;
  name?: string | null;
  description?: string | null;
  dataSourceName?: string;
};

// ============================================================================
// SUB-COMPONENTS
// ============================================================================

/**
 * RecordCard - Displays a record's basic information in a card format
 * Can be shown as a placeholder when no record is selected
 */
const RecordCard = ({
  record,
  isPlaceholder = false,
  t,
}: {
  record: RecordCardRecord | null;
  isPlaceholder?: boolean;
  t: { translations: Record<string, string> };
}) => {
  if (isPlaceholder) {
    return (
      <div className="border-2 border-dashed border-gray-300 rounded-lg p-4 bg-gray-50">
        <div className="text-center py-8 text-gray-400">
          <MagnifyingGlassIcon className="h-12 w-12 mx-auto mb-2 opacity-30" />
          <p className="text-sm">{t.translations.SEARCH_AND_SELECT_RECORDS}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="border-2 border-blue-500 rounded-lg p-4 bg-blue-50">
      <div className="space-y-2">
        <div>
          <div className="text-xs text-gray-500">
            {t.translations.RECORD_ID}
          </div>
          <div className="font-mono text-sm">{record?.id}</div>
        </div>
        <div>
          <div className="text-xs text-gray-500">{t.translations.NAME}</div>
          <div className="font-semibold">
            {record?.name ?? t.translations.UNKOWN}
          </div>
        </div>
        {record?.description && (
          <div>
            <div className="text-xs text-gray-500">
              {t.translations.DESCRIPTION}
            </div>
            <div className="text-sm">{record.description}</div>
          </div>
        )}
        {record?.dataSourceName && (
          <div>
            <div className="text-xs text-gray-500">
              {t.translations.DATA_SOURCE}
            </div>
            <div className="inline-block px-2 py-1 text-xs border border-gray-300 rounded">
              {record.dataSourceName}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

// ============================================================================
// MAIN COMPONENT
// ============================================================================

export default function AddEdgeModal({
  isOpen,
  onClose,
  currentRecord,
  relationship,
  direction,
  onSearchRecords,
  onCreateRelationships,
}: AddEdgeModalProps) {
  const { t } = useLanguage();

  // ============================================================================
  // STATE MANAGEMENT
  // ============================================================================

  // Search state
  const [searchTerm, setSearchTerm] = useState("");
  const [searchResults, setSearchResults] = useState<RecordSearchResult[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);

  // Selection state
  const [selectedRecords, setSelectedRecords] = useState<RecordSearchResult[]>(
    [],
  );
  const [selectedDirection, setSelectedDirection] = useState(direction);
  const [selectedRelationship] = useState(relationship);

  // UI state
  const [isCreating, setIsCreating] = useState(false);

  // ============================================================================
  // EFFECTS
  // ============================================================================

  /**
   * Reset modal state when it closes or opens
   */
  useEffect(() => {
    if (!isOpen) {
      setSearchTerm("");
      setSearchResults([]);
      setSelectedRecords([]);
      setHasSearched(false);
    } else {
      setSelectedDirection(direction);
    }
  }, [isOpen, direction]);

  // ============================================================================
  // HANDLERS
  // ============================================================================

  /**
   * Handles search submission from SearchBar
   */
  const handleSearch = async (payload: { query: string; option?: string }) => {
    const { query, option } = payload;

    if (!query.trim()) {
      setSearchResults([]);
      setHasSearched(false);
      return;
    }

    setIsSearching(true);
    setHasSearched(true);

    try {
      const results = await onSearchRecords(query, option);
      const filteredResults = results.filter((r) => r.id !== currentRecord.id);
      setSearchResults(filteredResults);
    } catch (error) {
      console.error("Error searching records:", error);
      setSearchResults([]);
    } finally {
      setIsSearching(false);
    }
  };

  /**
   * Toggles a record's selection state
   */
  const handleToggleRecord = (record: RecordSearchResult) => {
    setSelectedRecords((prev) => {
      const isSelected = prev.some((r) => r.id === record.id);
      if (isSelected) {
        return prev.filter((r) => r.id !== record.id);
      }
      return [...prev, record];
    });
  };

  /**
   * Checks if a record is currently selected
   */
  const isRecordSelected = (recordId: number) => {
    return selectedRecords.some((r) => r.id === recordId);
  };

  /**
   * Creates relationships for all selected records
   */
  const handleCreate = async () => {
    setIsCreating(true);
    try {
      await onCreateRelationships({
        records: selectedRecords,
        relationship: selectedRelationship,
        direction: selectedDirection,
      });
      onClose();
    } catch (error) {
      console.error("Error creating relationships:", error);
    } finally {
      setIsCreating(false);
    }
  };

  // ============================================================================
  // RENDER
  // ============================================================================

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg shadow-xl max-w-6xl w-full max-h-[90vh] overflow-hidden flex flex-col">
        {/* ===== HEADER ===== */}
        <div className="p-6 border-b border-gray-200">
          <div className="flex justify-between items-center">
            <h3 className="text-2xl font-bold">
              {t.translations.CREATE_RELATIONSHIP}
            </h3>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-full transition-colors"
            >
              <XMarkIcon className="h-5 w-5" />
            </button>
          </div>
        </div>

        {/* ===== CONTENT ===== */}
        <div className="flex-1 overflow-y-auto p-6">
          <div className="grid grid-cols-[1fr_auto_1fr] gap-6 items-start">
            {/* ----- LEFT COLUMN: Current Record + Direction Control ----- */}
            <div className="space-y-4">
              <p className="text-sm font-medium text-gray-600">
                {selectedDirection === "outgoing"
                  ? t.translations.FROM_
                  : t.translations.TO}
              </p>

              <RecordCard record={currentRecord} t={t} />

              {/* Relationship Metadata Panel */}
              <div className="border border-gray-300 rounded-lg p-4 bg-gray-50">
                <h4 className="font-semibold text-sm mb-3">
                  {t.translations.RELATIONSHIP_DETAILS}
                </h4>

                {/* Direction Selector */}
                <div className="mb-3">
                  <label className="block text-xs text-gray-600 mb-1">
                    {t.translations.DIRECTION}
                  </label>
                  <div className="flex gap-2">
                    <button
                      onClick={() => setSelectedDirection("outgoing")}
                      className={`flex-1 px-3 py-2 rounded-lg text-sm font-medium transition-colors ${
                        selectedDirection === "outgoing"
                          ? "bg-blue-600 text-white"
                          : "bg-white border border-gray-300 text-gray-700 hover:bg-gray-50"
                      }`}
                    >
                      <ArrowRightIcon className="h-4 w-4 inline mr-1" />
                      {t.translations.OUTGOING}
                    </button>
                    <button
                      onClick={() => setSelectedDirection("incoming")}
                      className={`flex-1 px-3 py-2 rounded-lg text-sm font-medium transition-colors ${
                        selectedDirection === "incoming"
                          ? "bg-blue-600 text-white"
                          : "bg-white border border-gray-300 text-gray-700 hover:bg-gray-50"
                      }`}
                    >
                      <ArrowLeftIcon className="h-4 w-4 inline mr-1" />
                      {t.translations.INCOMING}
                    </button>
                  </div>
                </div>
              </div>
            </div>

            {/* ----- MIDDLE COLUMN: Relationship Arrow ----- */}
            <div className="flex flex-col items-center justify-center pt-12">
              {selectedDirection === "outgoing" ? (
                <ArrowRightIcon className="h-10 w-10 text-blue-600" />
              ) : (
                <ArrowLeftIcon className="h-10 w-10 text-blue-600" />
              )}
              {/* TODO: Add relationship type selector when backend supports filtering by direction */}
            </div>

            {/* ----- RIGHT COLUMN: Record Search & Selection ----- */}
            <div>
              <div className="text-sm font-medium text-gray-600 mb-3">
                {selectedDirection === "outgoing"
                  ? t.translations.TO
                  : t.translations.FROM}{" "}
                {selectedRecords.length > 0 && (
                  <span className="text-blue-600">
                    ({selectedRecords.length} {t.translations.SELECTED})
                  </span>
                )}
              </div>

              <div className="space-y-3">
                {/* Selected Records Display */}
                {selectedRecords.length > 0 && (
                  <div className="border-2 border-blue-500 rounded-lg p-3 bg-blue-50 max-h-64 overflow-y-auto">
                    <div className="flex justify-between items-center mb-2">
                      <div className="text-xs font-semibold text-gray-600">
                        {t.translations.SELECTED_RECORDS}
                      </div>
                      <button
                        onClick={() => setSelectedRecords([])}
                        className="text-xs text-red-600 hover:underline"
                      >
                        {t.translations.CLEAR_ALL}
                      </button>
                    </div>
                    <div className="space-y-2">
                      {selectedRecords.map((record) => (
                        <div
                          key={record.id}
                          className="flex items-start justify-between gap-2 p-2 bg-white rounded border border-blue-200"
                        >
                          <div className="flex-1 min-w-0">
                            <div className="font-medium text-sm truncate">
                              {record.name}
                            </div>
                            <div className="text-xs text-gray-500">
                              {record.className && `${record.className} • `}
                              {record.dataSourceName}
                            </div>
                          </div>
                          <button
                            onClick={() => handleToggleRecord(record)}
                            className="p-1 hover:bg-gray-100 rounded-full flex-shrink-0"
                          >
                            <XMarkIcon className="h-3 w-3" />
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Placeholder when nothing selected */}
                {selectedRecords.length === 0 && (
                  <RecordCard record={null} isPlaceholder t={t} />
                )}

                {/* Search Section */}
                <div>
                  <SearchBar
                    placeholder={t.translations.SEARCH_FOR_RECORDS}
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    onSubmit={handleSearch}
                    aditionalFilters={false}
                  />

                  {/* Loading State */}
                  {isSearching && (
                    <div className="border border-gray-300 rounded-lg p-4 text-center mt-2">
                      <div className="loading loading-spinner loading-md"></div>
                      <p className="text-sm text-gray-500 mt-2">
                        {t.translations.SEARCHING}
                      </p>
                    </div>
                  )}

                  {/* No Results State */}
                  {!isSearching &&
                    hasSearched &&
                    searchResults.length === 0 && (
                      <div className="border border-gray-300 rounded-lg p-4 text-center mt-2">
                        <p className="text-sm text-gray-500">
                          {t.translations.NO_RECORDS_FOUND}
                        </p>
                      </div>
                    )}

                  {/* Search Results List */}
                  {!isSearching && searchResults.length > 0 && (
                    <div className="border border-gray-300 rounded-lg max-h-64 overflow-y-auto mt-2">
                      {searchResults.map((record) => {
                        const selected = isRecordSelected(record.id);
                        return (
                          <button
                            key={record.id}
                            onClick={() => handleToggleRecord(record)}
                            className={`w-full text-left p-3 border-b border-gray-200 last:border-b-0 hover:bg-gray-50 transition-colors ${
                              selected ? "bg-blue-50" : ""
                            }`}
                          >
                            <div className="flex items-center justify-between">
                              <div className="flex-1">
                                <div className="font-medium text-sm">
                                  {record.name}
                                </div>
                                <div className="text-xs text-gray-500">
                                  {record.className && `${record.className} • `}
                                  {record.dataSourceName ||
                                    `${t.translations.ID_LABEL} ${record.id}`}
                                </div>
                                {record.description && (
                                  <div className="text-xs text-gray-400 mt-1 line-clamp-1">
                                    {record.description}
                                  </div>
                                )}
                              </div>
                              {selected && (
                                <div className="px-2 py-1 bg-blue-600 text-white text-xs rounded ml-2">
                                  ✓
                                </div>
                              )}
                            </div>
                          </button>
                        );
                      })}
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* ===== FOOTER ===== */}
        <div className="p-6 border-t border-gray-200 flex justify-end gap-3">
          <button
            onClick={onClose}
            disabled={isCreating}
            className="px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors disabled:opacity-50"
          >
            {t.translations.CANCEL}
          </button>
          <button
            disabled={selectedRecords.length === 0 || isCreating}
            onClick={handleCreate}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-gray-300 disabled:cursor-not-allowed transition-colors"
          >
            {isCreating ? (
              <>
                <span className="loading loading-spinner loading-sm mr-2"></span>
                {t.translations.CREATING}
              </>
            ) : (
              <>
                {t.translations.CREATE}{" "}
                {selectedRecords.length > 1 ? `${selectedRecords.length} ` : ""}
                {selectedRecords.length > 1
                  ? t.translations.RELATIONSHIPS
                  : t.translations.RELATIONSHIP}
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
