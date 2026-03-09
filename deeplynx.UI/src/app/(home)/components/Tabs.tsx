import React, { useState, useEffect } from "react";

interface Tab {
  label: string;
  content: React.ReactNode;
}

interface TabsProps {
  tabs: Tab[];
  className?: string;
  onTabChange?: (label: string) => void;
  activeTab: string;
}

const Tabs: React.FC<TabsProps> = ({
  tabs,
  className = "",
  onTabChange,
  activeTab,
}) => {
  const [activeIndex, setActiveIndex] = useState(0);

  // Use useEffect to update activeIndex based on activeTab prop
  useEffect(() => {
    const index = tabs.findIndex((tab) => tab.label === activeTab);
    setActiveIndex(index !== -1 ? index : 0);
  }, [activeTab, tabs]);

  const handleTabClick = (index: number, label: string) => {
    setActiveIndex(index);
    if (onTabChange) {
      onTabChange(label);
    }
  };

  return (
    <div className={className}>
      {/* Tabs header */}
      <div className="tabs tabs-border border-b border-base-200 overflow-x-auto whitespace-nowrap">
        {tabs.map((tab, index) => (
          <a
            key={index}
            className={`tab tab-bordered mr-2 sm:mr-4 ${
              activeIndex === index ? "tab-active text-secondary" : ""
            }`}
            onClick={() => handleTabClick(index, tab.label)}
          >
            {tab.label}
          </a>
        ))}
      </div>

      {/* Tab content */}
      <div className="flex justify-center items-start w-full">
        <div className="w-full">{tabs[activeIndex].content}</div>
      </div>
    </div>
  );
};

export default Tabs;
