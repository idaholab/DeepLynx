// app/components/WithT.tsx
"use client";
import { useLanguage } from "@/app/contexts/Language";
type Translation = { translations: Record<string, string> };

export default function WithT({
  children,
}: {
  children: (t: Translation) => React.ReactNode;
}) {
  const { t } = useLanguage();
  return <>{children(t)}</>;
}
