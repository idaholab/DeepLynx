// src/app/(home)/record/components/AddEdgeModal.tsx

import React, { useState, useEffect } from "react";
import SearchBar from "../../components/SearchBar";

// SVG Icons (keep your existing icon components)
const MagnifyingGlassIcon = ({ className }: { className?: string }) => (
  <svg
    className={className}
    fill="none"
    viewBox="0 0 24 24"
    strokeWidth={1.5}
    stroke="currentColor"
  >
    <path
      strokeLinecap="round"
      strokeLinejoin="round"
      d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z"
    />
  </svg>
);

const XMarkIcon = ({ className }: { className?: string }) => (
  <svg
    className={className}
    fill="none"
    viewBox="0 0 24 24"
    strokeWidth={1.5}
    stroke="currentColor"
  >
    <path
      strokeLinecap="round"
      strokeLinejoin="round"
      d="M6 18L18 6M6 6l12 12"
    />
  </svg>
);

const ArrowRightIcon = ({ className }: { className?: string }) => (
  <svg
    className={className}
    fill="none"
    viewBox="0 0 24 24"
    strokeWidth={1.5}
    stroke="currentColor"
  >
    <path
      strokeLinecap="round"
      strokeLinejoin="round"
      d="M13.5 4.5L21 12m0 0l-7.5 7.5M21 12H3"
    />
  </svg>
);

const ArrowLeftIcon = ({ className }: { className?: string }) => (
  <svg
    className={className}
    fill="none"
    viewBox="0 0 24 24"
    strokeWidth={1.5}
    stroke="currentColor"
  >
    <path
      strokeLinecap="round"
      strokeLinejoin="round"
      d="M10.5 19.5L3 12m0 0l7.5-7.5M3 12h18"
    />
  </svg>
);

// Types
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
  dataSourceId?: number;
}

const relationshipTypes = [
  "MANAGES",
  "OWNS",
  "REPORTS_TO",
  "DEPENDS_ON",
  "RELATED_TO",
  "CONTAINS",
  "ASSIGNED_TO",
];

