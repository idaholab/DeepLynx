"use client";

import React from "react";
import { useLanguage } from "@/app/contexts/Language";
import { ArrowLeftIcon } from '@heroicons/react/24/outline';
import RoleSettings from "@/app/(home)/components/ProjectSettingsTable/ProjectTables/RoleSettings";
import { useRouter } from "next/navigation";

type Props = {
    projectId: string | string[];
};

export default function RoleSettingsClient({ projectId }: Props) {
    const { t } = useLanguage();
    const router = useRouter();

    const handleReturnToRoles = () => {
        router.push(`/project/${projectId}/project_settings?tab=Roles`);
    };

    return (
        <div>
            <div className="flex justify-between items-center bg-base-200/40 px-3 sm:px-6 lg:px-12 py-2">
                <div>
                    <h1 className="text-xl sm:text-2xl font-bold text-info-content">
                        {t.translations.ROLE_SETTINGS}
                    </h1>
                    <div className="flex justify-start items-center">
                        <button
                            className="flex items-center justify-start space-x-2"
                            onClick={handleReturnToRoles}>
                                <ArrowLeftIcon className="size-4 text-secondary"/>
                            <span>{t.translations.RETURN_TO_ROLES}</span>
                        </button>
                    </div>
                </div>
            </div>

            <div className="flex w-full gap-6 p-3 sm:p-6 lg:p-8">
                <div className="w-full">
                    <div className="bg-base-100 text-accent-content rounded-xl p-0 shadow-md card">
                        <div className="w-full">
                            <RoleSettings
                                id={projectId}
                            />
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};
