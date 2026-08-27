import React, { useState, useEffect } from "react";

interface Tab {
  label: string;
  content: React.ReactNode;
  displayLabel?: React.ReactNode;
}

interface TabsProps {
  tabs: Tab[];
  className?: string;
  onTabChange?: (label: string) => void;
  activeTab: string;
  rightAction?: React.ReactNode;
}

const Tabs: React.FC<TabsProps> = ({
  tabs,
  className = "",
  onTabChange,
  activeTab,
  rightAction,
}) => {
  const [activeIndex, setActiveIndex] = useState(0);

  // Use useEffect to update activeIndex based on activeTab prop
  useEffect(() => {
    const index = tabs.findIndex((tab) => tab.label === activeTab);
    setActiveIndex(index !== -1 ? index : 0);
  }, [activeTab, tabs]);

  const handleTabClick = (label: string) => {
    // Don't update activeIndex directly - activeTab prop is the single
    // source of truth. If the parent accepts the change (by updating the
    // activeTab prop it passes in), the useEffect above will sync
    // activeIndex accordingly. If the parent rejects the change (e.g. to
    // block switching while editing), nothing here should switch tabs.
    onTabChange?.(label);
  };

  return (
    <div className={className}>
      {/* Tabs header */}
      <div className="flex items-center border-b border-base-200">
        <div className="tabs tabs-border border-b border-base-200 overflow-x-auto whitespace-nowrap flex flex-1">
          {tabs.map((tab, index) => (
            <a
              key={index}
              className={`tab tab-bordered mr-2 sm:mr-4 ${activeIndex === index ? "tab-active text-secondary" : ""
                }`}
              onClick={() => handleTabClick(tab.label)}
            >
              {tab.displayLabel ?? tab.label}
            </a>
          ))}
        </div>
        {rightAction ? (
          <div className="ml-4 mr-3 sm:mr-6 lg:mr-12 shrink-0">
            {rightAction}
          </div>
        ) : null}
      </div>

      {/* Tab content */}
      <div className="flex justify-center items-start w-full">
        <div className="w-full">{tabs[activeIndex].content}</div>
      </div>
    </div>
  );
};

export default Tabs;
