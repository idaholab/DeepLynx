"use client";

import { useLanguage } from "@/app/contexts/Language";
import React, { useState } from "react";
import CopyToClipboardButton from "./CopyToClipboardButton";

const joinClasses = (...classes: Array<string | undefined>) =>
  classes.filter(Boolean).join(" ");

interface AvatarCellProps {
  name?: string;
  image?: string;
  showName?: boolean;
  size?: number;
  containerClassName?: string;
  avatarClassName?: string;
  labelClassName?: string;
}

interface ContactAvatarCellProps extends Omit<AvatarCellProps, "showName"> {
  email: string;
  triggerClassName?: string;
  dropdownClassName?: string;
  detailsClassName?: string;
}

const getInitials = (fullName?: string) => {
  if (!fullName) return "?";
  const parts = fullName.trim().split(/\s+/);

  if (parts.length === 1) {
    return parts[0][0]?.toUpperCase() ?? "?";
  }

  const firstInitial = parts[0][0]?.toUpperCase() ?? "";
  const lastInitial = parts[parts.length - 1][0]?.toUpperCase() ?? "";
  return `${firstInitial}${lastInitial}`;
};

const AvatarCell: React.FC<AvatarCellProps> = ({
  name,
  image,
  showName,
  size = 10,
  containerClassName,
  avatarClassName,
  labelClassName,
}) => {
  const [imgError, setImgError] = useState(false);

  const fallbackInitials = getInitials(name);

  const sizeClass = `w-${size} h-${size}`;
  const textSizeClass = size >= 12 ? "text-xl" : "text-lg";

  return (
    <div className={joinClasses("flex items-center space-x-3", containerClassName)}>
      <div title={name || ""}>
        {imgError || !image ? (
          <div
            className={joinClasses(
              sizeClass,
              "flex items-center justify-center rounded-full bg-secondary font-bold text-primary-content",
              textSizeClass,
              avatarClassName,
            )}
          >
            {fallbackInitials}
          </div>
        ) : (
          <div className="avatar">
            <div className={joinClasses(sizeClass, "rounded-full", avatarClassName)}>
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={image} alt={name} onError={() => setImgError(true)} />
            </div>
          </div>
        )}
      </div>
      {showName && <p className={labelClassName}>{name}</p>}
    </div>
  );
};

export function ContactAvatarCell({
  name,
  image,
  size = 10,
  email,
  containerClassName,
  avatarClassName,
  triggerClassName,
  dropdownClassName,
  detailsClassName,
}: ContactAvatarCellProps) {
  const { t } = useLanguage();

  return (
    <div className="dropdown dropdown-bottom">
      <button
        type="button"
        tabIndex={0}
        aria-label={t.translations.VIEW_CONTACT_DETAILS_FOR.replace(
          "{name}",
          name ?? "",
        )}
        className={joinClasses(
          "avatar cursor-pointer rounded-full transition-transform duration-150 hover:scale-105 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-secondary/40",
          triggerClassName,
        )}
      >
        <AvatarCell
          name={name}
          image={image}
          size={size}
          containerClassName={joinClasses("space-x-0", containerClassName)}
          avatarClassName={avatarClassName}
        />
      </button>

      <div
        tabIndex={0}
        className={joinClasses(
          "dropdown-content z-[9999] mt-2 w-fit min-w-[10rem] max-w-[18rem] rounded-box border border-base-300 bg-base-100 p-3 shadow-xl",
          dropdownClassName,
        )}
      >
        <div className={joinClasses("flex items-start justify-between gap-3", detailsClassName)}>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-base-content">{name}</p>
            <p className="mt-1 break-all text-xs text-base-content/70">
              {email}
            </p>
          </div>

          <CopyToClipboardButton
            value={email}
            tooltipLabel={t.translations.COPY_EMAIL}
            ariaLabel={t.translations.COPY_EMAIL_FOR.replace(
              "{name}",
              name ?? "",
            )}
          />
        </div>
      </div>
    </div>
  );
}

export default AvatarCell;
