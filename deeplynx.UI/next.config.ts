// next.config.ts
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  env: {
    NEXT_PUBLIC_HIDE_INSIGHT:
      process.env.NEXT_PUBLIC_HIDE_INSIGHT ?? process.env.HIDE_INSIGHT ?? "true",
  },
};

export default nextConfig;
