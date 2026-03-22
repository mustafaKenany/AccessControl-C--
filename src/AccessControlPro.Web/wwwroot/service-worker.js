// Service worker for PWA — network only (no caching issues)
self.addEventListener('install', event => {
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    // Clear any old caches
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.map(key => caches.delete(key)))
        ).then(() => clients.claim())
    );
});

self.addEventListener('fetch', event => {
    // Always fetch from network — no caching (prevents stale CSS/JS)
    event.respondWith(fetch(event.request));
});
