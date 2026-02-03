// src/app/(home)/record/components/AdditionalPropertiesEditor.tsx

"use client";

import { useLanguage } from "@/app/contexts/Language";
import {
  XMarkIcon,
  ChevronDownIcon,
  ChevronRightIcon,
  TrashIcon,
  PlusIcon,
} from "@heroicons/react/24/outline";
import React, { useState, useEffect } from "react";

interface EditablePropertyRowProps {
  path: string;
  value: any;
  onChange: (path: string, value: any) => void;
  onDelete: (path: string) => void;
  depth?: number;
}

function EditablePropertyRow({
  path,
  value,
  onChange,
  onDelete,
  depth = 0,
}: EditablePropertyRowProps) {
  const { t } = useLanguage();
  const [isExpanded, setIsExpanded] = useState(depth < 2);
  const [jsonError, setJsonError] = useState<string | null>(null);
  const key = path.split(".").pop() || "";
  const isObject =
    typeof value === "object" && value !== null && !Array.isArray(value);
  const isArray = Array.isArray(value);

  const handleValueChange = (newValue: string) => {
    onChange(path, newValue);
  };

  const renderInput = () => {
    if (isArray) {
      return (
        <div className="flex-1">
          <input
            type="text"
            value={JSON.stringify(value)}
            onChange={(e) => {
              try {
                const parsed = JSON.parse(e.target.value);
                onChange(path, parsed);
                setJsonError(null);
              } catch (error) {
                setJsonError(t.translations.INVALID_JSON_ARRAY_SYNTAX);
              }
            }}
            className={`input input-sm input-bordered w-full font-mono text-xs ${jsonError ? "input-error" : ""}`}
            placeholder={t.translations.ARRAY_VALUE_EXAMPLE}
          />
          {jsonError && (
            <label className="label py-1">
              <span className="label-text-alt text-error text-xs">
                {jsonError}
              </span>
            </label>
          )}
        </div>
      );
    }

    return (
      <input
        type="text"
        value={value ?? ""}
        onChange={(e) => handleValueChange(e.target.value)}
        className="input input-sm input-bordered w-full"
      />
    );
  };

  return (
    <div className="border-b border-base-300 last:border-b-0">
      <div className="flex items-center gap-2 p-2 hover:bg-base-200 transition-colors">
        {/* Indentation */}
        <div style={{ width: `${depth * 20}px` }} />

        {/* Expand/Collapse for nested objects */}
        {isObject && (
          <button
            type="button"
            onClick={() => setIsExpanded(!isExpanded)}
            className="btn btn-xs btn-ghost btn-circle"
          >
            {isExpanded ? (
              <ChevronDownIcon className="w-4 h-4" />
            ) : (
              <ChevronRightIcon className="w-4 h-4" />
            )}
          </button>
        )}
        {!isObject && <div className="w-8" />}

        {/* Key */}
        <div className="font-medium text-sm min-w-32 flex-shrink-0">
          {key.replace(/_/g, " ").replace(/\b\w/g, (l) => l.toUpperCase())}
        </div>

        {/* Value editor */}
        {!isObject && <div className="flex-1">{renderInput()}</div>}

        {/* Type indicator for objects */}
        {isObject && (
          <div className="flex-1 text-sm text-base-content/60 italic">
            {Object.keys(value).length} {t.translations.PROPERTIES_LABEL}
          </div>
        )}

        {/* Delete button */}
        <button
          type="button"
          onClick={() => onDelete(path)}
          className="btn btn-xs btn-ghost btn-circle text-error hover:bg-error/10"
        >
          <TrashIcon className="w-4 h-4" />
        </button>
      </div>

      {/* Nested properties */}
      {isObject && isExpanded && (
        <div className="bg-base-100">
          {Object.entries(value).map(([nestedKey, nestedValue]) => (
            <EditablePropertyRow
              key={`${path}.${nestedKey}`}
              path={`${path}.${nestedKey}`}
              value={nestedValue}
              onChange={onChange}
              onDelete={onDelete}
              depth={depth + 1}
            />
          ))}
        </div>
      )}
    </div>
  );
}

interface AdditionalPropertiesEditorProps {
  isOpen: boolean;
  onClose: () => void;
  properties: any;
  onSave: (properties: any) => Promise<void>;
  isSaving?: boolean;
}

