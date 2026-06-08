"use client";

import React from "react";
import { Column, RecordTableRow } from "@/app/(home)/types/types";
import GenericTable from "./GenericTable";

type GridViewProps = {
  columns: Column<RecordTableRow>[];
  data: RecordTableRow[];
  activeSearchTerms?: string[];
  selectedProjects?: string[];
};

const GridView = ({
  columns,
  data,
  selectedProjects,
}: GridViewProps) => {

  const filteredRecords =
    selectedProjects?.includes("All your Projects") || !selectedProjects
      ? data
      : data.filter(
        (record) =>
          record.projectId !== undefined &&
          selectedProjects.includes(String(record.projectId))
      );

  return (
    <div className="px-3 sm:px-6 lg:px-8">
      <GenericTable
        columns={columns}
        data={filteredRecords}
        enablePagination
        rowsPerPage={8}
        gridView={true}
        bordered={false}
      />
    </div>
  );
};

export default GridView;
