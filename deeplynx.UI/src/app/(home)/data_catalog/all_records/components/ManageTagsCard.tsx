"use client";

import { useLanguage } from "@/app/contexts/Language";

type AvailableTag = {
    id: number;
    name: string;
    projectId: number | null;
};

// Props for the bulk tag management card shown while record selection mode is active.
type Props = {
    selectedRecordCount: number;
    bulkTagQuery: string;
    onBulkTagQueryChange: (value: string) => void;
    availableTags: AvailableTag[];
    onCancelBulkTags: () => void;
    getBulkTagState: (tag: AvailableTag) => BulkTagState;
    onToggleBulkTag: (tag: AvailableTag) => void;
};

type BulkTagState = "checked" | "unchecked" | "indeterminate";

/**
 *  Displays available tags for the selected records and lets the user stage
 *  bulk tag attach/unattach changes before applying them.
 */
export default function ManageTagsCard({
    selectedRecordCount,
    bulkTagQuery,
    onBulkTagQueryChange,
    availableTags,
    onCancelBulkTags,
    getBulkTagState,
    onToggleBulkTag,
}:  Props){
    const {t} = useLanguage();
    
    const filteredTags = availableTags.filter((tag) =>
        tag.name.toLowerCase().includes(bulkTagQuery.toLowerCase()),
    );

    return (
        <div className="rounded-box border border-base-300 bg-base-100 shadow-sm">
            <div className="flex items-center justify-between border-b border-base-300 px-4 py-3">
                <div className="text-sm font-semibold">Manage tags</div>
                
                <span className="text-xs text-base-content/60">
                      {selectedRecordCount} records selected   
                </span>
            </div>
            
            <div className="p-4">
                <input
                    type="search"
                    className="input input-sm w-full"
                    placeholder={t.translations.SEARCH_TAGS}
                    value={bulkTagQuery}
                    onChange={(e) => onBulkTagQueryChange(e.target.value)}
                />
                
                <div className="mt-4 max-h-64 overflow-auto">
                    {filteredTags.length === 0 ? (
                        <p className="text-xs text-base-content/50">
                            No tags available
                        </p>
                    ) : (
                        <div className="space-y-2">
                            {filteredTags.map((tag) => {
                                const state = getBulkTagState(tag);

                                return (
                                    <div key={tag.id}
                                         className="flex items-center gap-2 text-sm">
                                        <input
                                            type="checkbox"
                                            className="checkbox checkbox-primary checkbox-xs"
                                            checked={state === "checked"}
                                            ref={(input) => {
                                                if (input) {
                                                    input.indeterminate = state === "indeterminate";
                                                }
                                            }}
                                            onChange={() => onToggleBulkTag(tag)}
                                        />

                                        <span>{tag.name}</span>
                                    </div>
                                );
                            })}
                        </div>
                    )}
                </div>
                
                <div className="mt-4 flex justify-end">
                    <button
                        type="button"
                        className="btn btn-sm btn-ghost"
                        onClick={onCancelBulkTags}
                    >
                        Cancel
                    </button>
                </div>
            </div>
        </div>
    );
}