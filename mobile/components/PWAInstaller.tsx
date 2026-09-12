"use client";

import { useEffect } from "react";

/**
 * Registra el Service Worker para habilitar instalación (PWA) y offline parcial.
 * Solo en producción (build) viaja el SW; en dev Next sirve HMR y no debe cachear.
 */
export default function PWAInstaller() {
  useEffect(() => {
    if (!("serviceWorker" in navigator)) return;
    const onLoad = () => {
      // Enviroment: solo registrar cuando existe /sw.js de build (no en dev).
      navigator.serviceWorker
        .register("/sw.js", { scope: "/" })
        .then((reg) => console.log("SW registered:", reg.scope))
        .catch((err) => console.warn("SW registration failed:", err));
    };
    window.addEventListener("load", onLoad);
    return () => window.removeEventListener("load", onLoad);
  }, []);

  return null;
}