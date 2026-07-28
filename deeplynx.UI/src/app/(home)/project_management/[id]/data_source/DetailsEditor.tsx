import { useState } from "react";
import { DataSourceResponseDto } from "../../../types/responseDTOs";
import { UpdateDataSourceRequestDto } from "../../../types/requestDTOs";
import { updateDataSource } from "@/app/lib/client_service/data_source_services.client";

type DetailsFormState = {
  name: string;
  description: string;
  abbreviation: string;
  type: string;
  baseUri: string;
  config: string; // JSON text in the UI
};

const DetailsEditor = ({
  projectId,
  source,
  onSaved,
  onClose,
  setError,
}: {
  projectId: number;
  source: DataSourceResponseDto;
  onSaved: () => Promise<void> | void;
  onClose: () => void;
  setError: (s: string | null) => void;
}) => {
  const [form, setForm] = useState<DetailsFormState>({
    name: source.name ?? "",
    description: source.description ?? "",
    abbreviation: source.abbreviation ?? "",
    type: source.type ?? "",
    baseUri: source.baseuri ?? "",
    // Show config as pretty JSON in the textarea
    config: source.config ? JSON.stringify(source.config, null, 2) : "",
  });
  const [saving, setSaving] = useState(false);

  const onSave = async () => {
    if (source.id == null) return;

    setSaving(true);
    setError(null);

    // Validate & parse config JSON
    let parsedConfig: Record<string, unknown> | undefined = undefined;

    if (form.config.trim().length > 0) {
      try {
        const parsed = JSON.parse(form.config);
        // Make sure it's an object
        if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
          parsedConfig = parsed as Record<string, unknown>;
        } else {
          setSaving(false);
          setError("Config must be a JSON object.");
          return;
        }
      } catch (e) {
        setSaving(false);
        setError("Config must be valid JSON.");
        return;
      }
    }

    const payload: UpdateDataSourceRequestDto = {
      // only include if non-empty; otherwise leave undefined
      ...(form.name.trim() && { name: form.name.trim() }),
      ...(form.description.trim() && { description: form.description.trim() }),
      ...(form.abbreviation.trim() && {
        abbreviation: form.abbreviation.trim(),
      }),
      ...(form.type.trim() && { type: form.type.trim() }),
      ...(form.baseUri.trim() && { baseUri: form.baseUri.trim() }),
      ...(parsedConfig && { config: parsedConfig }),
    };

    try {
      await updateDataSource(projectId, Number(source.id), payload);
      await onSaved();
      onClose();
    } catch (e) {
      const errorMessage = e instanceof Error ? e.message : "Update failed.";
      setError(errorMessage);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="bg-base-200/50 rounded-lg p-4 mt-2">
      <div className="grid md:grid-cols-2 gap-3">
        <div className="form-control">
          <span className="text-sm font-semibold mb-1">Name</span>
          <input
            className="input input-bordered w-full"
            placeholder="Enter data source name"
            value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
          />
        </div>

        <div className="form-control">
          <span className="text-sm font-semibold mb-1">Abbreviation</span>
          <input
            className="input input-bordered w-full"
            placeholder="Short code (e.g. 'PGSQL')"
            value={form.abbreviation}
            onChange={(e) =>
              setForm((f) => ({ ...f, abbreviation: e.target.value }))
            }
          />
        </div>

        <div className="form-control">
          <span className="text-sm font-semibold mb-1">Type</span>
          <input
            className="input input-bordered w-full"
            placeholder="Type (e.g. PostgreSQL, S3)"
            value={form.type}
            onChange={(e) => setForm((f) => ({ ...f, type: e.target.value }))}
          />
        </div>

        <div className="form-control">
          <span className="text-sm font-semibold mb-1">
            Base URI / Connection
          </span>
          <input
            className="input input-bordered w-full"
            placeholder="Connection string or URI"
            value={form.baseUri}
            onChange={(e) =>
              setForm((f) => ({ ...f, baseUri: e.target.value }))
            }
          />
        </div>

        <div className="form-control md:col-span-2">
          <span className="text-sm font-semibold mb-1">Description</span>
          <textarea
            className="textarea textarea-bordered w-full"
            placeholder="Description"
            value={form.description}
            onChange={(e) =>
              setForm((f) => ({ ...f, description: e.target.value }))
            }
          />
        </div>

        <div className="form-control md:col-span-2">
          <span className="text-sm font-semibold mb-1">Config (JSON)</span>
          <textarea
            className="textarea textarea-bordered w-full font-mono text-sm"
            placeholder='{"key": "value"}'
            value={form.config}
            onChange={(e) => setForm((f) => ({ ...f, config: e.target.value }))}
          />
        </div>
      </div>

      <div className="flex justify-end gap-2 mt-3">
        <button className="btn btn-ghost" onClick={onClose} disabled={saving}>
          Close
        </button>
        <button className="btn btn-primary" onClick={onSave} disabled={saving}>
          {saving ? <span className="loading loading-spinner" /> : "Save"}
        </button>
      </div>
    </div>
  );
};

export default DetailsEditor;
