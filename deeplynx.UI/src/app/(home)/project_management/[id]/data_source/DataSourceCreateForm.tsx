// src/app/(home)/project_management/data_source/DataSourceCreateForm.tsx

import type { CreateDataSourceRequestDto } from "@/app/(home)/types/requestDTOs";

type CreateFormProps = {
  createForm: CreateDataSourceRequestDto;
  setCreateForm: React.Dispatch<
    React.SetStateAction<CreateDataSourceRequestDto>
  >;
  saving: boolean;
  onCreate: () => void;
  onCancel: () => void;
};

const DataSourceCreateForm = ({
  createForm,
  setCreateForm,
  saving,
  onCreate,
  onCancel,
}: CreateFormProps) => {
  return (
    <div className="card mb-6 border-2 border-dashed border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body">
        <h3 className="text-lg font-semibold">Create Data Source</h3>
        <div className="grid md:grid-cols-2 gap-3">
          <input
            className="input input-bordered"
            placeholder="Name *"
            value={createForm.name}
            onChange={(e) =>
              setCreateForm((f) => ({ ...f, name: e.target.value }))
            }
          />
          <input
            className="input input-bordered"
            placeholder="Abbreviation"
            value={createForm.abbreviation ?? ""}
            onChange={(e) =>
              setCreateForm((f) => ({ ...f, abbreviation: e.target.value }))
            }
          />
          <input
            className="input input-bordered"
            placeholder="Type (e.g., PostgreSQL, S3)"
            value={createForm.type ?? ""}
            onChange={(e) =>
              setCreateForm((f) => ({ ...f, type: e.target.value }))
            }
          />
          <input
            className="input input-bordered"
            placeholder="Base URI / Connection"
            value={createForm.baseUri ?? ""}
            onChange={(e) =>
              setCreateForm((f) => ({ ...f, baseUri: e.target.value }))
            }
          />
          <textarea
            className="textarea textarea-bordered md:col-span-2"
            placeholder="Description"
            value={createForm.description ?? ""}
            onChange={(e) =>
              setCreateForm((f) => ({ ...f, description: e.target.value }))
            }
          />
        </div>
        <div className="flex justify-end gap-2 mt-3">
          <button
            className="btn btn-ghost"
            onClick={onCancel}
            disabled={saving}
          >
            Cancel
          </button>
          <button
            className="btn btn-primary"
            onClick={onCreate}
            disabled={saving}
          >
            {saving ? <span className="loading loading-spinner" /> : "Create"}
          </button>
        </div>
      </div>
    </div>
  );
};

export default DataSourceCreateForm;
