"use client";

import { useLanguage } from "@/app/contexts/Language";
import Link from "next/link";
import { myRecentSearches, mySavedSearches } from "../dummy_data/data";
import { Column, MySearchsTable, PopularTable } from "../types/types";
import AvatarCell from "./Avatar";
import GenericTable from "./GenericTable";
import Tabs from "./Tabs";
import { useState } from "react";

interface SavedSearchProps {
  className?: string;
}

const SavedSearches = ({ className }: SavedSearchProps) => {
  const { t } = useLanguage();
  const [activeTab, setActiveTab] = useState("Recent");

  const my_search_table_columns: Column<MySearchsTable>[] = [
    {
      header: "Name",
      data: "name",
    },
    {
      header: "Filters",
      cell: (row) =>
        row.filters.map((filter, index) => (
          <span key={index} className="badge badge-info mr-2 mt-2">
            {filter}
          </span>
        )),
      sortable: false,
    },
    {
      header: "Saved",
      data: "createdAt",
      sortable: false,
    },
  ];


  const tabData = [
    {
      label: "Recent",
      content: (
        <GenericTable
          columns={my_search_table_columns}
          data={myRecentSearches}
          enablePagination
          rowsPerPage={5}
        />
      ),
    },
    {
      label: "Favorites",
      content: (
        <GenericTable
          columns={my_search_table_columns}
          data={mySavedSearches}
          enablePagination
          rowsPerPage={5}
        />
      ),
    },
  ];

  return (
    <div className="card bg-base-200/30 border border-base-300/50 shadow-sm m-2">
      <div className="card-body">
        {/* Header */}
        <div className="flex justify-between items-center mb-4">
          <h2 className="card-title text-base-content">Saved Searches</h2>
          <Link
            className="btn btn-secondary btn-sm"
            href="/savedsearchesplaceholder"
          >
            {t.translations.VISIT}
          </Link>
        </div>

        {/* Tabs */}
        <Tabs
          tabs={tabData}
          className="tabs tabs-boxed bg-base-100"
          activeTab={activeTab}
          onTabChange={(label) => setActiveTab(label)}
        />
      </div>
    </div>
  );
};

export default SavedSearches;
