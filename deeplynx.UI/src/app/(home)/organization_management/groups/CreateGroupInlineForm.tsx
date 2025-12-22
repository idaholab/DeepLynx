// src/app/(home)/organization_management/groups/CreateGroupInlineForm.tsx
"use client";

import { useLanguage } from "@/app/contexts/Language";
import React from "react";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

interface CreateGroupInlineFormProps {
  show: boolean;
  loading: boolean;
  newGroupName: string;
  newGroupDescription: string;
  onChangeName: (value: string) => void;
  onChangeDescription: (value: string) => void;
  onCancel: () => void;
  onSubmit: () => void;
}

/* -------------------------------------------------------------------------- */
/*                        CreateGroupInlineForm Component                     */
/* -------------------------------------------------------------------------- */

const CreateGroupInlineForm: React.FC<CreateGroupInlineFormProps> = ({
  show,
  loading,
  newGroupName,
  newGroupDescription,
  onChangeName,
  onChangeDescription,
  onCancel,
  onSubmit,
}) => {
  const { t } = useLanguage();
  if (!show) return null;

  return (
    <div className="bg-base-200 rounded-lg shadow-lg p-6 mb-4 border-2 border-primary">
      <h3 className="font-bold text-xl mb-4">
        {t.translations.CREATE_NEW_GROUP}
      </h3>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
        <div className="form-control">
          <label className="label">
            <span className="label-text font-semibold">
              {t.translations.GROUP_NAME_STAR}
            </span>
          </label>
          <input
            type="text"
            placeholder={t.translations.EG_ENGINEERING_TEAM}
            className="input input-bordered w-full"
            value={newGroupName}
            onChange={(e) => onChangeName(e.target.value)}
            disabled={loading}
          />
        </div>
        <div className="form-control">
          <label className="label">
            <span className="label-text font-semibold">
              {t.translations.DESCRIPTION}
            </span>
          </label>
          <input
            type="text"
            placeholder={t.translations.BRIEF_DESCRIPTION}
            className="input input-bordered w-full"
            value={newGroupDescription}
            onChange={(e) => onChangeDescription(e.target.value)}
            disabled={loading}
          />
        </div>
      </div>
      <div className="flex gap-2 justify-end">
        <button className="btn btn-ghost" onClick={onCancel} disabled={loading}>
          {t.translations.CANCEL}
        </button>
        <button
          className="btn btn-primary"
          onClick={onSubmit}
          disabled={loading || !newGroupName.trim()}
        >
          {loading ? (
            <span className="loading loading-spinner loading-sm" />
          ) : (
            t.translations.CREATE_GROUP
          )}
        </button>
      </div>
    </div>
  );
};

export default CreateGroupInlineForm;
