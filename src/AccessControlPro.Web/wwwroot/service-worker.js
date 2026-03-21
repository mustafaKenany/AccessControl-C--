// Simple service worker for PWA
self.addEventListener('install', event => {
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(clients.claim());
});

self.addEventListener('fetch', event => {
    // Network first, cache fallback
    event.respondWith(
        fetch(event.request).catch(() => caches.match(event.request))
    );
});
