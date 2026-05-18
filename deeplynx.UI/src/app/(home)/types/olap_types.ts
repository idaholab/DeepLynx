export type OlapPlotData = {
    columns: string[];
    data: (string | number)[][];
}

export type OlapPlotResponse = {
    plotData: OlapPlotData;
}

