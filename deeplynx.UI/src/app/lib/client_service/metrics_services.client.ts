"use client";

import api from "./api";

/**
 * Get system storage size in bytes.
 * @returns Promise with the system storage size in bytes
 */
export const getSystemStorageSize = async (): Promise<number> => {
  try {
    const res = await api.get<number>("/metrics/storage/size");
    return res.data;
  } catch (error) {
    console.error("Error fetching system storage size:", error);
    throw error;
  }
};

/**
 * Get system data source count.
 * @returns Promise with the system data source count
 */
export const getSystemDataSourceCount = async (): Promise<number> => {
  try {
    const res = await api.get<number>("/metrics/datasources/count", {
      params: { hideArchived: true },
    });
    return res.data;
  } catch (error) {
    console.error("Error fetching system data source count:", error);
    throw error;
  }
};
