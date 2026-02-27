export type TimeseriesPlotData = {
    columns: string[];
    data: (string | number)[][];
}

export type TimeseriesPlotResponse = {
    timeseriesPlotData: TimeseriesPlotData;
}

export type LatestRowResponse = {
    latestRowData: Record<string, string | number>;
}

export type TimeseriesUploadResponse = {
    id: number;
    name: string;
    uri?: string;
    dataSourceId?: number;
    projectId?: number;
    lastUpdatedAt?: string;
    lastUpdatedBy?: string;
}

export type TimeseriesUploadStartResponse = {
    uploadId: string;
}

export type TimeseriesChunkUploadResponse = {
    chunkUploadStatus: string;
}

export type TimeseriesUploadCompleteResponse = {
    timeseriesUploadRecord: TimeseriesUploadResponse;
}