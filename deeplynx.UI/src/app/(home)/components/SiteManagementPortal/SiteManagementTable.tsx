import { Column } from "../../types/types";
import { useState } from "react";
import PaginationControls from "../PaginationControls"
type SiteManagementTableProps<T extends { id: string | number }> = {
    columns: Column<T>[];
    data: T[];
    expandableKey?: keyof T;
    rowKey?: keyof T;
    truncateLength?: number;
    border?: boolean;
}

const ExpandableCell = ({
    value,
    truncateLength,
    isExpanded,
    onToggle,
}: {
    value: string;
    truncateLength: number;
    isExpanded: boolean;
    onToggle: () => void;
}) => {
    const isTruncatable = value.length > truncateLength;

    return (
        <div>
            <span className="break-words">
                {isExpanded || !isTruncatable
                    ? value
                    : value.slice(0, truncateLength) + "..."}
            </span>
            {isTruncatable && (
                <button
                    className="btn btn-xs btn-ghost ml-1"
                    onClick={onToggle}
                >
                    {isExpanded ? "See less" : "See more"}
                </button>
            )}
        </div>
    );
};

export const SiteManagementTable = <T extends { id: string | number }>({
    columns,
    data,
    expandableKey,
    truncateLength = 100,
    border,
    rowKey

}: SiteManagementTableProps<T>) => {
    const [currentPage, setCurrentPage] = useState(1);
    const [pageSize, setPageSize] = useState(5);
    const [expandedRow, setExpandedRow] = useState<string | null>(null);

    const totalPages = Math.max(1, Math.ceil(data.length / pageSize));
    const firstRowIndex = (currentPage - 1) * pageSize;
    const currentData = data.slice(firstRowIndex, firstRowIndex + pageSize);


    return (
        <div className={`overflow-x-auto ${border ? "shadow-md shadow-dynamic-shadow rounded-xl" : ""}`}>
            <table className="table">
                {/* head */}
                <thead>
                    <tr className="text-base-content bg-base-200">
                        {columns.map((col, colIndex) => (
                            <th key={colIndex}>{col.header}</th>
                        ))}
                    </tr>
                </thead>
                <tbody>
                    {currentData.map((row, rowIndex) => (
                        <tr key={row.id}>
                            {columns.map((col, colIndex) => (
                                <td key={colIndex}>
                                    {col.cell
                                        ? col.cell(row, rowIndex)
                                        : col.data === expandableKey && expandableKey !== undefined
                                            ? <ExpandableCell
                                                value={String(row[expandableKey])}
                                                truncateLength={truncateLength}
                                                isExpanded={expandedRow === String(row.id)}
                                                onToggle={() => setExpandedRow(expandedRow === String(row.id) ? null : String(row.id))}
                                            />
                                            : (row[col.data as keyof T] as React.ReactNode)}
                                </td>
                            ))}
                        </tr>
                    )
                    )}
                </tbody>
            </table>
            <PaginationControls
                currentPage={currentPage}
                pageSize={pageSize}
                totalPages={totalPages}
                onPageChange={setCurrentPage}
                onPageSizeChange={(newSize) => {
                    setPageSize(newSize);
                    setCurrentPage(1);
                }}
            />
        </div>
    )
}
