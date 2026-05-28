"use client";

import React from "react";
import { useLanguage } from "@/app/contexts/Language";

type BadgeSize = "xs" | "sm" | "md" | "lg";

interface BetaBadgeProps {
  size?: BadgeSize;
  className?: string;
}

export const BetaBadge: React.FC<BetaBadgeProps> = ({
  size = "sm",
  className = "",
}) => {
  const { t } = useLanguage();
  return (
    <span
      className={`badge badge-${size} badge-accent badge-outline font-semibold uppercase tracking-wide ${className}`}
    >
      {t.translations.BETA}
    </span>
  );
};

export default BetaBadge;
