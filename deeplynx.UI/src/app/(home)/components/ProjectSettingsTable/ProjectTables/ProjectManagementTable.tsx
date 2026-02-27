import React, { FC, useState, useEffect } from 'react';
import GenericTable from '../../GenericTable';
import { useLanguage } from "@/app/contexts/Language";
import { Column } from '../../../types/types';
import { PermissionResponseDto, RoleResponseDto } from '@/app/(home)/types/responseDTOs';

interface RoleManagementTableProps {
  initialData: PermissionResponseDto[];
  projectData: PermissionResponseDto[];
}

const RoleManagementTable: FC<RoleManagementTableProps> = ({
  initialData,
  projectData,
}) => {
  const { t } = useLanguage();
  const [selectedMembers, setSelectedMembers] = useState<boolean[]>(new Array(initialData.length).fill(false));
  const [selectAll, setSelectAll] = useState<boolean>(false);

  // ===== Project Management table state =====
  const [projRows, setProjRows] = useState<PermissionResponseDto[]>(projectData);
  const [projSelected, setProjSelected] = useState<boolean[]>(
    new Array(projectData.length).fill(false)
  );
  const [projSelectAll, setProjSelectAll] = useState(false);

  useEffect(() => {
    setProjRows(projectData);
    setProjSelected(new Array(projectData.length).fill(false));
    setProjSelectAll(false);
  }, [projectData]);

  const handleSelectAll = () => {
    const newSelection = !selectAll;
    setSelectAll(newSelection);
    setSelectedMembers(new Array(initialData.length).fill(newSelection));
  };

  const handleCheckboxChange = (index: number) => {
    const newSelection = [...selectedMembers];
    newSelection[index] = !newSelection[index];
    setSelectedMembers(newSelection);
  };

  const columns: Column<PermissionResponseDto>[] = [
    {
      header: "Project Management",
      data: "action",
    },
    {
      header: "Description",
      data: "description",
      sortable: false,
    },
    {
      header: (
        <input
          type="checkbox"
          className="checkbox"
          checked={selectAll}
          onChange={handleSelectAll}
        />
      ),
      cell: (row: PermissionResponseDto, index: number) => (
        <input
          type="checkbox"
          className="checkbox"
          checked={selectedMembers[index]}
          onChange={() => handleCheckboxChange(index)}
        />
      ),
      sortable: false,
    },
  ];

  return (
    <div>
      <GenericTable
        columns={columns}
        data={projectData}
      />
    </div>
  );
};

export default RoleManagementTable;