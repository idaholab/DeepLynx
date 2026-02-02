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