"use client";
import React from "react";
import { RecentUpload } from "../types/types";
import {
  ArrowsRightLeftIcon,
  LinkIcon,
  CircleStackIcon,
} from "@heroicons/react/24/outline";
import AvatarCell from "./Avatar";

const iconMap = {
  arrows: ArrowsRightLeftIcon,
  link: LinkIcon,
  stack: CircleStackIcon,
} as const;

type IconKey = keyof typeof iconMap;

export default function RecentUploadsCard({
  title = "Recent Uploads to Project",
  uploads,
  uploadText,
}: {
  title?: string;
  uploads: RecentUpload[];
  uploadText: string;
}) {
  return (
    <div className="card card-border w-auto">
      <div className="card-body">
        <h2 className="card-title">{title}</h2>
        <ul className="list bg-base-200/20 rounded rounded-xl">
          {uploads.map((u) => {
            return (
              <li className="list-row" key={u.id}>
                <AvatarCell name={u.name} image={u.avatar} />
                <div className="pt-2">
                  <b className="text-black">{u.name}</b> {uploadText} {u.file}
                </div>
              </li>
            );
          })}
        </ul>
      </div>
    </div>
  );
}
