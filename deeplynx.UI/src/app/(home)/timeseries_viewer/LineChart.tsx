import React, { useEffect, useRef } from 'react';
import * as echarts from 'echarts';

export default function EChartsLineChart() {
    const chartRef = useRef(null);

    useEffect(() => {
        if (!chartRef.current) return;

        // Initialize chart
        const chart = echarts.init(chartRef.current);

        // Chart configuration
        const option = {
            title: {
                text: 'Timeseries',
                left: 'center',
                textStyle: {
                    fontSize: 20,
                    fontWeight: 'bold'
                }
            },
            tooltip: {
                trigger: 'axis',
                axisPointer: {
                    type: 'cross'
                }
            },
            legend: {
                data: ['Sensor A', 'Sensor B', 'Sensor C'],
                top: 40
            },
            grid: {
                left: '3%',
                right: '4%',
                bottom: '3%',
                containLabel: true
            },
            xAxis: {
                type: 'category',
                boundaryGap: false,
                data: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
            },
            yAxis: {
                type: 'value',
                name: 'Temp',
                axisLabel: {
                    formatter: '{value}'
                }
            },
            series: [
                {
                    name: 'Sensor A',
                    type: 'line',
                    smooth: true,
                    data: [120, 132, 101, 134, 90, 230, 210, 182, 191, 234, 290, 330],
                    itemStyle: {
                        color: '#5470c6'
                    },
                    areaStyle: {
                        opacity: 0.3
                    }
                },
                {
                    name: 'Sensor B',
                    type: 'line',
                    smooth: true,
                    data: [220, 182, 191, 234, 290, 330, 310, 201, 154, 190, 330, 410],
                    itemStyle: {
                        color: '#91cc75'
                    },
                    areaStyle: {
                        opacity: 0.3
                    }
                },
                {
                    name: 'Sensor C',
                    type: 'line',
                    smooth: true,
                    data: [150, 232, 201, 154, 190, 330, 410, 321, 250, 287, 412, 390],
                    itemStyle: {
                        color: '#fac858'
                    },
                    areaStyle: {
                        opacity: 0.3
                    }
                }
            ]
        };

        // Set chart options
        chart.setOption(option);

        // Handle resize
        const handleResize = () => {
            chart.resize();
        };
        window.addEventListener('resize', handleResize);

        // Cleanup
        return () => {
            window.removeEventListener('resize', handleResize);
            chart.dispose();
        };
    }, []);

    return (
        <div className="w-full h-screen flex items-center justify-center bg-gray-50 p-8">
            <div className="w-full max-w-6xl bg-white rounded-lg shadow-lg p-6">
                <div ref={chartRef} className="w-full h-[600px]" />
            </div>
        </div>
    );
}