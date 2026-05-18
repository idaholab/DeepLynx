"use client";
import { useState } from "react";
import { DatePickerQuery } from "../types/types";

interface DatePickerProps {
    row: DatePickerQuery;
    onChange: (value: string) => void;
}

type DateState = { dateValue?: string };


export const DatePicker: React.FC<DatePickerProps> = ({ row, onChange }) => {
    const [date, setDate] = useState<DateState>({});


    const handleDateTimeChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const value = e.target.value;
        if (!value) return;
        setDate({ dateValue: value });
        onChange(`${value}T00:00:00`);
    };

    return (
        <div className="w-full">
            <div className="flex flex-wrap items-center gap-2">
                {/* Date */}
                <div className="relative w-full sm:w-auto">
                    <input
                        type="date"
                        className="input input-bordered input-sm max-h-8 w-full sm:w-auto"
                        onChange={handleDateTimeChange}
                    />
                </div>
            </div>
        </div>


    );
}
