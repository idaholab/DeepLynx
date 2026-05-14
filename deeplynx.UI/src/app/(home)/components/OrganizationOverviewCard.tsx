"use client";

import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  getOrganizationDataModalityCount,
  getOrganizationDataSourceCount,
  getOrganizationFileCount,
  getOrganizationRecordCount,
  getOrganizationStorageSize,
} from "@/app/lib/client_service/organization_services.client";
import {
  CircleStackIcon,
  DocumentIcon,
  FolderIcon,
  ServerStackIcon,
  Squares2X2Icon,
} from "@heroicons/react/24/outline";
import { useEffect, useState } from "react";

const getMetricNumber = (value: unknown): number => {
  if (typeof value === "number") return Number.isFinite(value) ? value : 0;
  if (typeof value === "string") {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }
  if (value && typeof value === "object") {
    const record = value as Record<string, unknown>;
    return getMetricNumber(
      record.value ??
        record.count ??
        record.bytes ??
        record.byteSum ??
        record.size ??
        record.storageSize,
    );
  }

  return 0;
};

const formatBytes = (bytes: unknown): string => {
  const normalizedBytes = getMetricNumber(bytes);
  if (normalizedBytes <= 0) return "0 B";

  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB", "TB"];
  const i = Math.min(
    Math.floor(Math.log(normalizedBytes) / Math.log(k)),
    sizes.length - 1,
  );
  const value = normalizedBytes / Math.pow(k, i);

  return `${Math.floor(value)} ${sizes[i]}`;
};

const OVERVIEW_CARD_CLASS =
  "card bg-base-200/30 border border-base-300/50 shadow-sm hover:shadow-md transition-all";

const OrganizationOverviewCard = () => {
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();
  const [metrics, setMetrics] = useState({
    storageSize: 0,
    dataSources: 0,
    dataModalities: 0,
    records: 0,
    files: 0,
  });

  useEffect(() => {
    if (!organization?.organizationId) {
      setMetrics({
        storageSize: 0,
        dataSources: 0,
        dataModalities: 0,
        records: 0,
        files: 0,
      });
      return;
    }

    let isActive = true;

    const fetchMetrics = async () => {
      const organizationId = organization.organizationId as number;

      const [storageSize, dataSources, dataModalities, records, files] =
        await Promise.allSettled([
          getOrganizationStorageSize(organizationId),
          getOrganizationDataSourceCount(organizationId),
          getOrganizationDataModalityCount(organizationId),
          getOrganizationRecordCount(organizationId),
          getOrganizationFileCount(organizationId),
        ]);

      if (!isActive) return;

      setMetrics({
        storageSize:
          storageSize.status === "fulfilled"
            ? getMetricNumber(storageSize.value)
            : 0,
        dataSources:
          dataSources.status === "fulfilled"
            ? getMetricNumber(dataSources.value)
            : 0,
        dataModalities:
          dataModalities.status === "fulfilled"
            ? getMetricNumber(dataModalities.value)
            : 0,
        records:
          records.status === "fulfilled" ? getMetricNumber(records.value) : 0,
        files: files.status === "fulfilled" ? getMetricNumber(files.value) : 0,
      });

      if (storageSize.status === "rejected") {
        console.error(
          "Failed to fetch organization storage size:",
          storageSize.reason,
        );
      }

      if (dataSources.status === "rejected") {
        console.error(
          "Failed to fetch organization data source count:",
          dataSources.reason,
        );
      }

      if (dataModalities.status === "rejected") {
        console.error(
          "Failed to fetch organization data modality count:",
          dataModalities.reason,
        );
      }

      if (records.status === "rejected") {
        console.error(
          "Failed to fetch organization record count:",
          records.reason,
        );
      }

      if (files.status === "rejected") {
        console.error("Failed to fetch organization file count:", files.reason);
      }
    };

    void fetchMetrics();

    return () => {
      isActive = false;
    };
  }, [organization?.organizationId]);

  const primaryMetrics = [
    {
      title: t.translations.STORAGE_SIZE,
      value: formatBytes(metrics.storageSize),
      Icon: ServerStackIcon,
    },
    {
      title: t.translations.DATA_SOURCES,
      value: metrics.dataSources,
      Icon: FolderIcon,
    },
    {
      title: t.translations.DATA_MODALITIES,
      value: metrics.dataModalities,
      Icon: Squares2X2Icon,
    },
  ];

  const secondaryMetrics = [
    {
      title: t.translations.RECORD_COUNT,
      value: metrics.records,
      Icon: CircleStackIcon,
    },
    {
      title: t.translations.FILE_COUNT,
      value: metrics.files,
      Icon: DocumentIcon,
    },
  ];

  return (
    <div className={OVERVIEW_CARD_CLASS}>
      <div className="card-body">
        <h2 className="text-xl font-semibold text-base-content">
          {t.translations.ORGANIZATION_OVERVIEW}
        </h2>

        <div className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            {primaryMetrics.map(({ title, value, Icon }) => (
              <div key={title}>
                <div className="text-base-content opacity-70 text-sm">
                  {title}
                </div>
                <div className="text-secondary flex items-center text-xl font-bold mt-1">
                  <Icon className="size-7 mr-2 shrink-0" />
                  <div className="text-base-content break-words">{value}</div>
                </div>
              </div>
            ))}
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            {secondaryMetrics.map(({ title, value, Icon }) => (
              <div key={title}>
                <div className="text-base-content opacity-70 text-sm">
                  {title}
                </div>
                <div className="text-secondary flex items-center text-xl font-bold mt-1">
                  <Icon className="size-7 mr-2 shrink-0" />
                  <div className="text-base-content break-words">{value}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};

export default OrganizationOverviewCard;
