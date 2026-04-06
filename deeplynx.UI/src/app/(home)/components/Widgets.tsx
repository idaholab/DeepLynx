"use client";

import React from "react";
import { WidgetType } from "../types/types";
import ProjectOverviewWidget from "./WidgetCards/ProjectOverview";
import TeamMembersWidget from "./WidgetCards/TeamMembers";

interface WidgetCardProps {
  widgets: WidgetType[];
}

const WIDGET_COMPONENTS: Record<WidgetType, React.ComponentType> = {
  ProjectOverview: ProjectOverviewWidget,
  TeamMembers: TeamMembersWidget,
};

const BASE_WIDGET_CARD_CLASS =
  "card bg-base-200/30 border border-base-300/50 shadow-sm hover:shadow-md transition-all";

const WidgetCard: React.FC<WidgetCardProps> = ({ widgets }) => {
  return (
    <div className="space-y-4">
      {/* Render the supported project widgets in the order supplied by the parent. */}
      {widgets.map((widget) => {
        const WidgetComponent = WIDGET_COMPONENTS[widget];

        return (
          <div key={widget} className={BASE_WIDGET_CARD_CLASS}>
            <WidgetComponent />
          </div>
        );
      })}
    </div>
  );
};

export default WidgetCard;
