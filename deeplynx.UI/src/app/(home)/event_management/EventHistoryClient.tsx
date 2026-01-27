"use client";

import Link from "next/link";
import React, { useState, useEffect, useCallback } from "react";
import GenericTable from "@/app/(home)/components/GenericTable";
import { Column } from "@/app/(home)/types/types";
import GenericTableSkeleton from "@/app/(home)/components/skeletons/generictableskeleton";
import {
  EventResponseDto,
  PaginatedEventsResponseDto,
} from "../types/responseDTOs";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  EventFilterParams,
  queryAuthorizedEvents,
} from "@/app/lib/client_service/event_services.client";
import ProjectDropdown from "../components/ProjectDropdown";

type Props = {
  initialProjects: { id: string; name: string }[];
  initialSelectedProjects: string[];
};

const EventsHistoryClient = ({initialProjects, initialSelectedProjects}: Props) => {
  const [rowsPerPage, setRowsPerPage] = useState(25);
  const [projects] = useState(initialProjects);
  const [selectedProjects, setSelectedProjects] = useState<string[]>(
    initialSelectedProjects
  );

  const getThirtyDaysAgo = () => {
    const date = new Date();
    date.setDate(date.getDate() - 30);
    return date.toISOString().split("T")[0];
  };

  const [data, setData] = useState<EventResponseDto[]>([]);
  const [pagination, setPagination] = useState({
    pageNumber: 1,
    pageSize: rowsPerPage,
    totalCount: 0,
  });
  const [loading, setLoading] = useState(false);
  const [filters, setFilters] = useState<EventFilterParams>({
    startDate: getThirtyDaysAgo(),
  });
  const { organization, hasLoaded: orgLoaded } = useOrganizationSession();

  type FilterConfig = {
    key: string;
    label: string;
    placeholder?: string;
    type?: "text" | "select" | "date" | "datetime-local";
    options?: { value: string; label: string }[];
  };

  const filterConfig: FilterConfig[] = [
    { key: "startDate", label: "Start Date", type: "date" },
    { key: "endDate", label: "End Date", type: "date" },
    {
      key: "projectName",
      label: "Project Name",
      placeholder: "Filter by project name...",
    },
    {
      key: "lastUpdatedBy",
      label: "Last Updated By",
      placeholder: "Filter by user...",
    },
    {
      key: "operation",
      label: "Operation",
      placeholder: "Filter by operation...",
    },
    {
      key: "entityType",
      label: "Entity Type",
      placeholder: "Filter by entity type...",
    },
    {
      key: "entityName",
      label: "Entity Name",
      placeholder: "Filter by entity name...",
    },
    {
      key: "dataSourceName",
      label: "Data Source",
      placeholder: "Filter by data source...",
    },
  ];

  // Handler for when filters change
  const handleFilterChange = (newFilters: EventFilterParams) => {
    setFilters(newFilters);
  };

  const fetchEvents = useCallback(
    async (pageNumber: number, pageSize: number) => {

      if (!organization?.organizationId || selectedProjects.length === 0) {
        setData([]);
        setPagination({
          pageNumber: 1,
          pageSize: rowsPerPage,
          totalCount: 0,
        });
        return;
      }

      setLoading(true);
      try {
        const organizationId = Number(organization.organizationId);
        const isAllSelected = selectedProjects.length === initialProjects.length;
        
        const projectIds = isAllSelected
          ? []
          : selectedProjects.map(Number);

        const result: PaginatedEventsResponseDto = await queryAuthorizedEvents(
          organizationId,         
          projectIds,
          {
            pageNumber,
            pageSize,
            ...filters,
          }
        );

        setData(result.items);
        setPagination({
          pageNumber: result.pageNumber,
          pageSize: result.pageSize,
          totalCount: result.totalCount,
        });
      } catch (error) {
        console.error("Failed to fetch events:", error);
        setData([]);
        setPagination({
          pageNumber: 1,
          pageSize: rowsPerPage,
          totalCount: 0,
        });
      } finally {
        setLoading(false);
      }
    },
    [
      filters,
      rowsPerPage,
      organization?.organizationId,
      selectedProjects,
      initialProjects.length,
    ]
  );

  const getCleanFilters = (filters: EventFilterParams): Record<string, string | number | number[] | undefined> => {
  const cleaned: Record<string, string | number | number[] | undefined> = {};
  Object.entries(filters).forEach(([key, value]) => {
    if (value !== null && value !== undefined) {
      cleaned[key] = value;
    }
  });
  return cleaned;
};

  // Fetch when selectedProjects changes
  useEffect(() => {
    if (!orgLoaded) {
      return;
    }

    if (!organization?.organizationId) {
      return;
    }

    if (selectedProjects.length === 0) {
      setData([]);
      setPagination({
        pageNumber: 1,
        pageSize: rowsPerPage,
        totalCount: 0,
      });
      return;
    }

    fetchEvents(1, rowsPerPage);
  }, [
    fetchEvents,
    orgLoaded,
    organization?.organizationId,
    rowsPerPage,
    selectedProjects,
  ]);

  const handlePageChange = (pageNumber: number) => {
    fetchEvents(pageNumber, pagination.pageSize);
  };

  const handlePageSizeChange = (pageSize: number) => {
    setRowsPerPage(pageSize);
    fetchEvents(1, pageSize);
  };

  const columns: Column<EventResponseDto>[] = [
    {
      header: "Time Stamp",
      data: "lastUpdatedAt",
      sortable: false,
      cell: (row) => {
        const date = new Date(row.lastUpdatedAt ? row.lastUpdatedAt : "");
        return date.toLocaleString();
      },
    },
    {
      header: "User",
      data: "lastUpdatedByUserName",
      sortable: false,
      cell: (row) => (
        <Link
          href="/member_management"
          className="text-base-content hover:underline"
        >
          {row.lastUpdatedByUserName}
        </Link>
      ),
    },
    {
      header: "Project",
      data: "projectName",
      sortable: false,
      cell: (row) => (
        <Link
          href={`/project/${row.projectId}`}
          className="text-base-content hover:underline"
        >
          {row.projectName}
        </Link>
      ),
    },
    {
      header: "Operation",
      data: "operation",
      sortable: false,
    },
    {
      header: "Entity Type",
      data: "entityType",
      sortable: false,
    },
    {
      header: "Entity Name",
      data: "entityName",
      sortable: false,
      cell: (row) => {
        if (row.entityType === "record") {
          return (
            <Link
              href={`/record?recordId=${row.entityId}&projectId=${row.projectId}`}
              className="text-base-content hover:underline"
            >
              {row.entityName}
            </Link>
          );
        }
        return <span>{row.entityName}</span>;
      },
    },
    {
      header: "Data Source",
      data: "dataSourceName",
      sortable: false,
    },
  ];

  return (
    <div>
      <div className="bg-base-200/50 border-b border-base-300/30 pt-4 px-8">
        <h1 className="text-2xl my-6 font-bold text-info-content">
          Event History
        </h1>
        <ProjectDropdown 
          projects={projects}
          onSelectionChange={setSelectedProjects}
          defaultSelected={
            initialSelectedProjects.length
              ? initialSelectedProjects
              : undefined
          }
        />
      </div>

      {loading && (
        <div className="p-8 bg-base-100 min-h-screen">
          <GenericTableSkeleton
            totalColumns={7}
            totalRows={rowsPerPage}
            title={true}
            searchBar={true}
            filters={true}
            pagination={true}
            bordered={true}
          />
        </div>
      )}
      <div className="flex">
        <div className="flex-1">
          <GenericTable
            columns={columns}
            gridView={true}
            data={data}
            enablePageLengthChange={true}
            enablePagination={true}
            backendPagination={true}
            paginationMetadata={pagination}
            onPageChange={handlePageChange}
            onPageSizeChange={handlePageSizeChange}
            bordered={false}
            searchBar={true}
            filterPlaceholder="Search this page..."
            rowsPerPage={rowsPerPage}
            setRowsPerPage={setRowsPerPage}
            filters={filterConfig}
            filterValues={getCleanFilters(filters)}
            onFilterChange={handleFilterChange}
          />
        </div>
      </div>
    </div>
  );
};

export default EventsHistoryClient;
