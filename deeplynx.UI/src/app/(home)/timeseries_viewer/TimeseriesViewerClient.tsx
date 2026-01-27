'use client';
import React, { useRef, useState } from 'react'
import ProjectDropdown from '../components/ProjectDropdown';
import Tabs from '../components/Tabs';
import { useLanguage } from '@/app/contexts/Language';
import { useOrganizationSession } from '@/app/contexts/OrganizationSessionProvider';
import EChartsLineChart from './LineChart';
import { ArrowDownTrayIcon } from '@heroicons/react/24/outline';
import * as echarts from 'echarts';
import { queryBuilder } from '@/app/lib/client_service/query_services.client';
import { QueryBuilderQuery } from '../types/types';
import { CustomQueryRequestDto } from '../types/requestDTOs';
import { useProjectSession } from '@/app/contexts/ProjectSessionProvider';
import { HistoricalRecordResponseDto } from '../types/responseDTOs';

type Props = {
    timeseriesFiles: HistoricalRecordResponseDto[]
};

export default function TimeseriesViewerClient({ timeseriesFiles }: Props) {
    const { t } = useLanguage();
    const { organization } = useOrganizationSession();
    const { project } = useProjectSession();
    const [activeTab, setActiveTab] = useState("");
    const [files, setFiles] = useState<HistoricalRecordResponseDto[]>(timeseriesFiles);

    // ============================================
    // CHART CONFIGURATION - Replace with backend data
    // ============================================
    const chartTitle = "Temperature & Data Monitoring";
    const xAxisName = "Time Points";

    //Using query builder to grab Timeseries files for now
    const dto: CustomQueryRequestDto = {
        filter: "class_name",
        operator: '=',
        value: "Timeseries"
    }

    const yAxisConfigs = [
        {
            name: 'Temperature (K)',
            position: 'left' as const,
            formatter: '{value}K'
        },
        {
            name: 'Data Values',
            position: 'right' as const,
            formatter: '{value}',
            min: 0,
            max: 6
        }
    ];

    const seriesYAxisMapping: Record<string, number> = {
        'Temperature (K)': 0,
        'Data_1': 1,
        'Data_2': 1,
        'Data_3': 1,
        'Data_4': 1,
        'Data_5': 1
    };

    // CSV data converted to timeseries format
    const timeseriesData = [
        {
            "name": "time_x",
            "values": Array(101).fill(0).map((_, i) => `Point ${i + 1}`)
        },
        {
            "name": "Temperature (K)",
            "values": [
                912.01, 895.58, 914.13, 911.14, 934.53, 899.63, 877.43, 903.92, 879.81, 899.08,
                882.6, 937.65, 931.76, 875.54, 898.76, 967.42, 874.32, 949.69, 901.21, 877.24,
                891.18, 885.95, 950.3, 940, 898.78, 911.82, 877.1, 945.57, 890.16, 942.95,
                972.28, 933.17, 929.19, 955.62, 898.11, 893.58, 932.34, 961.13, 947.5, 873.93,
                922.77, 959.06, 924.29, 892.76, 942.11, 899.31, 935.92, 957.9, 958.69, 901.49,
                954.18, 905.93, 968.97, 939.75, 971.81, 919.62, 939.75, 892.92, 909.82, 879.83,
                970.61, 886.75, 925.72, 920.99, 967.67, 967.62, 956.61, 881.43, 955.91, 882.54,
                886.4, 874.96, 935.11, 897.41, 913.33, 944.22, 928.71, 875.64, 882.9, 961.1,
                917.03, 941.35, 972.27, 961.29, 907.68, 893.73, 900.98, 944.16, 934.28, 957.76,
                898.07, 943.96, 899.57, 920.43, 915.27, 944.75, 917.49, 922.39, 967.65, 931.91, 938.93
            ]
        },
        {
            "name": "Data_1",
            "values": Array(101).fill(1)
        },
        {
            "name": "Data_2",
            "values": Array(101).fill(2)
        },
        {
            "name": "Data_3",
            "values": Array(101).fill(3)
        },
        {
            "name": "Data_4",
            "values": Array(101).fill(4)
        },
        {
            "name": "Data_5",
            "values": Array(101).fill(5)
        }
    ];

    const availableFiles = [
        { name: 'temperature_data_2024_01.csv', date: '2024-01-15', size: '2.4 MB' },
        { name: 'temperature_data_2024_02.csv', date: '2024-02-12', size: '2.1 MB' },
        { name: 'temperature_data_2024_03.csv', date: '2024-03-10', size: '2.6 MB' },
        { name: 'sensor_readings_q1.csv', date: '2024-03-31', size: '5.2 MB' },
        { name: 'sensor_readings_q2.csv', date: '2024-06-30', size: '4.8 MB' },
    ];
    // ============================================
    // END CHART CONFIGURATION
    // ============================================

    // Time range state
    const [startDate, setStartDate] = useState('2024-01-01T00:00');
    const [endDate, setEndDate] = useState('2024-01-07T23:59');
    const [sliderStart, setSliderStart] = useState(0);
    const [sliderEnd, setSliderEnd] = useState(100);
    const [dataZoom, setDataZoom] = useState({ start: 0, end: 100, show: true });

    // Chart settings
    const [showMarkPoints, setShowMarkPoints] = useState(true);
    const [showMarkLines, setShowMarkLines] = useState(true);

    // File selection
    const [selectedFile, setSelectedFile] = useState('temperature_data_2024_01.csv');

    // Series visibility - default all visible
    const [visibleSeries, setVisibleSeries] = useState<Record<string, boolean>>({
        'Temperature (K)': true,
        'Data_1': true,
        'Data_2': true,
        'Data_3': true,
        'Data_4': true,
        'Data_5': true
    });

    // Toggle series visibility
    const toggleSeries = (seriesName: string) => {
        setVisibleSeries(prev => ({
            ...prev,
            [seriesName]: !prev[seriesName]
        }));
    };

    const handleApplyTimeRange = () => {
        setDataZoom({
            start: sliderStart,
            end: sliderEnd,
            show: true
        });
    };

    const handleExport = (format: 'csv' | 'png' | 'pdf') => {
        if (format === 'png') {
            const chartDiv = document.querySelector('.echarts-timeseries-chart') as HTMLDivElement;
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
    const TimeRangeTab = () => (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Available Data Files */}
            <div className="lg:col-span-1">
                <div className="card bg-base-100 shadow-xl">
                    <div className="card-body">
                        <h3 className="font-semibold mb-4">Available Timeseries Files</h3>

                        <div className="space-y-2 max-h-[300px] overflow-y-auto">
                            {timeseriesFiles.map((file, index) => (
                                <div
                                    key={index}
                                    className={`p-3 rounded-lg border cursor-pointer transition-all ${selectedFile === file.name
                                        ? 'border-primary bg-primary/10'
                                        : 'border-base-300 hover:border-primary/50 hover:bg-base-200'
                                        }`}
                                    onClick={() => setSelectedFile(file.name!)}
                                >
                                    <div className="flex items-start justify-between">
                                        <div className="flex-1 min-w-0">
                                            <p className={`text-sm font-medium truncate ${selectedFile === file.name ? 'text-primary' : ''
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
            {/* Custom Range */}
            <div className="lg:col-span-2">
                <div className="card bg-base-100 shadow-xl">
                    <div className="card-body">
                        <h3 className="font-semibold mb-4">Custom Time Range</h3>

                        {/* Date Time Inputs */}
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                            <div className="form-control">
                                <label className="label">
                                    <span className="label-text font-medium">Start Date & Time</span>
                                </label>
                                <input
                                    type="datetime-local"
                                    value={startDate}
                                    onChange={(e) => setStartDate(e.target.value)}
                                    className="input input-bordered w-full"
                                />
                            </div>

                            <div className="form-control">
                                <label className="label">
                                    <span className="label-text font-medium">End Date & Time</span>
                                </label>
                                <input
                                    type="datetime-local"
                                    value={endDate}
                                    onChange={(e) => setEndDate(e.target.value)}
                                    className="input input-bordered w-full"
                                />
                            </div>
                        </div>
                        {/* Action Buttons */}
                        <div className="flex justify-end gap-2">
                            <button
                                onClick={() => {
                                    setSliderStart(0);
                                    setSliderEnd(100);
                                    setStartDate('2024-01-01T00:00');
                                    setEndDate('2024-01-07T23:59');
                                }}
                                className="btn btn-ghost btn-sm"
                            >
                                Reset
                            </button>
                            <button
                                onClick={handleApplyTimeRange}
                                className="btn btn-primary btn-sm"
                            >
                                Apply Selection
                            </button>
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
                    </div>
                </div>
            </div>
        </div>
    )

    const ColumnTab = () => (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <div className="lg:col-span-2">
                <div className="card bg-base-100 shadow-xl">
                    <div className="card-body">
                    </div>
                </div>
            </div>
        </div>
    )

    const AnnotationTab = () => (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <div className="lg:col-span-2">
                <div className="card bg-base-100 shadow-xl">
                    <div className="card-body">
                    </div>
                </div>
            </div>
        </div>
    )

    const tabData = [
        {
            label: "Set Up",
            content: <TimeRangeTab />
        },
        {
            label: "Data Check",
            content: <ColumnTab />
        },
        {
            label: "Data Schema",
            content: <SchemaTab />
        },
        {
            label: "Data Annotation",
            content: <AnnotationTab />
        }
    ];

    return (
        <div>
            {/* Header */}
            <div className="bg-base-200/40 pl-12 p-4">
                <h1 className="text-2xl font-bold text-base-content">
                    {t.translations.TIMESERIES_VIEWER}
                </h1>
                {/* <div className="">
                    <ProjectDropdown
                        projects={projects}
                        onSelectionChange={setSelectedProjects}
                        defaultSelected={
                            initialSelectedProjects.length
                                ? initialSelectedProjects
                                : undefined
                        }
                    />
                </div> */}
            </div>

            <div className="flex justify-end p-4">
                <div className="dropdown dropdown-end">
                    <button tabIndex={0} className="btn btn-primary btn-sm">
                        <ArrowDownTrayIcon className="size-6" />
                    </button>
                    <ul tabIndex={0} className="dropdown-content menu bg-base-100 rounded-box z-[1] w-52 p-2 shadow-lg border border-base-300">
                        {/* <li>
                            <a onClick={() => handleExport('csv')}>
                                <span>Export as CSV</span>
                            </a>
                        </li> */}
                        <li>
                            <a onClick={() => handleExport('png')}>
                                <span>Export as PNG</span>
                            </a>
                        </li>
                    </ul>
                </div>
            </div>

            {/* Chart Viewer with Series Controls */}
            <div className="px-6 pt-6">
                <div className="grid grid-cols-1 lg:grid-cols-4 gap-4">
                    {/* Chart */}
                    <div className="lg:col-span-3">
                        <div className="card bg-base-100 shadow-sm">
                            <div className="card-body p-4">
                                <EChartsLineChart
                                    title={chartTitle}
                                    xAxisName={xAxisName}
                                    yAxisConfigs={yAxisConfigs}
                                    seriesYAxisMapping={seriesYAxisMapping}
                                    dataZoom={dataZoom}
                                    timeseriesData={timeseriesData}
                                    visibleSeries={visibleSeries}
                                    showMarkPoints={showMarkPoints}
                                    showMarkLines={showMarkLines}
                                />
                            </div>
                        </div>
                    </div>

                    {/* Right Sidebar with Controls */}
                    <div className="lg:col-span-1 space-y-4">
                        {/* Series Visibility Controls */}
                        <div className="card bg-base-100 shadow-xl">
                            <div className="card-body p-4">
                                <h3 className="font-semibold text-sm mb-3">Plot Options</h3>
                                <div className="space-y-2">
                                    {Object.entries(visibleSeries).map(([seriesName, isVisible]) => (
                                        <div key={seriesName} className="form-control">
                                            <label className="label cursor-pointer justify-start gap-2 py-1">
                                                <input
                                                    type="checkbox"
                                                    checked={isVisible}
                                                    onChange={() => toggleSeries(seriesName)}
                                                    className="checkbox checkbox-primary checkbox-sm"
                                                />
                                                <span className="label-text text-sm">{seriesName}</span>
                                            </label>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>

                        {/* Chart Settings */}
                        <div className="card bg-base-100 shadow-xl">
                            <div className="card-body p-4">
                                <h3 className="font-semibold text-sm mb-3">Chart Settings</h3>
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
                                                <span className="label-text text-sm font-medium">Zoom controls</span>
                                                <div className="text-xs text-base-content/60">Interactive zoom slider</div>
                                            </div>
                                        </label>
                                    </div>
                                    <div className="form-control">
                                        <label className="label cursor-pointer justify-start gap-2 py-1">
                                            <input
                                                type="checkbox"
                                                checked={showMarkPoints}
                                                onChange={(e) => setShowMarkPoints(e.target.checked)}
                                                className="checkbox checkbox-primary checkbox-sm"
                                            />
                                            <div>
                                                <span className="label-text text-sm font-medium">Min/max markers</span>
                                                <div className="text-xs text-base-content/60">Show value markers</div>
                                            </div>
                                        </label>
                                    </div>
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
                    className="tabs tabs-border ml-5"
                    onTabChange={setActiveTab}
                    activeTab={activeTab}
                />
            </div>
        </div>
    )
}