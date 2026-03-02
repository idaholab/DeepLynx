import React, { FC, useState } from 'react';
import GenericTable from '../../GenericTable';
import { useLanguage } from "@/app/contexts/Language";
import { Column } from '../../../types/types';
import { PermissionResponseDto } from '@/app/(home)/types/responseDTOs';
interface UserPermsTableProps {
  data: PermissionResponseDto[];
}

const UserManagementTable: FC<UserPermsTableProps> = ({ data: initialData }) => {
  const [data] = useState<PermissionResponseDto[]>(initialData);
  const [selectedMembers, setSelectedMembers] = useState<boolean[]>(new Array(initialData.length).fill(false));
  const [selectAll, setSelectAll] = useState<boolean>(false);

  const handleSelectAll = () => {
    const newSelection = !selectAll;
    setSelectAll(newSelection);
    setSelectedMembers(new Array(data.length).fill(newSelection));
  };

  const handleCheckboxChange = (index: number) => {
    const newSelection = [...selectedMembers];
    newSelection[index] = !newSelection[index];
    setSelectedMembers(newSelection);

    if (newSelection.every(Boolean)) {
      setSelectAll(true);
    } else {
      setSelectAll(false);
    }
  };


  const columns: Column<PermissionResponseDto>[] = [
    {
      header: "User Mangement",
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
        data={data}
      // enablePagination
      // rowsPerPage={5}
      />
    </div>
  );
};

export default UserManagementTable;