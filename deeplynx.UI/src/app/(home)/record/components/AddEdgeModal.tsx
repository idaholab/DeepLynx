import React, { useState } from "react";

// SVG Icons
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

const CalendarIcon = ({ className }: { className?: string }) => (
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
      d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 012.25-2.25h13.5A2.25 2.25 0 0121 7.5v11.25m-18 0A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75m-18 0v-7.5A2.25 2.25 0 015.25 9h13.5A2.25 2.25 0 0121 11.25v7.5"
    />
  </svg>
);

const ClockIcon = ({ className }: { className?: string }) => (
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
      d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z"
    />
  </svg>
);

// Mock data
const currentRecord = {
  id: "12345",
  name: "Customer Account - ABC Corp",
  description: "Primary enterprise customer account for ABC Corporation",
  dataSource: "Salesforce CRM",
};

const mockRecords = [
  {
    id: "1",
    name: "Support Ticket #1234",
    type: "Ticket",
    description: "Critical issue with login system",
    dataSource: "Zendesk",
  },
  {
    id: "2",
    name: "Jane Smith",
    type: "Contact",
    description: "Account Manager at ABC Corp",
    dataSource: "Salesforce CRM",
  },
  {
    id: "3",
    name: "Q4 Sales Report",
    type: "Document",
    description: "Quarterly sales performance analysis",
    dataSource: "Google Drive",
  },
  {
    id: "4",
    name: "Project Phoenix",
    type: "Project",
    description: "Digital transformation initiative",
    dataSource: "Jira",
  },
  {
    id: "5",
    name: "Marketing Campaign 2024",
    type: "Campaign",
    description: "Q1 digital marketing initiative",
    dataSource: "HubSpot",
  },
  {
    id: "6",
    name: "John Doe",
    type: "Contact",
    description: "Technical Lead at XYZ Inc",
    dataSource: "Salesforce CRM",
  },
];

const relationshipTypes = [
  "MANAGES",
  "OWNS",
  "REPORTS_TO",
  "DEPENDS_ON",
  "RELATED_TO",
  "CONTAINS",
  "ASSIGNED_TO",
];

const recentRelationships = [
  { from: "ABC Corp", to: "Jane Smith", type: "MANAGES", date: "2 hours ago" },
  {
    from: "ABC Corp",
    to: "Support Ticket #1234",
    type: "OWNS",
    date: "5 hours ago",
  },
  {
    from: "Project Phoenix",
    to: "ABC Corp",
    type: "ASSIGNED_TO",
    date: "Yesterday",
  },
];

