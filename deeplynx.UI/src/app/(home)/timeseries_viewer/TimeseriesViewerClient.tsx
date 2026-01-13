'use client';
import React, { useRef, useState } from 'react'
import ProjectDropdown from '../components/ProjectDropdown';
import Tabs from '../components/Tabs';
import { useLanguage } from '@/app/contexts/Language';
import { useOrganizationSession } from '@/app/contexts/OrganizationSessionProvider';
import EChartsLineChart from './LineChart';

type Props = {
    initialProjects: { id: string; name: string }[];
    initialSelectedProjects: string[];
};

type TimePreset = {
    label: string;
    start: number;
    end: number;
    icon: string;
};

export default function TimeseriesViewerClient({ initialProjects, initialSelectedProjects }: Props) {
    const { t } = useLanguage();
    const { organization } = useOrganizationSession();

    const initialSelectedProjectsRef = useRef(initialSelectedProjects);
    const [projects] = useState(initialProjects);
    const [selectedProjects, setSelectedProjects] = useState<string[]>(
        initialSelectedProjects
    );
    const [activeTab, setActiveTab] = useState("");

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

    // Time range state
    const [startDate, setStartDate] = useState('2024-01-01T00:00');
    const [endDate, setEndDate] = useState('2024-01-07T23:59');
    const [sliderStart, setSliderStart] = useState(0);
    const [sliderEnd, setSliderEnd] = useState(100);
    const [dataZoom, setDataZoom] = useState({ start: 0, end: 100, show: true });

    // Chart settings
    const [smoothing, setSmoothing] = useState(true);
    const [showGrid, setShowGrid] = useState(true);
    const [showTooltips, setShowTooltips] = useState(true);

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

    // Time presets
    const timePresets: TimePreset[] = [
        { label: 'First 20%', start: 0, end: 20, icon: '◐' },
        { label: 'First Half', start: 0, end: 50, icon: '◐' },
        { label: 'Second Half', start: 50, end: 100, icon: '◑' },
        { label: 'Last 20%', start: 80, end: 100, icon: '◑' },
        { label: 'Middle 50%', start: 25, end: 75, icon: '▣' },
        { label: 'All Data', start: 0, end: 100, icon: '⬜' },
    ];

    const handleSliderChange = (e: React.ChangeEvent<HTMLInputElement>, type: 'start' | 'end') => {
        const value = Number(e.target.value);

        if (type === 'start') {
            setSliderStart(Math.min(value, sliderEnd));
        } else {
            setSliderEnd(Math.max(value, sliderStart));
        }
    };

    const handleApplyTimeRange = () => {
        setDataZoom({
            start: sliderStart,
            end: sliderEnd,
            show: true
        });
    };

    const handlePresetClick = (preset: TimePreset) => {
        setSliderStart(preset.start);
        setSliderEnd(preset.end);
        setDataZoom({
            start: preset.start,
            end: preset.end,
            show: true
        });
    };

    const handleExport = (format: 'csv' | 'png' | 'pdf') => {
        console.log(`Exporting as ${format}`);
        alert(`Export as ${format.toUpperCase()} - Feature coming soon!`);
    };

    // Tab content components
    const TimeRangeTab = () => (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Left Column - Quick Presets */}
            <div className="lg:col-span-1">
                <div className="card bg-base-100 border border-base-300">
                    <div className="card-body">
                        <h3 className="font-semibold mb-3">Quick Select</h3>
                        <div className="space-y-2">
                            {timePresets.map((preset) => (
                                <button
                                    key={preset.label}
                                    onClick={() => handlePresetClick(preset)}
                                    className="btn btn-outline btn-sm w-full justify-start gap-2"
                                >
                                    <span>{preset.icon}</span>
                                    {preset.label}
                                </button>
                            ))}
                        </div>
                    </div>
                </div>
            </div>

            {/* Right Column - Custom Range */}
            <div className="lg:col-span-2">
                <div className="card bg-base-100 border border-base-300">
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

                        <div className="divider my-2">OR</div>

                        {/* Range Slider */}
                        <div className="form-control mb-6">
                            <label className="label">
                                <span className="label-text font-medium">Percentage Range</span>
                                <span className="label-text-alt">{sliderStart}% - {sliderEnd}%</span>
                            </label>
                            <div className="relative h-3 bg-base-200 rounded-full">
                                <div
                                    className="absolute h-3 bg-primary rounded-full transition-all"
                                    style={{
                                        left: `${sliderStart}%`,
                                        width: `${sliderEnd - sliderStart}%`
                                    }}
                                />

                                <input
                                    type="range"
                                    min="0"
                                    max="100"
                                    value={sliderStart}
                                    onChange={(e) => handleSliderChange(e, 'start')}
                                    className="range range-primary absolute w-full opacity-0 pointer-events-auto"
                                />

                                <input
                                    type="range"
                                    min="0"
                                    max="100"
                                    value={sliderEnd}
                                    onChange={(e) => handleSliderChange(e, 'end')}
                                    className="range range-primary absolute w-full opacity-0 pointer-events-auto"
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

    const SettingsTab = () => (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {/* Chart Display Settings */}
            <div className="card bg-base-100 border border-base-300">
                <div className="card-body">
                    <h3 className="font-semibold mb-4">Chart Display</h3>
                    <div className="space-y-4">
                        <div className="form-control">
                            <label className="label cursor-pointer justify-start gap-3">
                                <input
                                    type="checkbox"
                                    checked={smoothing}
                                    onChange={(e) => setSmoothing(e.target.checked)}
                                    className="toggle toggle-primary"
                                />
                                <div>
                                    <span className="label-text font-medium">Line smoothing</span>
                                    <div className="text-xs text-base-content/60">Apply curve smoothing to line charts</div>
                                </div>
                            </label>
                        </div>

                        <div className="divider my-2"></div>

                        <div className="form-control">
                            <label className="label cursor-pointer justify-start gap-3">
                                <input
                                    type="checkbox"
                                    checked={dataZoom.show}
                                    onChange={(e) => setDataZoom({ ...dataZoom, show: e.target.checked })}
                                    className="toggle toggle-primary"
                                />
                                <div>
                                    <span className="label-text font-medium">Zoom controls</span>
                                    <div className="text-xs text-base-content/60">Show interactive zoom slider on chart</div>
                                </div>
                            </label>
                        </div>
                    </div>
                </div>
            </div>

            {/* Series Visibility Settings */}
            <div className="card bg-base-100 border border-base-300">
                <div className="card-body">
                    <h3 className="font-semibold mb-4">Series Visibility</h3>
                    <div className="space-y-3">
                        {Object.entries(visibleSeries).map(([seriesName, isVisible]) => (
                            <div key={seriesName} className="form-control">
                                <label className="label cursor-pointer justify-start gap-3">
                                    <input
                                        type="checkbox"
                                        checked={isVisible}
                                        onChange={() => toggleSeries(seriesName)}
                                        className="checkbox checkbox-primary"
                                    />
                                    <span className="label-text">{seriesName}</span>
                                </label>
                            </div>
                        ))}
                    </div>

                    <div className="divider my-2"></div>

                    <div className="flex gap-2">
                        <button
                            onClick={() => {
                                const allVisible: Record<string, boolean> = {};
                                Object.keys(visibleSeries).forEach(key => {
                                    allVisible[key] = true;
                                });
                                setVisibleSeries(allVisible);
                            }}
                            className="btn btn-sm btn-outline flex-1"
                        >
                            Show All
                        </button>
                        <button
                            onClick={() => {
                                const allHidden: Record<string, boolean> = {};
                                Object.keys(visibleSeries).forEach(key => {
                                    allHidden[key] = false;
                                });
                                setVisibleSeries(allHidden);
                            }}
                            className="btn btn-sm btn-outline flex-1"
                        >
                            Hide All
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );

    const ExportTab = () => (
        <div className="space-y-6">
            {/* Export Options */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="card bg-base-100 border border-base-300 hover:border-primary transition-colors">
                    <div className="card-body">
                        <div className="flex items-center gap-3 mb-3">
                            <div className="text-3xl">📊</div>
                            <h3 className="card-title text-base">CSV Export</h3>
                        </div>
                        <p className="text-sm text-base-content/70 mb-4">
                            Export raw time-series data in CSV format for analysis in Excel or other tools.
                        </p>
                        <ul className="text-xs text-base-content/60 space-y-1 mb-4">
                            <li>✓ Raw data values</li>
                            <li>✓ Timestamps included</li>
                            <li>✓ All sensors exported</li>
                        </ul>
                        <button
                            onClick={() => handleExport('csv')}
                            className="btn btn-primary btn-sm w-full"
                        >
                            Download CSV
                        </button>
                    </div>
                </div>

                <div className="card bg-base-100 border border-base-300 hover:border-primary transition-colors">
                    <div className="card-body">
                        <div className="flex items-center gap-3 mb-3">
                            <div className="text-3xl">🖼️</div>
                            <h3 className="card-title text-base">PNG Image</h3>
                        </div>
                        <p className="text-sm text-base-content/70 mb-4">
                            Save the current chart view as a high-resolution PNG image file.
                        </p>
                        <ul className="text-xs text-base-content/60 space-y-1 mb-4">
                            <li>✓ High resolution (300 DPI)</li>
                            <li>✓ Current zoom level</li>
                            <li>✓ Transparent background</li>
                        </ul>
                        <button
                            onClick={() => handleExport('png')}
                            className="btn btn-primary btn-sm w-full"
                        >
                            Download PNG
                        </button>
                    </div>
                </div>

                <div className="card bg-base-100 border border-base-300 hover:border-primary transition-colors">
                    <div className="card-body">
                        <div className="flex items-center gap-3 mb-3">
                            <div className="text-3xl">📄</div>
                            <h3 className="card-title text-base">PDF Report</h3>
                        </div>
                        <p className="text-sm text-base-content/70 mb-4">
                            Generate a comprehensive PDF report with chart and data summary.
                        </p>
                        <ul className="text-xs text-base-content/60 space-y-1 mb-4">
                            <li>✓ Chart visualization</li>
                            <li>✓ Data summary table</li>
                            <li>✓ Time range metadata</li>
                        </ul>
                        <button
                            onClick={() => handleExport('pdf')}
                            className="btn btn-primary btn-sm w-full"
                        >
                            Generate PDF
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );

    const tabData = [
        {
            label: "Time Range",
            content: <TimeRangeTab />
        },
        {
            label: "Chart Settings",
            content: <SettingsTab />
        },
        {
            label: "Export",
            content: <ExportTab />
        }
    ];

    return (
        <div>
            {/* Header */}
            <div className="bg-base-200/40 pl-12 p-6">
                <h1 className="text-2xl font-bold text-base-content">
                    {t.translations.TIMESERIES_VIEWER}
                </h1>
                <div className="mt-4">
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
            </div>

            {/* Chart Viewer */}
            <div className="px-6 pt-6">
                <div className="card bg-base-100 shadow-sm">
                    <div className="card-body p-4">
                        <EChartsLineChart
                            dataZoom={dataZoom}
                            smoothing={smoothing}
                            timeseriesData={timeseriesData}
                            visibleSeries={visibleSeries}
                        />
                    </div>
                </div>
            </div>

            {/* Controls in Tabs */}
            <div className="p-2">
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