'use client';
import React, { useEffect, useState } from 'react'
import Tabs from '../components/Tabs';
import { useLanguage } from '@/app/contexts/Language';
import { useOrganizationSession } from '@/app/contexts/OrganizationSessionProvider';
import EChartsLineChart from './LineChart';
import Heatmap from './HeatMap';
import { ArrowDownTrayIcon } from '@heroicons/react/24/outline';
import * as echarts from 'echarts';
import { useProjectSession } from '@/app/contexts/ProjectSessionProvider';
import { HistoricalRecordResponseDto } from '../types/responseDTOs';
import { getTimeseriesPlotData } from '@/app/lib/client_service/timeseries_services.client';

type Props = {
    timeseriesFiles: HistoricalRecordResponseDto[]
};

export default function TimeseriesViewerClient({ timeseriesFiles }: Props) {
    const { t } = useLanguage();
    const { organization } = useOrganizationSession();
    const { project } = useProjectSession();
    const [activeTab, setActiveTab] = useState("");
    const [activeFile, setActiveFile] = useState<HistoricalRecordResponseDto | null>(null);
    const [loading, setLoading] = useState(false);
    const [visibleSeries, setVisibleSeries] = useState<Record<string, boolean>>({});
    const [schema, setSchema] = useState<string[]>();
    const [limit, setLimit] = useState<number>(20);
    const [rowStride, setRowStride] = useState<number>(4);
    const [selectedXAxis, setSelectedXAxis] = useState<string>("");
    const [selectedYAxes, setSelectedYAxes] = useState<string[]>([]);
    const [chartType, setChartType] = useState<'2d-line' | 'heatmap'>('2d-line');
    const isReadyToLoad = activeFile !== null && selectedXAxis !== "" && selectedYAxes.length > 0;


    const [timeseriesData, setTimeseriesData] = useState<Array<{
        name: string;
        values: (string | number)[];
    }>>([]);

    const loadTimeseriesData = async (datasourceId: number, recordId: number, limitValue: number, rowStrideValue: number) => {
        try {
            setLoading(true);

            const plotData = await getTimeseriesPlotData(
                Number(organization?.organizationId),
                Number(project?.projectId),
                datasourceId,
                recordId,
                limitValue,
                rowStrideValue
            );

            const { columns, data } = plotData;
            setSchema(columns)

            const transformed = columns.map((colName, colIndex) => ({
                name: colName,
                values: data.map(row => row[colIndex])
            }));

            setTimeseriesData(transformed);

            // Set default X axis (first column)
            if (transformed.length > 0) {
                setSelectedXAxis(transformed[0].name);
            }

            // Set default Y axes (all columns except first)
            if (transformed.length > 1) {
                const yAxisOptions = transformed.slice(1).map(s => s.name);
                setSelectedYAxes(yAxisOptions);
            }

            // Set all series as visible
            const newVisibleSeries: Record<string, boolean> = {};
            transformed.slice(1).forEach(series => {
                newVisibleSeries[series.name] = true;
            });
            setVisibleSeries(newVisibleSeries);

        } catch (err) {
            console.error("Error loading timeseries data:", err);
        } finally {
            setLoading(false);
        }
    };

    const filteredTimeseriesData = React.useMemo(() => {
        if (!selectedXAxis || selectedYAxes.length === 0) return [];

        const xAxisData = timeseriesData.find(col => col.name === selectedXAxis);
        const yAxisData = timeseriesData.filter(col => selectedYAxes.includes(col.name));

        if (!xAxisData) return [];
        return [xAxisData, ...yAxisData];
    }, [timeseriesData, selectedXAxis, selectedYAxes]);

    // Load data when activeFile changes
    useEffect(() => {
        if (activeFile?.dataSourceId && activeFile?.id) {
            loadTimeseriesData(activeFile.dataSourceId, activeFile.id, limit, rowStride);
        }
    }, [activeFile, limit, rowStride]);

    // ============================================
    // CHART CONFIGURATION
    // ============================================
    const chartTitle = activeFile?.name || "Chart";
    const xAxisName = selectedXAxis || "X Axis";

    const yAxisConfigs = [
        {
            name: "Y Axis",
            position: 'left' as const,
            formatter: '{value}K'
        }
    ];

    const seriesYAxisMapping: Record<string, number> = {};
    timeseriesData.slice(1).forEach(series => {
        seriesYAxisMapping[series.name] = 0;
    });

    // ============================================
    // CHART TOGGLES
    // ============================================
    const [dataZoom, setDataZoom] = useState({ start: 0, end: 100, show: true });
    const [showMarkPoints, setShowMarkPoints] = useState(true);

    // Also update seriesYAxisMapping dynamically
    useEffect(() => {
        if (timeseriesData.length > 0) {
            const newMapping: Record<string, number> = {};
            timeseriesData.slice(1).forEach(series => {
                newMapping[series.name] = 0;
            });
        }
    }, [timeseriesData]);

    const handleExport = (format: 'csv' | 'png' | 'pdf') => {
        if (format === 'png') {
            const chartDiv = document.querySelector('.echarts-timeseries-chart, .echarts-heatmap-chart') as HTMLDivElement;
            if (chartDiv) {
                const chartInstance = echarts.getInstanceByDom(chartDiv);
                if (chartInstance) {
                    const url = chartInstance.getDataURL({
                        type: 'png',
                        pixelRatio: 2,
                        backgroundColor: '#ffffff'
                    });

                    const link = document.createElement('a');
                    link.download = `timeseries-chart-${new Date().toISOString().slice(0, 10)}.png`;
                    link.href = url;
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                }
            }
        } else if (format === 'csv') {
            const headers = timeseriesData.map(item => item.name).join(',');
            const rows: string[] = [];

            const maxLength = Math.max(...timeseriesData.map(item => item.values.length));

            for (let i = 0; i < maxLength; i++) {
                const row = timeseriesData.map(item => item.values[i] ?? '').join(',');
                rows.push(row);
            }

            const csvContent = [headers, ...rows].join('\n');
            const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
            const url = URL.createObjectURL(blob);

            const link = document.createElement('a');
            link.download = `timeseries-data-${new Date().toISOString().slice(0, 10)}.csv`;
            link.href = url;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        } else if (format === 'pdf') {
            alert('PDF export requires additional library (jsPDF). Feature coming soon!');
        }
    };

    // Tab content components
    const SetUpTab = () => (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Available Data Files - Row 1, Col 1 */}
            <div className="lg:col-span-1">
                <div className="card bg-base-100 shadow-xl">
                    <div className="card-body">
                        <h3 className="font-semibold mb-4">{t.translations.AVAILABLE_TIMESERIES_FILES}</h3>
                        <div className="space-y-2 max-h-[300px] overflow-y-auto">
                            {timeseriesFiles.map((file, index) => (
                                <div
                                    key={index}
                                    className={`p-3 rounded-lg border cursor-pointer transition-all ${activeFile?.id === file.id
                                        ? 'border-primary bg-primary/10'
                                        : 'border-base-300 hover:border-primary/50 hover:bg-base-200'
                                        }`}
                                    onClick={() => setActiveFile(file)}
                                >
                                    <div className="flex items-start justify-between">
                                        <div className="flex-1 min-w-0">
                                            <p className={`text-sm font-medium truncate ${activeFile?.id === file.id ? 'text-primary' : ''
                                                }`}>
                                                {file.name}
                                            </p>
                                            <div className="flex items-center gap-3 mt-1">
                                                <span className="text-xs text-base-content/60">
                                                    {file.lastUpdatedAt}
                                                </span>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>
            </div>

            {/* Chart Type Selector - Row 2, Col 1 */}
            <div className="lg:col-span-1">
                <div className="card bg-base-100 shadow-xl">
                    <div className="card-body">
                        <h3 className="font-semibold mb-4">{t.translations.CHART_TYPE}</h3>
                        <div className="form-control">
                            <label className="label cursor-pointer">
                                <span className="label-text">{t.translations.LINE_CHART_2D}</span>
                                <input
                                    type="radio"
                                    name="chartType"
                                    className="radio radio-primary"
                                    checked={chartType === '2d-line'}
                                    onChange={() => setChartType('2d-line')}
                                />
                            </label>
                        </div>
                        <div className="form-control">
                            <label className="label cursor-pointer">
                                <span className="label-text">{t.translations.HEATMAP}</span>
                                <input
                                    type="radio"
                                    name="chartType"
                                    className="radio radio-primary"
                                    checked={chartType === 'heatmap'}
                                    onChange={() => setChartType('heatmap')}
                                />
                            </label>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );

    const SchemaTab = () => (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <div className="lg:col-span-2">
                <div className="card bg-base-100 shadow-xl">
                    <div className="card-body">
                        <pre className="text-sm overflow-auto">
                            {JSON.stringify(schema, null, 2)}
                        </pre>
                    </div>
                </div>
            </div>
        </div>
    )

    const ColumnTab = () => (
        <div className="grid grid-cols-1 gap-6">
            <div className="card bg-base-100 shadow-xl">
                <div className="card-body">
                    <div className="overflow-x-auto max-h-[500px]">
                        <table className="table table-zebra table-pin-rows table-sm">
                            <thead>
                                <tr>
                                    {timeseriesData.map((column, index) => (
                                        <th key={index} className="text-left">
                                            {column.name}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {timeseriesData[0]?.values.map((_, rowIndex) => (
                                    <tr key={rowIndex}>
                                        {timeseriesData.map((column, colIndex) => (
                                            <td key={colIndex}>
                                                {column.values[rowIndex]}
                                            </td>
                                        ))}
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        </div>
    )

    const tabData = [
        {
            label: t.translations.SET_UP,
            content: <SetUpTab />
        },
        {
            label: t.translations.DATA_CHECK,
            content: <ColumnTab />
        },
        {
            label: t.translations.DATA_SCHEMA,
            content: <SchemaTab />
        }
    ];

    return (
        <div>
            {/* Header */}
            <div className="bg-base-200/40 px-3 sm:px-6 lg:px-12 p-4">
                <h1 className="text-xl sm:text-2xl font-bold text-base-content">
                    {t.translations.TIMESERIES_VIEWER}
                </h1>
            </div>

            <div className="flex justify-end p-4">
                <div className="dropdown dropdown-end">
                    <button tabIndex={0} className="btn btn-primary btn-sm">
                        <ArrowDownTrayIcon className="size-6" />
                    </button>
                    <ul tabIndex={0} className="dropdown-content menu bg-base-100 rounded-box z-[1] w-52 p-2 shadow-lg border border-base-300">
                        <li>
                            <a onClick={() => handleExport('png')}>
                                <span>{t.translations.EXPORT_AS_PNG}</span>
                            </a>
                        </li>
                    </ul>
                </div>
            </div>

            {/* Chart Viewer with Series Controls */}
            <div className="px-3 sm:px-6 pt-6">
                <div className="grid grid-cols-1 lg:grid-cols-4 gap-4">
                    {/* Chart */}
                    <div className="lg:col-span-3">
                        <div className="card bg-base-100 shadow-sm">
                            <div className="card-body p-4">
                                {!activeFile ? (
                                    <div className="flex items-center justify-center min-h-96">
                                        <div className="text-center">
                                            <h3 className="text-xl font-semibold mb-2">{t.translations.NO_FILE_SELECTED}</h3>

                                            <p className="text-base-content/60">{t.translations.SELECT_FILE_TO_BEGIN}</p>

                                        </div>
                                    </div>
                                ) : loading ? (
                                    <div className="flex items-center justify-center h-96">
                                        <span className="loading loading-spinner loading-lg"></span>
                                    </div>
                                ) : !selectedXAxis || selectedYAxes.length === 0 ? (
                                    <div className="flex items-center justify-center min-h-96">
                                        <div className="text-center">
                                            <h3 className="text-xl font-semibold mb-2">{t.translations.CONFIGURE_AXES}</h3>

                                            <p className="text-base-content/60">
                                                {!selectedXAxis ? t.translations.SELECT_X_AXIS_COLUMN : t.translations.SELECT_Y_AXIS_COLUMN}
                                            </p>
                                        </div>
                                    </div>
                                ) : chartType === '2d-line' ? (
                                    <EChartsLineChart
                                        title={chartTitle}
                                        xAxisName={xAxisName}
                                        yAxisConfigs={yAxisConfigs}
                                        seriesYAxisMapping={seriesYAxisMapping}
                                        dataZoom={dataZoom}
                                        timeseriesData={filteredTimeseriesData}
                                        visibleSeries={visibleSeries}
                                        showMarkPoints={showMarkPoints}
                                        showMarkLines={false}
                                    />
                                ) : (
                                    <Heatmap
                                        title={chartTitle}
                                        xAxisName={xAxisName}
                                        yAxisName="Series"
                                        timeseriesData={filteredTimeseriesData}
                                    />
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Right Sidebar with Controls */}
                    <div className="lg:col-span-1 space-y-4">
                        <div className="card bg-base-100 shadow-xl">
                            <div className="card-body p-4">
                                <div className="flex items-center justify-between mb-3">
                                    <h3 className="font-semibold text-sm">{t.translations.PLOT_OPTIONS}</h3>
                                    {isReadyToLoad ? (
                                        <div className="badge badge-success badge-sm">{t.translations.READY}</div>
                                    ) : (
                                        <div className="badge badge-warning badge-sm">{t.translations.CONFIGURE}</div>
                                    )}
                                </div>

                                {/* Limit Control */}
                                <div className="mb-4">
                                    <label className="label">
                                        <span className="label-text font-medium">{t.translations.LIMIT}: {limit}</span>
                                        <span className="label-text-alt">{t.translations.MAX_DATA_POINTS}</span>
                                    </label>
                                    <input
                                        type="range"
                                        min={10}
                                        max={100}
                                        value={limit}
                                        className="range range-sm range-primary"
                                        step={10}
                                        onChange={(e) => setLimit(Number(e.target.value))}
                                    />
                                    <div className="flex justify-between px-2 mt-1 text-xs">
                                        <span>10</span>
                                        <span>25</span>
                                        <span>50</span>
                                        <span>75</span>
                                        <span>100</span>
                                    </div>
                                </div>

                                {/* Row Stride Control */}
                                <div className="mb-4">
                                    <label className="label">
                                        <span className="label-text font-medium">{t.translations.ROW_STRIDE}: {rowStride}</span>
                                        <span className="label-text-alt">{t.translations.EVERY_NTH_ROW}</span>
                                    </label>
                                    <input
                                        type="range"
                                        min={1}
                                        max={10}
                                        value={rowStride}
                                        className="range range-sm range-primary"
                                        step={1}
                                        onChange={(e) => setRowStride(Number(e.target.value))}
                                    />
                                    <div className="flex justify-between px-2 mt-1 text-xs">
                                        <span>1</span>
                                        <span>3</span>
                                        <span>5</span>
                                        <span>7</span>
                                        <span>10</span>
                                    </div>
                                </div>

                                <div className="divider my-2">{t.translations.AXIS_SELECTION}</div>

                                {/* X Axis Selection */}
                                <div className="mb-4">
                                    <label className="label">
                                        <span className="label-text font-medium">{t.translations.X_AXIS}</span>
                                    </label>
                                    <select
                                        className="select select-bordered select-sm w-full"
                                        value={selectedXAxis}
                                        onChange={(e) => setSelectedXAxis(e.target.value)}
                                        disabled={timeseriesData.length === 0}
                                    >
                                        <option value="">{t.translations.SELECT_X_AXIS}</option>

                                        {timeseriesData.map((col) => (
                                            <option key={col.name} value={col.name}>
                                                {col.name}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                {/* Y Axes Selection - Same for both chart types */}
                                <div className="mb-4">
                                    <label className="label">
                                        <span className="label-text font-medium">{t.translations.Y_AXES_MULTIPLE}</span>

                                    </label>
                                    <div className="space-y-1 max-h-[200px] overflow-y-auto border border-base-300 rounded-lg p-2">
                                        {timeseriesData.length === 0 ? (
                                            <p className="text-xs text-base-content/60 text-center py-2">
                                                {t.translations.NO_COLUMNS_AVAILABLE}
                                            </p>
                                        ) : (
                                            timeseriesData
                                                .filter(col => col.name !== selectedXAxis)
                                                .map((col) => (
                                                    <div key={col.name} className="form-control">
                                                        <label className="label cursor-pointer justify-start gap-2 py-1">
                                                            <input
                                                                type="checkbox"
                                                                checked={selectedYAxes.includes(col.name)}
                                                                onChange={(e) => {
                                                                    if (e.target.checked) {
                                                                        setSelectedYAxes([...selectedYAxes, col.name]);
                                                                    } else {
                                                                        setSelectedYAxes(selectedYAxes.filter(name => name !== col.name));
                                                                    }
                                                                }}
                                                                className="checkbox checkbox-primary checkbox-sm"
                                                            />
                                                            <span className="label-text text-sm">{col.name}</span>
                                                        </label>
                                                    </div>
                                                ))
                                        )}
                                    </div>
                                </div>

                                {/* Quick actions */}
                                {timeseriesData.length > 0 && (
                                    <div className="flex gap-2">
                                        <button
                                            className="btn btn-xs btn-ghost"
                                            onClick={() => {
                                                const allY = timeseriesData
                                                    .filter(col => col.name !== selectedXAxis)
                                                    .map(col => col.name);
                                                setSelectedYAxes(allY);
                                            }}
                                        >
                                            {t.translations.SELECT_ALL}
                                        </button>
                                        <button
                                            className="btn btn-xs btn-ghost"
                                            onClick={() => setSelectedYAxes([])}
                                        >
                                            {t.translations.CLEAR_ALL}
                                        </button>
                                    </div>
                                )}
                            </div>
                        </div>

                        {/* Chart Settings */}
                        <div className="card bg-base-100 shadow-xl">
                            <div className="card-body p-4">
                                <h3 className="font-semibold text-sm mb-3">{t.translations.CHART_SETTINGS}</h3>
                                <div className="space-y-3">
                                    <div className="form-control">
                                        <label className="label cursor-pointer justify-start gap-2 py-1">
                                            <input
                                                type="checkbox"
                                                checked={dataZoom.show}
                                                onChange={(e) => setDataZoom({ ...dataZoom, show: e.target.checked })}
                                                className="checkbox checkbox-primary checkbox-sm"
                                            />
                                            <div>
                                                <span className="label-text text-sm font-medium">{t.translations.ZOOM_CONTROLS}</span>
                                                <div className="text-xs text-base-content/60">{t.translations.INTERACTIVE_ZOOM_SLIDER}</div>
                                            </div>
                                        </label>
                                    </div>
                                    {chartType === '2d-line' && (
                                        <div className="form-control">
                                            <label className="label cursor-pointer justify-start gap-2 py-1">
                                                <input
                                                    type="checkbox"
                                                    checked={showMarkPoints}
                                                    onChange={(e) => setShowMarkPoints(e.target.checked)}
                                                    className="checkbox checkbox-primary checkbox-sm"
                                                />
                                                <div>
                                                    <span className="label-text text-sm font-medium">{t.translations.MIN_MAX_MARKERS}</span>
                                                    <div className="text-xs text-base-content/60">{t.translations.SHOW_VALUE_MARKERS}</div>
                                                </div>
                                            </label>
                                        </div>
                                    )}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            {/* Controls in Tabs */}
            <div className="p-4">
                <Tabs
                    tabs={tabData}
                    className="mx-1 sm:mx-3"
                    onTabChange={setActiveTab}
                    activeTab={activeTab}
                />
            </div>
        </div>
    )
}
