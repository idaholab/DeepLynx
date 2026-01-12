'use client';
import React, { useRef, useState } from 'react'
import ProjectDropdown from '../components/ProjectDropdown';
import { useLanguage } from '@/app/contexts/Language';
import { useOrganizationSession } from '@/app/contexts/OrganizationSessionProvider';
import EChartsLineChart from './LineChart';

type Props = {
    initialProjects: { id: string; name: string }[];
    initialSelectedProjects: string[];

};

export default function TimeseriesViewerClient({ initialProjects, initialSelectedProjects }: Props) {
    const { t } = useLanguage();
    const { organization } = useOrganizationSession();

    // Use ref to store initial values to prevent re-renders
    const initialSelectedProjectsRef = useRef(initialSelectedProjects);
    const [projects] = useState(initialProjects);
    const [selectedProjects, setSelectedProjects] = useState<string[]>(
        initialSelectedProjects
    );
    return (
        <div>
            {/* Header */}
            <div className="flex justify-between items-center bg-base-200/40 pl-12 py-4">
                <div>
                    <h1 className="text-2xl font-bold text-info-content">
                        {t.translations.TIMESERIES_VIEWER}
                    </h1>
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
            {/* Viewer*/}
            <div>
                <EChartsLineChart />
            </div>
        </div>
    )
}

