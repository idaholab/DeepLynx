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
];

// Record Detail Card Component
const RecordCard = ({ record, label, isPlaceholder = false }: any) => {
  if (isPlaceholder) {
    return (
      <div className="border-2 border-dashed border-base-300 rounded-lg p-4 bg-base-200/30">
        <div className="text-sm font-medium text-base-content/40 mb-3">
          {label}
        </div>
        <div className="text-center py-8 text-base-content/40">
          <MagnifyingGlassIcon className="h-12 w-12 mx-auto mb-2 opacity-30" />
          <p className="text-sm">Search and select a record</p>
        </div>
      </div>
    );
  }

  return (
    <div className="border-2 border-primary rounded-lg p-4 bg-primary/5">
      <div className="text-sm font-medium text-base-content/60 mb-3">
        {label}
      </div>
      <div className="space-y-2">
        <div>
          <div className="text-xs text-base-content/60">Record ID</div>
          <div className="font-mono text-sm">{record.id}</div>
        </div>
        <div>
          <div className="text-xs text-base-content/60">Name</div>
          <div className="font-semibold">{record.name}</div>
        </div>
        <div>
          <div className="text-xs text-base-content/60">Description</div>
          <div className="text-sm">
            {record.description || "No description"}
          </div>
        </div>
        <div>
          <div className="text-xs text-base-content/60">Data Source</div>
          <div className="badge badge-sm badge-outline">
            {record.dataSource}
          </div>
        </div>
      </div>
    </div>
  );
};

// Concept 2: Side-by-Side with Collapsible Search
function Concept2({
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
  const [selected, setSelected] = useState<any>(null);

  const filteredRecords = mockRecords.filter((r) =>
    r.name.toLowerCase().includes(searchTerm.toLowerCase()),
  );

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-base-100 rounded-lg shadow-xl max-w-6xl w-full max-h-[90vh] overflow-hidden flex flex-col">
        <div className="p-6 border-b border-base-300">
          <div className="flex justify-between items-center">
            <h3 className="text-2xl font-bold">Create Relationship</h3>
            <button
              onClick={onClose}
              className="btn btn-sm btn-ghost btn-circle"
            >
              <XMarkIcon className="h-5 w-5" />
            </button>
          </div>
        </div>

        <div className="flex-1 overflow-y-auto p-6">
          <div className="grid grid-cols-[1fr_auto_1fr] gap-6 items-start">
            {/* Left: Current Record */}
            <RecordCard
              record={currentRecord}
              label={direction === "outgoing" ? "From" : "To"}
            />

            {/* Middle: Relationship */}
            <div className="flex flex-col items-center justify-center pt-12">
              {direction === "outgoing" ? (
                <ArrowRightIcon className="h-10 w-10 text-primary" />
              ) : (
                <ArrowLeftIcon className="h-10 w-10 text-primary" />
              )}
              <div className="mt-3 px-4 py-2 bg-primary/10 rounded-lg border border-primary/30 text-center">
                <div className="text-xs text-base-content/60 mb-1">
                  Relationship
                </div>
                <span className="font-semibold text-sm">{relationship}</span>
              </div>
            </div>

            {/* Right: Selection Area */}
            <div>
              <div className="text-sm font-medium text-base-content/60 mb-3">
                {direction === "outgoing" ? "To" : "From"}
              </div>

              {selected ? (
                <div className="space-y-3">
                  <RecordCard record={selected} label="" />
                  <button
                    onClick={() => setSelected(null)}
                    className="btn btn-sm btn-outline w-full"
                  >
                    Change Selection
                  </button>
                </div>
              ) : (
                <div className="space-y-3">
                  <RecordCard record={null} label="" isPlaceholder />

                  {/* Search Dropdown */}
                  <div>
                    <div className="relative mb-2">
                      <MagnifyingGlassIcon className="absolute left-3 top-3 h-4 w-4 text-base-content/40" />
                      <input
                        type="text"
                        placeholder="Search records..."
                        className="input input-sm input-bordered w-full pl-9"
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                      />
                    </div>
                    <div className="border border-base-300 rounded-lg max-h-48 overflow-y-auto">
                      {filteredRecords.map((record) => (
                        <button
                          key={record.id}
                          onClick={() => setSelected(record)}
                          className="w-full text-left p-2 border-b border-base-300 last:border-b-0 hover:bg-base-200 transition-colors"
                        >
                          <div className="font-medium text-sm">
                            {record.name}
                          </div>
                          <div className="text-xs text-base-content/60">
                            {record.type}
                          </div>
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="p-6 border-t border-base-300 flex justify-end gap-3">
          <button onClick={onClose} className="btn btn-ghost">
            Cancel
          </button>
          <button disabled={!selected} className="btn btn-primary">
            Create Relationship
          </button>
        </div>
      </div>
    </div>
  );
}

export function AddEdgeModal({
  isOpen,
  onClose,
  relationship,
  direction = "outgoing",
}: {
  isOpen: boolean;
  onClose: () => void;
  relationship: string;
  direction?: "outgoing" | "incoming";
}) {
  return (
    <Concept2
      isOpen={isOpen}
      onClose={onClose}
      relationship={relationship}
      direction={direction}
    />
  );
}
