// app/(home)/(routes)/timeseries_viewer/EChartsHeatmap.tsx
'use client';
import React, { useEffect, useRef } from 'react';
import * as echarts from 'echarts';

interface TimeseriesDataItem {
    name: string;
    values: (string | number)[];
}

interface EChartsHeatmapProps {
    title?: string;
    xAxisName?: string;
    yAxisName?: string;
    timeseriesData: TimeseriesDataItem[];
}

interface TooltipParams {
    value: [number, number, number];
}

export default function Heatmap({
    title = 'Heatmap',
    xAxisName = 'X Axis',
    yAxisName = 'Y Axis',
    timeseriesData
}: EChartsHeatmapProps) {
    const chartRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (!chartRef.current || timeseriesData.length < 3) return;

        const chart = echarts.init(chartRef.current);

        // X axis: first column (timestamps/categories)
        const xAxisData = timeseriesData[0]?.values || [];

        // Y axis: series names (all columns except first)
        const yAxisData = timeseriesData.slice(1).map(col => col.name);

        // Transform data into heatmap format: [xIndex, yIndex, value]
        const heatmapData: [number, number, number][] = [];

        xAxisData.forEach((xVal, xIndex) => {
            timeseriesData.slice(1).forEach((series, yIndex) => {
                const value = Number(series.values[xIndex]) || 0;
                heatmapData.push([xIndex, yIndex, value]);
            });
        });

        // Find min and max values for color scale
        const allValues = heatmapData.map(item => item[2]);
        const minValue = Math.min(...allValues);
        const maxValue = Math.max(...allValues);

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
                position: 'top',
                formatter: function (params: TooltipParams) {
                    const xLabel = xAxisData[params.value[0]];
                    const yLabel = yAxisData[params.value[1]];
                    const value = params.value[2];
                    return `${xLabel}<br/>${yLabel}: <strong>${value.toFixed(2)}</strong>`;
                }
            },
            grid: {
                left: '10%',
                right: '10%',
                top: '15%',
                bottom: '15%',
                containLabel: true
            },
            xAxis: {
                type: 'category',
                data: xAxisData,
                name: xAxisName,
                nameLocation: 'middle',
                nameGap: 35,
                splitArea: {
                    show: true
                },
                axisLabel: {
                    rotate: 45,
                    interval: Math.floor(xAxisData.length / 10) || 0 // Show ~10 labels max
                }
            },
            yAxis: {
                type: 'category',
                data: yAxisData,
                name: yAxisName,
                nameLocation: 'middle',
                nameGap: 50,
                splitArea: {
                    show: true
                }
            },
            visualMap: {
                min: minValue,
                max: maxValue,
                calculable: true,
                orient: 'horizontal',
                left: 'center',
                bottom: '0%',
                inRange: {
                    color: [
                        '#313695',
                        '#4575b4',
                        '#74add1',
                        '#abd9e9',
                        '#e0f3f8',
                        '#ffffbf',
                        '#fee090',
                        '#fdae61',
                        '#f46d43',
                        '#d73027',
                        '#a50026'
                    ]
                }
            },
            series: [
                {
                    name: 'Value',
                    type: 'heatmap',
                    data: heatmapData,
                    label: {
                        show: false
                    },
                    emphasis: {
                        itemStyle: {
                            shadowBlur: 10,
                            shadowColor: 'rgba(0, 0, 0, 0.5)'
                        }
                    }
                }
            ]
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
    }, [timeseriesData, title, xAxisName, yAxisName]);

    return (
        <div ref={chartRef} className="w-full h-[650px] echarts-heatmap-chart" />
    );
}