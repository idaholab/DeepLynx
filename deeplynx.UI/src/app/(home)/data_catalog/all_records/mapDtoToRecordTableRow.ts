// app/(home)/lib/mapDtoToRecordTableRow.ts
import { QueryRecordViewResponseDto } from "@/app/(home)/types/responseDTOs";
import { RecordTableRow } from "@/app/(home)/types/types";

export function mapDtoToRecordTableRow(
    dto: QueryRecordViewResponseDto,
): RecordTableRow {
    return {
        ...dto,
        fileType: "",
        timeseries: undefined,
        fileSize: undefined,
        select: false,
        associatedRecords: undefined,
        archivedAt: dto.isArchived ? dto.lastUpdatedAt : null,
    };
}