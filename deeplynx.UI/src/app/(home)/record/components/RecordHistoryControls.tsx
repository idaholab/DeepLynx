import React from "react";
import { useLanguage } from "@/app/contexts/Language";
import { CompareMode } from "./RecordHistoryDifferenceUtils";

interface Props {
  selectedIndex: number;
  compareMode: CompareMode;
  manualCompareUiIndex: number;
  sliderIndex: number;
  historyLength: number;
  changedCount: number;
  versionOptions: React.ReactNode;
  manualVersionOptions: React.ReactNode;
  onSelectedIndexChange: (nextIndex: number) => void;
  onCompareModeChange: (mode: CompareMode) => void;
  onSliderChange: (nextIndex: number) => void;
  onSliderCommit: () => void;
  onManualCompareUiChange: (nextIndex: number) => void;
  onManualCompareBlur: () => void;
}

export default function RecordHistoryControls({
  selectedIndex,
  compareMode,
  manualCompareUiIndex,
  sliderIndex,
  historyLength,
  changedCount,
  versionOptions,
  manualVersionOptions,
  onSelectedIndexChange,
  onCompareModeChange,
  onSliderChange,
  onSliderCommit,
  onManualCompareUiChange,
  onManualCompareBlur,
}: Props) {
  const { t } = useLanguage();

  return (
    // Version selection and compare mode controls.
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        {/* Top row: selectors, compare mode, and summary counters. */}
        <div className="flex flex-wrap items-end gap-4">
          <div className="form-control min-w-[280px] flex-1">
            <label className="label py-1 mr-2">
              <span className="label-text font-medium">
                {t.translations.RECORD_HISTORY_SELECT_VERSION}
              </span>
            </label>
            <select
              className="select select-bordered"
              value={selectedIndex}
              onChange={(e) => onSelectedIndexChange(Number(e.target.value))}
            >
              {versionOptions}
            </select>
          </div>

          <div className="form-control">
            <label className="label py-1">
              <span className="label-text font-medium mr-2">
                {t.translations.RECORD_HISTORY_COMPARE_AGAINST}
              </span>
            </label>
            <div className="join">
              <button
                type="button"
                className={`btn btn-sm join-item ${compareMode === "previous" ? "btn-primary" : "btn-outline"}`}
                onClick={() => onCompareModeChange("previous")}
              >
                {t.translations.RECORD_HISTORY_PREVIOUS}
              </button>
              <button
                type="button"
                className={`btn btn-sm join-item ${compareMode === "latest" ? "btn-primary" : "btn-outline"}`}
                onClick={() => onCompareModeChange("latest")}
              >
                {t.translations.RECORD_HISTORY_LATEST}
              </button>
              <button
                type="button"
                className={`btn btn-sm join-item ${compareMode === "manual" ? "btn-primary" : "btn-outline"}`}
                onClick={() => onCompareModeChange("manual")}
              >
                {t.translations.RECORD_HISTORY_MANUAL}
              </button>
            </div>
          </div>

          <div className="ml-auto text-right text-sm">
            <p className="font-medium">
              {t.translations.RECORD_HISTORY_VERSIONS}: {historyLength}
            </p>
            <p className="opacity-70">
              {t.translations.RECORD_HISTORY_CHANGES_HIGHLIGHTED}:{" "}
              {changedCount}
            </p>
          </div>
        </div>

        {/* Bottom row: timeline slider and manual compare selector. */}
        <div className="flex justify-between">
          <input
            type="range"
            min={0}
            max={Math.max(historyLength - 1, 0)}
            step={1}
            value={sliderIndex}
            onChange={(e) => onSliderChange(Number(e.target.value))}
            onMouseUp={onSliderCommit}
            onTouchEnd={onSliderCommit}
            onBlur={onSliderCommit}
            className="range range-primary range-sm"
          />

          {compareMode === "manual" && (
            <div className="flex justify-end">
              <div className="form-control flex mr-4">
                <label className="label py-1 mr-4">
                  <span className="label-text font-medium">
                    {t.translations.RECORD_HISTORY_MANUAL_COMPARE_VERSION}
                  </span>
                </label>
                <select
                  className="select select-bordered"
                  value={manualCompareUiIndex}
                  onChange={(e) =>
                    onManualCompareUiChange(Number(e.target.value))
                  }
                  onBlur={onManualCompareBlur}
                >
                  {manualVersionOptions}
                </select>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