// Record Detail Card Component
const RecordCard = ({ record, isPlaceholder = false }: any) => {
  if (isPlaceholder) {
    return (
      <div>
        <div className="border-2 border-dashed border-gray-300 rounded-lg p-4 bg-gray-50">
          <div className="text-center py-8 text-gray-400">
            <MagnifyingGlassIcon className="h-12 w-12 mx-auto mb-2 opacity-30" />
            <p className="text-sm">Search and select records</p>
          </div>
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
        <div>
          <div className="text-xs text-gray-500">Description</div>
          <div className="text-sm">
            {record.description || "No description"}
          </div>
        </div>
        <div>
          <div className="text-xs text-gray-500">Data Source</div>
          <div className="inline-block px-2 py-1 text-xs border border-gray-300 rounded">
            {record.dataSource}
          </div>
        </div>
      </div>
    </div>
  );
};

// Main Modal Component
function AddEdgeModal({
  isOpen,
  onClose,
  relationship,
  direction,
}: {
  isOpen: boolean;
  onClose: () => void;
  relationship: string;
  direction: "outgoing" | "incoming";
}) {
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedRecords, setSelectedRecords] = useState<any[]>([]);
  const [selectedRelationship, setSelectedRelationship] =
    useState(relationship);
  const [selectedDirection, setSelectedDirection] = useState(direction);
  const [notes, setNotes] = useState("");
  const [startDate, setStartDate] = useState("");

  const filteredRecords = mockRecords.filter((r) =>
    r.name.toLowerCase().includes(searchTerm.toLowerCase()),
  );

  const handleToggleRecord = (record: any) => {
    setSelectedRecords((prev) => {
      const isSelected = prev.some((r) => r.id === record.id);
      if (isSelected) {
        return prev.filter((r) => r.id !== record.id);
      }
      return [...prev, record];
    });
  };

  const isRecordSelected = (recordId: string) => {
    return selectedRecords.some((r) => r.id === recordId);
  };

  const handleCreate = () => {
    console.log("Creating relationships:", {
      records: selectedRecords,
      relationship: selectedRelationship,
      direction: selectedDirection,
      notes,
      startDate,
    });
    onClose();
    setSelectedRecords([]);
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 mt-25">
      <div className="bg-white rounded-lg shadow-xl max-w-6xl w-full max-h-[90vh] overflow-hidden flex flex-col">
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

        <div className="flex-1 overflow-y-auto p-6">
          <div className="grid grid-cols-[1fr_auto_1fr] gap-6 items-start">
            {/* Left: Current Record + Metadata */}

            <div className="space-y-4">
              {selectedDirection === "outgoing" ? <p>From</p> : <p>To</p>}
              <RecordCard record={currentRecord} />
              {/* Relationship Metadata Panel */}
              <div className="border border-gray-300 rounded-lg p-4 bg-gray-50">
                <h4 className="font-semibold text-sm mb-3">
                  Relationship Details
                </h4>

                {/* Selector */}
                {/* <div className="mb-3">
                  <label className="block text-xs text-gray-600 mb-1">
                    Relationship Type
                  </label>
                  <select
                    value={selectedRelationship}
                    onChange={(e) => setSelectedRelationship(e.target.value)}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm"
                  >
                    {relationshipTypes.map((type) => (
                      <option key={type} value={type}>
                        {type}
                      </option>
                    ))}
                  </select>
                </div> */}

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
                              {record.type} • {record.dataSource}
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
                  <RecordCard record={null} label="" isPlaceholder />
                )}

                {/* Search Section */}
                <div>
                  <div className="relative mb-2">
                    <MagnifyingGlassIcon className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
                    <input
                      type="text"
                      placeholder="Search records..."
                      className="w-full pl-9 pr-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                      value={searchTerm}
                      onChange={(e) => setSearchTerm(e.target.value)}
                    />
                  </div>
                  <div className="border border-gray-300 rounded-lg max-h-48 overflow-y-auto">
                    {filteredRecords.map((record) => {
                      const selected = isRecordSelected(record.id);
                      return (
                        <button
                          key={record.id}
                          onClick={() => handleToggleRecord(record)}
                          className={`w-full text-left p-2 border-b border-gray-200 last:border-b-0 hover:bg-gray-100 transition-colors ${
                            selected ? "bg-blue-50" : ""
                          }`}
                        >
                          <div className="flex items-center justify-between">
                            <div className="flex-1">
                              <div className="font-medium text-sm">
                                {record.name}
                              </div>
                              <div className="text-xs text-gray-500">
                                {record.type}
                              </div>
                            </div>
                            {selected && (
                              <div className="px-2 py-1 bg-blue-600 text-white text-xs rounded">
                                ✓
                              </div>
                            )}
                          </div>
                        </button>
                      );
                    })}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="p-6 border-t border-gray-200 flex justify-end gap-3">
          <button
            onClick={onClose}
            className="px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
          >
            Cancel
          </button>
          <button
            disabled={selectedRecords.length === 0}
            onClick={handleCreate}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-gray-300 disabled:cursor-not-allowed transition-colors"
          >
            Create{" "}
            {selectedRecords.length > 1 ? `${selectedRecords.length} ` : ""}
            Relationship{selectedRecords.length > 1 ? "s" : ""}
          </button>
        </div>
      </div>
    </div>
  );
}

// Demo wrapper
export default function App() {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div className="min-h-screen bg-gray-100 p-8">
      <div className="max-w-4xl mx-auto">
        <h1 className="text-3xl font-bold mb-6">
          Multi-Select Edge Modal with Metadata
        </h1>
        <button
          onClick={() => setIsOpen(true)}
          className="px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
        >
          Open Modal
        </button>
      </div>

      <AddEdgeModal
        isOpen={isOpen}
        onClose={() => setIsOpen(false)}
        relationship="MANAGES"
        direction="outgoing"
      />
    </div>
  );
}
