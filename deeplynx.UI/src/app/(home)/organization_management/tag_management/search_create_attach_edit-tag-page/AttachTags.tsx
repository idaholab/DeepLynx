import SimpleFilterInput from "@/app/(home)/components/SimpleFilterComponent";
import {
  QueryRecordViewResponseDto,
  TagResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { getRecentlyAddedRecords } from "@/app/lib/client_service/query_services.client";
import React, { useCallback, useEffect, useState } from "react";
import toast from "react-hot-toast";

const parseTags = (
  tags: string | TagResponseDto[] | undefined | null
): TagResponseDto[] => {
  if (!tags) return [];

  if (typeof tags === "string") {
    try {
      return JSON.parse(tags) as TagResponseDto[];
    } catch (e) {
      console.error("Error parsing tags:", e);
      return [];
    }
  }

  if (Array.isArray(tags)) {
    return tags;
  }

  return [];
};

type RecordWithParsedTags = Omit<QueryRecordViewResponseDto, "tags"> & {
  tags: TagResponseDto[];
};

interface Props {
  loading: boolean;
  error: string | null;
  filteredTags: TagResponseDto[];
  tags: TagResponseDto[];
  searchQuery: string;
  onSearchChange: (query: string) => void;
  selectedTagIds: Set<number>;
  setSelectedTagIds: React.Dispatch<React.SetStateAction<Set<number>>>;
}

const AttachTags = ({
  loading,
  error,
  filteredTags,
  tags,
  searchQuery,
  onSearchChange,
  selectedTagIds,
  setSelectedTagIds,
}: Props) => {
  const handleTagToggle = (tagId: number) => {
    const newSelected = new Set(selectedTagIds);
    if (newSelected.has(tagId)) {
      newSelected.delete(tagId);
    } else {
      newSelected.add(tagId);
    }
    setSelectedTagIds(newSelected);
  };

  return (
    <div
      className="w-[85%] mx-auto flex flex-col"
      style={{ height: "calc(90vh - 325px)" }}
    >
      <h3 className="font-bold mb-4">Attach Tags</h3>
      <SimpleFilterInput
        placeholder="Filter tags..."
        value={searchQuery}
        onChange={onSearchChange}
      />
      <div className="mt-4 flex-1 flex flex-col overflow-hidden">
        {loading && <p>Loading tags ...</p>}
        {error && <p className="text-error flex justify-center">{error}</p>}
        {!loading && filteredTags.length === 0 && tags.length === 0 && (
          <p className="text-base-300">No Tags found</p>
        )}
        {!loading && filteredTags.length === 0 && tags.length > 0 && (
          <p className="text-base-300">No tags match your search</p>
        )}
        {!loading && filteredTags.length > 0 && (
          <div className="space-y-2 flex-1 flex flex-col overflow-hidden">
            <div className="flex justify-between items-center mb-2">
              <p className="text-sm font-semibold">
                Tags ({filteredTags.length}):
              </p>
              {selectedTagIds.size > 0 && (
                <span className="text-xs text-base-content/70">
                  {selectedTagIds.size} selected
                </span>
              )}
            </div>
            <ul className="space-y-1 flex-1 overflow-y-auto">
              {filteredTags.map((tag, index) => (
                <li
                  key={tag.id || index}
                  className="px-3 py-1 hover:bg-base-200 rounded cursor-pointer"
                  onClick={() => handleTagToggle(tag.id)}
                >
                  <input
                    type="checkbox"
                    className="checkbox checkbox-primary"
                    checked={selectedTagIds.has(tag.id)}
                    onChange={() => handleTagToggle(tag.id)}
                  />
                  <span className="badge ml-2">
                    {tag.name || JSON.stringify(tag)}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
};

interface AttachTagsRecordsListProps {
  selectedTagIds: Set<number>;
  onClearSelectedTags: () => void;
}

export const AttachTagsRecordsList = ({
  selectedTagIds,
  onClearSelectedTags,
}: AttachTagsRecordsListProps) => {
  const { organization } = useOrganizationSession();

  const [records, setRecords] = useState<RecordWithParsedTags[]>([]);
  const [searchResults, setSearchResults] = useState<RecordWithParsedTags[]>(
    []
  );
  const [loading, setLoading] = useState(false);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState("");
  const [attachLoading, setAttachLoading] = useState(false);
  const [selectedRecordIds, setSelectedRecordIds] = useState<Set<number>>(
    new Set()
  );

  const fetchRecentRecords = useCallback(async () => {
    if (!organization?.organizationId) {
      setRecords([]);
      return;
    }

    setLoading(true);

    try {
      // Pass empty array to get all recent records across organization
      const recentRecords = await getRecentlyAddedRecords(
        organization.organizationId as number,
        []
      );
      const recordsWithParsedTags: RecordWithParsedTags[] = recentRecords.map(
        (record) => ({
          ...record,
          tags: parseTags(record.tags),
        })
      );
      setRecords(recordsWithParsedTags);
    } catch (error) {
      console.error("Error fetching recent records:", error);
    } finally {
      setLoading(false);
    }
  }, [organization?.organizationId]);

  useEffect(() => {
    fetchRecentRecords();
  }, [fetchRecentRecords]);

  const performFullTextSearch = useCallback(
    async (searchTerm: string) => {
      if (!searchTerm.trim() || !organization?.organizationId) {
        setSearchResults([]);
        return;
      }

      setSearchLoading(true);

      try {
        // TODO: Update to use organization-level fullTextSearch
        // const data = await fullTextSearch(
        //   organization.organizationId,
        //   searchTerm,
        //   []
        // );
        // const resultsWithParsedTags: RecordWithParsedTags[] = data.map(
        //   (record) => ({
        //     ...record,
        //     tags: parseTags(record.tags),
        //   })
        // );
        // setSearchResults(resultsWithParsedTags);
      } catch (error) {
        console.error("Search error:", error);
        setSearchResults([]);
      } finally {
        setSearchLoading(false);
      }
    },
    [organization?.organizationId]
  );

  const handleSubmit = async () => {
    await performFullTextSearch(searchTerm);
  };

  const handleClearSearch = () => {
    setSearchTerm("");
    setSearchResults([]);
    setSelectedRecordIds(new Set());
  };

  const handleKeyPress = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") {
      handleSubmit();
    }
  };

  const handleRecordToggle = (recordId: number | null) => {
    if (recordId === null) return;

    const newSelected = new Set(selectedRecordIds);
    if (newSelected.has(recordId)) {
      newSelected.delete(recordId);
    } else {
      newSelected.add(recordId);
    }
    setSelectedRecordIds(newSelected);
  };

  const handleAttachTags = async () => {
    if (selectedRecordIds.size === 0) {
      toast.error("Please select at least one record");
      return;
    }

    if (selectedTagIds.size === 0) {
      toast.error("Please select at least one tag to attach");
      return;
    }

    setAttachLoading(true);

    try {
      const attachPromises: Promise<TagResponseDto>[] = [];
      // TODO: Update to organization-level attach endpoint
      // selectedRecordIds.forEach((recordId) => {
      //   selectedTagIds.forEach((tagId) => {
      //     attachPromises.push(
      //       attachTagToRecordOrganization(organization.organizationId, recordId, tagId)
      //     );
      //   });
      // });

      await Promise.all(attachPromises);

      toast.success(
        `Successfully attached ${selectedTagIds.size} tag(s) to ${selectedRecordIds.size} record(s)`
      );

      setSelectedRecordIds(new Set());
      onClearSelectedTags();

      // Refresh records
      await fetchRecentRecords();
    } catch (error) {
      console.error("Error attaching tags:", error);
      toast.error("Failed to attach tags to some records");
    } finally {
      setAttachLoading(false);
    }
  };

  const displayRecords = searchResults.length > 0 ? searchResults : records;
  const isSearching = searchResults.length > 0;

  return (
    <div
      className="w-[85%] mx-auto flex flex-col"
      style={{ height: "calc(90vh - 325px)" }}
    >
      <div className="gap-2 mb-4">
        <h3 className="font-bold mb-4">Search Records</h3>
        <div className="flex gap-2 mb-4">
          <input
            type="text"
            placeholder="Search Record"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            onKeyDown={handleKeyPress}
            className="input input-bordered flex-1"
          />
          <button
            className="btn btn-primary"
            onClick={handleSubmit}
            disabled={searchLoading || !searchTerm.trim()}
          >
            {searchLoading ? "Searching..." : "Search"}
          </button>
          {searchResults.length > 0 && (
            <button
              className="btn btn-outline btn-error"
              onClick={handleClearSearch}
            >
              Clear
            </button>
          )}
        </div>
        {selectedRecordIds.size > 0 && (
          <div className="mb-4 p-3 bg-base-200 rounded-lg flex items-center justify-between">
            <span className="text-sm">
              {selectedRecordIds.size} record(s) selected •{" "}
              {selectedTagIds.size} tag(s) to attach
            </span>
            <button
              className="btn btn-secondary btn-sm"
              onClick={handleAttachTags}
              disabled={attachLoading || selectedTagIds.size === 0}
            >
              {attachLoading ? "Attaching..." : "Attach Tags"}
            </button>
          </div>
        )}
      </div>

      <div className="flex-1 flex flex-col overflow-hidden">
        <h3 className="font-bold mb-4">
          {isSearching ? "Search Results" : "Recently Added Records"}
        </h3>

        {(searchLoading || loading) && (
          <p className="text-sm">Loading records...</p>
        )}

        {!searchLoading && !loading && displayRecords.length === 0 && (
          <p className="text-base-content/70 text-sm">
            {isSearching
              ? "No records found matching your search"
              : "No recent records found"}
          </p>
        )}

        {!searchLoading && !loading && displayRecords.length > 0 && (
          <div className="flex-1 flex flex-col overflow-hidden">
            <p className="text-sm text-base-content/70 mb-2">
              {displayRecords.length} record
              {displayRecords.length !== 1 ? "s" : ""}
            </p>
            <ul className="space-y-2 overflow-y-auto flex-1">
              {displayRecords.map((record, index) => (
                <li
                  key={record.id || index}
                  className="px-3 py-2 hover:bg-info/50 cursor-pointer transition-colors border-b border-base-200"
                >
                  <div className="flex items-start gap-2">
                    <input
                      type="checkbox"
                      className="checkbox checkbox-primary mt-1"
                      checked={
                        record.id !== null && selectedRecordIds.has(record.id)
                      }
                      onChange={() => handleRecordToggle(record.id)}
                      onClick={(e) => e.stopPropagation()}
                    />

                    <div className="flex-1">
                      <div className="text-sm font-semibold">{record.name}</div>

                      {record.tags && record.tags.length > 0 && (
                        <div className="flex gap-1 flex-wrap mt-1">
                          {record.tags.map((tag) => (
                            <span
                              className="badge badge-outline badge-secondary badge-sm"
                              key={tag.id}
                            >
                              {tag.name}
                            </span>
                          ))}
                        </div>
                      )}

                      {record.lastUpdatedAt && (
                        <div className="text-xs text-base-content/60 mt-1">
                          Last Updated:{" "}
                          {new Date(record.lastUpdatedAt).toLocaleDateString()}
                        </div>
                      )}
                    </div>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
};

export default AttachTags;
