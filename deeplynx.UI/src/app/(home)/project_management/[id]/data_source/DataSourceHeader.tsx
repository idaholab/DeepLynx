// src/app/(home)/project_management/[id]/data_source/DataSourceHeader.tsx

type HeaderProps = {
  hideArchived: boolean;
  setHideArchived: (value: boolean | ((prev: boolean) => boolean)) => void;
};

const DataSourceHeader = ({ hideArchived, setHideArchived }: HeaderProps) => {
  return (
    <div className="mb-6 flex items-center justify-between border-b border-base-300 pb-4">
      <div>
        <h2 className="text-2xl font-bold mb-2">Data Sources</h2>
        <p className="text-base-content/70">
          Manage catalog data sources for this project
        </p>
      </div>
      <label className="flex items-center gap-2 cursor-pointer">
        <input
          type="checkbox"
          className="toggle toggle-primary"
          checked={hideArchived}
          onChange={() => setHideArchived((s) => !s)}
        />
        <span className="text-sm">Hide archived</span>
      </label>
    </div>
  );
};

export default DataSourceHeader;
