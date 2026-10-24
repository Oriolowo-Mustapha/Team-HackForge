/* Cache only the public offline page and immutable app assets. Never cache farm data or API responses. */
const VERSION = "farmhand-offline-v2";
const OFFLINE = "/offline";
self.addEventListener("install", (event) => {
  event.waitUntil(
    (async () => {
      const cache = await caches.open(VERSION);
      const response = await fetch(OFFLINE, { cache: "reload" });
      if (!response.ok) throw new Error("Offline page unavailable");
      await cache.put(OFFLINE, response.clone());
      const html = await response.text();
      const assets = [
        ...new Set(
          [...html.matchAll(/(?:src|href)="([^" ]+)"/g)]
            .map((match) => match[1])
            .filter((url) => url.startsWith("/_next/static/")),
        ),
      ];
      await cache.addAll([
        ...assets,
        "/icons/icon-192.png",
        "/icons/icon-512.png",
      ]);
    })(),
  );
});
self.addEventListener("activate", (event) => {
  event.waitUntil(
    (async () => {
      for (const key of await caches.keys()) {
        if (key.startsWith("farmhand-offline-") && key !== VERSION)
          await caches.delete(key);
      }
      await self.clients.claim();
    })(),
  );
});
self.addEventListener("message", (event) => {
  if (event.data?.type === "SKIP_WAITING") self.skipWaiting();
});
self.addEventListener("fetch", (event) => {
  const request = event.request;
  const url = new URL(request.url);
  if (
    request.method !== "GET" ||
    url.origin !== self.location.origin ||
    url.pathname.startsWith("/api/")
  )
    return;
  if (request.mode === "navigate") {
    event.respondWith(
      fetch(request).catch(
        async () =>
          (await caches.match(OFFLINE)) ??
          new Response("FarmHand is offline. Reconnect to continue.", {
            status: 503,
          }),
      ),
    );
    return;
  }
  if (
    url.pathname.startsWith("/_next/static/") ||
    url.pathname.startsWith("/icons/")
  ) {
    event.respondWith(
      (async () => {
        const cached = await caches.match(request);
        if (cached) return cached;
        const response = await fetch(request);
        if (response.ok) {
          const cache = await caches.open(VERSION);
          await cache.put(request, response.clone());
        }
        return response;
      })(),
    );
  }
});
