import React, { useEffect, useRef } from 'react';
import * as echarts from 'echarts';

interface DataZoomConfig {
    start?: number;
    end?: number;
    show?: boolean;
}

interface TimeseriesDataItem {
    name: string;
    values: (string | number)[];
}

interface YAxisConfig {
    name: string;
    position: 'left' | 'right';
    formatter?: string;
    min?: number;
    max?: number;
}

interface EChartsLineChartProps {
    title?: string;
    xAxisName?: string;
    yAxisConfigs?: YAxisConfig[];
    dataZoom?: DataZoomConfig;
    timeseriesData: TimeseriesDataItem[];
    visibleSeries: Record<string, boolean>;
    showMarkPoints?: boolean;
    showMarkLines?: boolean;
    seriesYAxisMapping?: Record<string, number>; // Maps series name to yAxis index
}

export default function EChartsLineChart({
    title = 'Chart',
    xAxisName = 'Time',
    yAxisConfigs = [
        { name: 'Value', position: 'left', formatter: '{value}' }
    ],
    dataZoom = { start: 0, end: 100, show: true },
    timeseriesData,
    visibleSeries,
    showMarkPoints = true,
    showMarkLines = true,
    seriesYAxisMapping = {}
}: EChartsLineChartProps) {
    const chartRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (!chartRef.current) return;

        const chart = echarts.init(chartRef.current);
        const xAxisData = timeseriesData.find(item => item.name === 'time_x')?.values || [];

        const colors = ['#5470c6', '#91cc75', '#fac858', '#ee6666', '#73c0de', '#3ba272'];

        const series = timeseriesData
            .filter(item => item.name !== 'time_x' && visibleSeries[item.name])
            .map((item, index) => {
                const baseSeries: any = {
                    name: item.name,
                    type: 'line' as const,
                    data: item.values,
                    // Use mapping if provided, otherwise default to 0
                    yAxisIndex: seriesYAxisMapping[item.name] ?? 0,
                    itemStyle: {
                        color: colors[index % colors.length]
                    },
                    lineStyle: {
                        width: 2
                    },
                    emphasis: {
                        focus: 'series'
                    }
                };

                // Add markPoint if enabled
                if (showMarkPoints) {
                    baseSeries.markPoint = {
                        data: [
                            { type: 'max', name: 'Max' },
                            { type: 'min', name: 'Min' }
                        ]
                    };
                }

                // Add markLine if enabled
                if (showMarkLines) {
                    baseSeries.markLine = {
                        data: [
                            { type: 'average', name: 'Avg' }
                        ]
                    };
                }

                return baseSeries;
            });

        const legendData = series.map((s: any) => s.name);

        // Build yAxis configuration from props
        const yAxisOptions = yAxisConfigs.map(config => ({
            type: 'value' as const,
            name: config.name,
            position: config.position,
            axisLabel: {
                formatter: config.formatter || '{value}'
            },
            ...(config.min !== undefined && { min: config.min }),
            ...(config.max !== undefined && { max: config.max })
        }));

        const option = {
            title: {
                text: title,
                left: 'center',
                textStyle: {
                    fontSize: 18,
                    fontWeight: 'bold'
                }
            },
            tooltip: {
                trigger: 'axis',
                axisPointer: {
                    type: 'cross',
                    label: {
                        backgroundColor: '#6a7985'
                    }
                },
                formatter: function (params: any) {
                    let result = `${params[0].axisValue}<br/>`;
                    params.forEach((param: any) => {
                        result += `${param.marker} ${param.seriesName}: <strong>${param.value}</strong><br/>`;
                    });
                    return result;
                }
            },
            legend: {
                data: legendData,
                top: 35,
                type: 'scroll'
            },
            grid: {
                left: '3%',
                right: '8%',
                top: '20%',
                bottom: dataZoom.show ? '20%' : '10%',
                containLabel: true
            },
            xAxis: {
                type: 'category',
                boundaryGap: false,
                name: xAxisName,
                data: xAxisData,
                axisLabel: {
                    rotate: 45
                }
            },
            yAxis: yAxisOptions,
            dataZoom: dataZoom.show ? [
                {
                    type: 'slider',
                    show: true,
                    xAxisIndex: [0],
                    start: dataZoom.start ?? 0,
                    end: dataZoom.end ?? 100,
                    bottom: '5%',
                    height: 20,
                    handleSize: '100%',
                    textStyle: {
                        color: '#666'
                    }
                },
                {
                    type: 'inside',
                    xAxisIndex: [0],
                    start: dataZoom.start ?? 0,
                    end: dataZoom.end ?? 100
                }
            ] : [],
            series: series
        };

        chart.setOption(option);

        const handleResize = () => {
            chart.resize();
        };
        window.addEventListener('resize', handleResize);

        return () => {
            window.removeEventListener('resize', handleResize);
            chart.dispose();
        };
    }, [dataZoom, timeseriesData, visibleSeries, showMarkPoints, showMarkLines, title, xAxisName, yAxisConfigs, seriesYAxisMapping]);

    return (
        <div ref={chartRef} className="w-full h-[475px] echarts-timeseries-chart" />
    );
}