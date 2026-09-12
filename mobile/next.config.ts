import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // El Service Worker y el manifest deben servirse sin caché agresiva para que las
  // actualizaciones de la PWA se propaguen (evita SW "pegado" en versiones viejas).
  async headers() {
    return [
      {
        source: "/sw.js",
        headers: [
          { key: "Cache-Control", value: "public, max-age=0, must-revalidate" },
          { key: "Service-Worker-Allowed", value: "/" },
        ],
      },
      {
        source: "/manifest.webmanifest",
        headers: [{ key: "Content-Type", value: "application/manifest+json" }],
      },
    ];
  },
};

export default nextConfig;