// next.config.ts
import type { NextConfig } from "next";

const hideInsight =
  process.env.HIDE_INSIGHT ?? process.env.NEXT_PUBLIC_HIDE_INSIGHT ?? "true";

const nextConfig: NextConfig = {
  output: "standalone",
  env: {
    NEXT_PUBLIC_HIDE_INSIGHT: hideInsight,
  },
};

export default nextConfig;