// Record Detail Card Component
const RecordCard = ({ record, isPlaceholder = false }: any) => {
  if (isPlaceholder) {
    return (
      <div className="border-2 border-dashed border-gray-300 rounded-lg p-4 bg-gray-50">
        <div className="text-center py-8 text-gray-400">
          <MagnifyingGlassIcon className="h-12 w-12 mx-auto mb-2 opacity-30" />
          <p className="text-sm">Search and select records</p>
        </div>
      </div>
    );
  }

  return (
    <div className="border-2 border-blue-500 rounded-lg p-4 bg-blue-50">
      <div className="space-y-2">
        <div>
          <div className="text-xs text-gray-500">Record ID</div>
          <div className="font-mono text-sm">{record.id}</div>
        </div>
        <div>
          <div className="text-xs text-gray-500">Name</div>
          <div className="font-semibold">{record.name}</div>
        </div>
        {record.description && (
          <div>
            <div className="text-xs text-gray-500">Description</div>
            <div className="text-sm">{record.description}</div>
          </div>
        )}
        {record.dataSourceName && (
          <div>
            <div className="text-xs text-gray-500">Data Source</div>
            <div className="inline-block px-2 py-1 text-xs border border-gray-300 rounded">
              {record.dataSourceName}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

// Main Modal Component
export default function AddEdgeModal({
  isOpen,
  onClose,
  currentRecord,
  relationship,
  direction,
  projectId,
  organizationId,
  onSearchRecords,
  onCreateRelationships,
}: AddEdgeModalProps) {
  const [searchTerm, setSearchTerm] = useState("");
  const [searchResults, setSearchResults] = useState<RecordSearchResult[]>([]);
  const [selectedRecords, setSelectedRecords] = useState<RecordSearchResult[]>(
    [],
  );
  const [selectedRelationship, setSelectedRelationship] =
    useState(relationship);
  const [selectedDirection, setSelectedDirection] = useState(direction);
  const [isSearching, setIsSearching] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);

  // Reset state when modal opens/closes
  useEffect(() => {
    if (!isOpen) {
      setSearchTerm("");
      setSearchResults([]);
      setSelectedRecords([]);
      setHasSearched(false);
    }
  }, [isOpen]);

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
      // Filter out the current record from results
      const filteredResults = results.filter((r) => r.id !== currentRecord.id);
      setSearchResults(filteredResults);
    } catch (error) {
      console.error("Error searching records:", error);
      setSearchResults([]);
    } finally {
      setIsSearching(false);
    }
  };

  const handleToggleRecord = (record: RecordSearchResult) => {
    setSelectedRecords((prev) => {
      const isSelected = prev.some((r) => r.id === record.id);
      if (isSelected) {
        return prev.filter((r) => r.id !== record.id);
      }
      return [...prev, record];
    });
  };

  const isRecordSelected = (recordId: number) => {
    return selectedRecords.some((r) => r.id === recordId);
  };

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

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg shadow-xl max-w-6xl w-full max-h-[90vh] overflow-hidden flex flex-col">
        {/* Header */}
        <div className="p-6 border-b border-gray-200">
          <div className="flex justify-between items-center">
            <h3 className="text-2xl font-bold">Create Relationship</h3>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-full transition-colors"
            >
              <XMarkIcon className="h-5 w-5" />
            </button>
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6">
          <div className="grid grid-cols-[1fr_auto_1fr] gap-6 items-start">
            {/* Left: Current Record + Metadata */}
            <div className="space-y-4">
              <p className="text-sm font-medium text-gray-600">
                {selectedDirection === "outgoing" ? "From" : "To"}
              </p>
              <RecordCard record={currentRecord} />

              {/* Relationship Metadata Panel */}
              <div className="border border-gray-300 rounded-lg p-4 bg-gray-50">
                <h4 className="font-semibold text-sm mb-3">
                  Relationship Details
                </h4>

                {/* Direction Selector */}
                <div className="mb-3">
                  <label className="block text-xs text-gray-600 mb-1">
                    Direction
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
                      Outgoing
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
                      Incoming
                    </button>
                  </div>
                </div>
              </div>
            </div>

            {/* Middle: Relationship Arrow */}
            <div className="flex flex-col items-center justify-center pt-12">
              {selectedDirection === "outgoing" ? (
                <ArrowRightIcon className="h-10 w-10 text-blue-600" />
              ) : (
                <ArrowLeftIcon className="h-10 w-10 text-blue-600" />
              )}
              <div className="mt-3 px-4 py-2 bg-blue-100 rounded-lg border border-blue-300 text-center">
                <div className="text-xs text-gray-600 mb-1">Relationship</div>
                <span className="font-semibold text-sm">
                  {selectedRelationship}
                </span>
              </div>
            </div>

            {/* Right: Selection Area */}
            <div>
              <div className="text-sm font-medium text-gray-600 mb-3">
                {selectedDirection === "outgoing" ? "To" : "From"}{" "}
                {selectedRecords.length > 0 && (
                  <span className="text-blue-600">
                    ({selectedRecords.length} selected)
                  </span>
                )}
              </div>

              <div className="space-y-3">
                {/* Selected Records Display */}
                {selectedRecords.length > 0 && (
                  <div className="border-2 border-blue-500 rounded-lg p-3 bg-blue-50 max-h-64 overflow-y-auto">
                    <div className="flex justify-between items-center mb-2">
                      <div className="text-xs font-semibold text-gray-600">
                        Selected Records
                      </div>
                      <button
                        onClick={() => setSelectedRecords([])}
                        className="text-xs text-red-600 hover:underline"
                      >
                        Clear All
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
                  <RecordCard record={null} isPlaceholder />
                )}

                {/* Search Section */}
                <div>
                  <SearchBar
                    placeholder="Search for records..."
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    onSubmit={handleSearch}
                    aditionalFilters={false}
                  />

                  {/* Search Results */}
                  {isSearching && (
                    <div className="border border-gray-300 rounded-lg p-4 text-center">
                      <div className="loading loading-spinner loading-md"></div>
                      <p className="text-sm text-gray-500 mt-2">Searching...</p>
                    </div>
                  )}

                  {!isSearching &&
                    hasSearched &&
                    searchResults.length === 0 && (
                      <div className="border border-gray-300 rounded-lg p-4 text-center">
                        <p className="text-sm text-gray-500">
                          No records found
                        </p>
                      </div>
                    )}

                  {!isSearching && searchResults.length > 0 && (
                    <div className="border border-gray-300 rounded-lg max-h-64 overflow-y-auto">
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
                                  {record.dataSourceName || `ID: ${record.id}`}
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

        {/* Footer */}
        <div className="p-6 border-t border-gray-200 flex justify-end gap-3">
          <button
            onClick={onClose}
            disabled={isCreating}
            className="px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            disabled={selectedRecords.length === 0 || isCreating}
            onClick={handleCreate}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-gray-300 disabled:cursor-not-allowed transition-colors"
          >
            {isCreating ? (
              <>
                <span className="loading loading-spinner loading-sm mr-2"></span>
                Creating...
              </>
            ) : (
              <>
                Create{" "}
                {selectedRecords.length > 1 ? `${selectedRecords.length} ` : ""}
                Relationship{selectedRecords.length > 1 ? "s" : ""}
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
