import { Column } from "../../types/types";
import { useState } from "react";
import PaginationControls from "../PaginationControls"
type SiteManagementTableProps<T extends object> = {
    columns: Column<T>[];
    data: T[];
    expandableKey?: keyof T;
    rowKey?: keyof T;
    truncateLength?: number;
    border?: boolean;
}

export const SiteManagementTable = <T extends object>({
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
                        <tr key={rowIndex}>
                            {columns.map((col, colIndex) => (
                                <td key={colIndex}>
                                    {col.cell
                                        ? col.cell(row, rowIndex)
                                        : col.data === expandableKey && expandableKey !== undefined
                                            ? (
                                                <div>
                                                    <span className="break-words">
                                                        {expandedRow === (row[rowKey as keyof T] as string)
                                                            ? String(row[expandableKey])
                                                            : String(row[expandableKey]).length > truncateLength
                                                                ? String(row[expandableKey]).slice(0, truncateLength) + "..."
                                                                : String(row[expandableKey])}
                                                    </span>
                                                    {String(row[expandableKey]).length > truncateLength && (
                                                        <button
                                                            className="btn btn-xs btn-ghost ml-1"
                                                            onClick={() => setExpandedRow(expandedRow === (row[rowKey as keyof T] as string) ? null : (row[rowKey as keyof T] as string))}
                                                        >
                                                            {expandedRow === (row[rowKey as keyof T] as string) ? "See less" : "See more"}
                                                        </button>
                                                    )}
                                                </div>
                                            )
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
