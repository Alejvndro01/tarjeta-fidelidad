// public/sw.js — Service Worker PWA (Workbox runtime)
// Copia de runtime offline: cachea navegación (shell de la app) con network-first y
// precaches los assets estáticos (JS/CSS) para abrir la app sin conexión.
importScripts("https://storage.googleapis.com/workbox-cdn/releases/7.1.0/workbox-sw.js");

workbox.setConfig({ debug: false });

const { registerRoute } = workbox.routing;
const { NetworkFirst, CacheFirst, StaleWhileRevalidate } = workbox.strategies;
const { ExpirationPlugin } = workbox.expiration;
const { precacheAndRoute } = workbox.precaching;

// Assets precacheados los inyecta el build (scripts/build-sw.js) al generar el SW final.
// Se reemplaza el array durante build. En dev queda vacío (NetworkOnly).
self.__WB_MANIFEST = [];

// Navegación: network-first con fallback a shell cacheado → offline parcial.
registerRoute(
  ({ request }) => request.mode === "navigate",
  new NetworkFirst({
    cacheName: "shell",
    networkTimeoutSeconds: 3,
    plugins: [new ExpirationPlugin({ maxEntries: 1, maxAgeSeconds: 24 * 60 * 60 })],
  })
);

// Assets JS/CSS/fuentes: cache-first (inmutables por hash).
registerRoute(
  ({ request }) =>
    ["script", "style"].includes(request.destination) || request.destination === "font",
  new CacheFirst({
    cacheName: "static-assets",
    plugins: [new ExpirationPlugin({ maxEntries: 100, maxAgeSeconds: 30 * 24 * 60 * 60 })],
  })
);

// Imágenes: stale-while-revalidate.
registerRoute(
  ({ request }) => request.destination === "image",
  new StaleWhileRevalidate({ cacheName: "images", maxEntries: 60 })
);

// Activación: limpia cachés viejas.
self.addEventListener("activate", (event) => {
  const allowed = ["shell", "static-assets", "images"];
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => !allowed.includes(k)).map((k) => caches.delete(k)))
    )
  );
});