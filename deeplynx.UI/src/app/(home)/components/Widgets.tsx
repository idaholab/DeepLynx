"use client";

import CreateWidget from "@/app/(home)/components/CreateWidgetsModal";
import { useLanguage } from "@/app/contexts/Language";
import {
  Cog6ToothIcon,
  DocumentCheckIcon,
  PlusIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import { Reorder } from "framer-motion";
import React, { useEffect, useState } from "react";
import GraphWidget from "./WidgetCards/GraphWidget";
import LinksWidget from "./WidgetCards/LinksWidget";
import ProjectOverviewWidget from "./WidgetCards/ProjectOverview";
import TeamMembersWidget from "./WidgetCards/TeamMembers";
import { WidgetType } from "../types/types";


interface WidgetCardProps {
  widgets: WidgetType[];
  onSave: (widgets: WidgetType[]) => void;
  onCustomizeChange: (canCustomize: boolean) => void;
}

const WidgetCard: React.FC<WidgetCardProps> = ({
  widgets,
  onSave,
  onCustomizeChange,
}) => {
  const [currentWidgets, setCurrentWidgets] = useState<WidgetType[]>(widgets);
  const [initialWidgets, setInitialWidgets] = useState<WidgetType[]>(widgets);
  const [canCustomize, setCanCustomize] = useState<boolean>(false);
  const [widgetModal, setWidgetModal] = useState<boolean>(false);
  const { t } = useLanguage();

  useEffect(() => {
    if (canCustomize) {
      setInitialWidgets(currentWidgets);
    }
    onCustomizeChange(canCustomize);
  }, [canCustomize, currentWidgets, onCustomizeChange]);

  const renderWidgets = (widget: WidgetType) => {
    switch (widget) {
      // case "DataOverview":
      //   return <DataOverviewWidget />;

      case "Links":
        return <LinksWidget />;

      case "Graph":
        return <GraphWidget />;

      // case "RecentActivity":
      //   return <RecentActivityWidget />;

      case "ProjectOverview":
        return <ProjectOverviewWidget />;

      case "TeamMembers":
        return <TeamMembersWidget />;

      default:
        return null;
    }
  };

  const handleReorder = (newOrder: WidgetType[]) => {
    if (canCustomize) {
      setCurrentWidgets(newOrder);
    }
  };

  const handleCancel = () => {
    setCurrentWidgets(initialWidgets);
    setCanCustomize(false);
  };

  const handleSave = () => {
    onSave(currentWidgets);
    setCanCustomize(false);
  };

  return (
    <div>
      {/* Action Buttons Bar */}
      <div className="flex justify-end items-center mb-4 gap-2">
        {canCustomize && (
          <>
            <button
              onClick={handleCancel}
              className="btn btn-sm btn-outline btn-neutral"
            >
              <XMarkIcon className="h-5 w-5" />
              <span>{t.translations.CANCEL}</span>
            </button>
            <button onClick={handleSave} className="btn btn-sm btn-primary">
              <DocumentCheckIcon className="h-5 w-5" />
              <span>{t.translations.SAVE}</span>
            </button>
          </>
        )}
      </div>

      <Reorder.Group
        axis="y"
        onReorder={handleReorder}
        values={currentWidgets}
        className="space-y-4"
      >
        {currentWidgets.map((widget) => (
          <Reorder.Item
            key={widget}
            value={widget}
            className={`
              card 
              bg-base-200/30 
              border 
              border-base-300/50 
              shadow-sm 
              hover:shadow-md 
              transition-all
              ${canCustomize
                ? "cursor-grab active:cursor-grabbing hover:bg-base-200/50"
                : "cursor-default"
              }
            `}
            style={{ pointerEvents: canCustomize ? "auto" : "none" }}
          >
            {renderWidgets(widget)}
          </Reorder.Item>
        ))}
      </Reorder.Group>

      <CreateWidget
        isOpen={widgetModal}
        onClose={() => setWidgetModal(false)}
      />
    </div>
  );
};

export default WidgetCard;
