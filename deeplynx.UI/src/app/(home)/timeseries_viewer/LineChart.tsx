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

interface EChartsLineChartProps {
    dataZoom?: DataZoomConfig;
    timeseriesData: TimeseriesDataItem[];
    visibleSeries: Record<string, boolean>;
    showMarkPoints?: boolean;
    showMarkLines?: boolean;
}

export default function EChartsLineChart({
    dataZoom = { start: 0, end: 100, show: true },
    timeseriesData,
    visibleSeries,
    showMarkPoints = true,
    showMarkLines = true
}: EChartsLineChartProps) {
    const chartRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (!chartRef.current) return;

        const chart = echarts.init(chartRef.current);
        const xAxisData = timeseriesData.find(item => item.name === 'time_x')?.values || [];

        const colors = ['#5470c6', '#91cc75', '#fac858', '#ee6666', '#73c0de', '#3ba272'];

        // Filter data based on visibility and separate temperature and other data for dual Y-axes
        const series = timeseriesData
            .filter(item => item.name !== 'time_x' && visibleSeries[item.name])
            .map((item, index) => {
                const baseSeries: any = {
                    name: item.name,
                    type: 'line' as const,
                    data: item.values,
                    // Temperature uses yAxis 0, others use yAxis 1
                    yAxisIndex: item.name === 'Temperature (K)' ? 0 : 1,
                    itemStyle: {
                        color: colors[index % colors.length]
                    },
                    lineStyle: {
                        width: 2
                    },
                    areaStyle: item.name === 'Temperature (K)' ? {
                        opacity: 0.1
                    } : undefined,
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

        const option = {
            title: {
                text: 'Temperature & Data Monitoring',
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
                        const unit = param.seriesName === 'Temperature (K)' ? 'K' : '';
                        result += `${param.marker} ${param.seriesName}: <strong>${param.value}${unit}</strong><br/>`;
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
                data: xAxisData,
                axisLabel: {
                    rotate: 45
                }
            },
            yAxis: [
                {
                    type: 'value',
                    name: 'Temperature (K)',
                    position: 'left',
                    axisLabel: {
                        formatter: '{value}K'
                    }
                },
                {
                    type: 'value',
                    name: 'Data Values',
                    position: 'right',
                    axisLabel: {
                        formatter: '{value}'
                    },
                    min: 0,
                    max: 6
                }
            ],
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
    }, [dataZoom, timeseriesData, visibleSeries, showMarkPoints, showMarkLines]);

    return (
        <div ref={chartRef} className="w-full h-[475px]" />
    );
}