export default function AdditionalPropertiesEditor({
  isOpen,
  onClose,
  properties,
  onSave,
  isSaving = false,
}: AdditionalPropertiesEditorProps) {
  const { t } = useLanguage();
  const [editedProperties, setEditedProperties] = useState(properties);
  const [newKey, setNewKey] = useState("");
  const [newValue, setNewValue] = useState("");

  useEffect(() => {
    setEditedProperties(properties);
  }, [properties]);

  if (!isOpen) return null;

  const handleChange = (path: string, value: any) => {
    const keys = path.split(".");
    const newProps = JSON.parse(JSON.stringify(editedProperties));

    let current = newProps;
    for (let i = 0; i < keys.length - 1; i++) {
      current = current[keys[i]];
    }
    current[keys[keys.length - 1]] = value;

    setEditedProperties(newProps);
  };

  const handleDelete = (path: string) => {
    const keys = path.split(".");
    const newProps = JSON.parse(JSON.stringify(editedProperties));

    let current = newProps;
    for (let i = 0; i < keys.length - 1; i++) {
      current = current[keys[i]];
    }
    delete current[keys[keys.length - 1]];

    setEditedProperties(newProps);
  };

  const handleAddProperty = () => {
    if (!newKey.trim()) return;

    const newProps = { ...editedProperties, [newKey]: newValue };
    setEditedProperties(newProps);
    setNewKey("");
    setNewValue("");
  };

  const handleSave = async () => {
    await onSave(editedProperties);
  };

  return (
    <dialog className="modal modal-open">
      <div className="modal-box max-w-4xl w-full h-[80vh] flex flex-col p-0">
        {/* Header */}
        <div className="p-6 border-b border-base-300 flex-shrink-0">
          <div className="flex justify-between items-center">
            <div>
              <h3 className="text-2xl font-bold">
                {t.translations.EDIT_ADDITIONAL_PROPERTIES}
              </h3>
              <p className="text-sm text-base-content/60 mt-1">
                {t.translations.EDIT_ADDITIONAL_PROPERTIES_HELP}
              </p>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="btn btn-sm btn-ghost btn-circle"
              disabled={isSaving}
            >
              <XMarkIcon className="h-5 w-5" />
            </button>
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6">
          {/* Existing Properties */}
          <div className="card bg-base-100 border border-base-300 mb-4">
            <div className="card-body p-0">
              {Object.keys(editedProperties).length === 0 ? (
                <div className="p-8 text-center text-base-content/60">
                  {t.translations.NO_PROPERTIES_YET_ADD_ONE}
                </div>
              ) : (
                <div className="divide-y divide-base-300">
                  {Object.entries(editedProperties).map(([key, value]) => (
                    <EditablePropertyRow
                      key={key}
                      path={key}
                      value={value}
                      onChange={handleChange}
                      onDelete={handleDelete}
                    />
                  ))}
                </div>
              )}
            </div>
          </div>

          {/* Add New Property */}
          <div className="card bg-base-200 border border-base-300">
            <div className="card-body">
              <h4 className="card-title text-sm flex items-center gap-2">
                <PlusIcon className="w-4 h-4" />
                {t.translations.ADD_NEW_PROPERTY}
              </h4>
              <div className="form-control">
                <div className="join w-full">
                  <input
                    type="text"
                    placeholder={t.translations.PROPERTY_KEY_PLACEHOLDER}
                    value={newKey}
                    onChange={(e) => setNewKey(e.target.value)}
                    className="input input-sm input-bordered join-item flex-1"
                  />
                  <input
                    type="text"
                    placeholder={t.translations.VALUE}
                    value={newValue}
                    onChange={(e) => setNewValue(e.target.value)}
                    className="input input-sm input-bordered join-item flex-1"
                  />
                  <button
                    type="button"
                    onClick={handleAddProperty}
                    disabled={!newKey.trim()}
                    className="btn btn-sm btn-primary join-item"
                  >
                    {t.translations.ADD}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="modal-action p-6 border-t border-base-300 flex-shrink-0 m-0">
          <div className="flex w-full justify-end">
            <div className="flex gap-3">
              <button
                type="button"
                onClick={onClose}
                className="btn btn-ghost"
                disabled={isSaving}
              >
                {t.translations.CANCEL}
              </button>
              <button
                type="button"
                onClick={handleSave}
                className="btn btn-primary"
                disabled={isSaving}
              >
                {isSaving ? (
                  <>
                    <span className="loading loading-spinner loading-sm"></span>
                    {t.translations.SAVING}
                  </>
                ) : (
                  t.translations.SAVE_CHANGES
                )}
              </button>
            </div>
          </div>
        </div>
      </div>
      <form method="dialog" className="modal-backdrop">
        <button type="button" onClick={onClose}>
          {t.translations.CLOSE}
        </button>
      </form>
    </dialog>
  );
}
