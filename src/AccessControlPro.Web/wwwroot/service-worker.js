// AccessControlPro PWA service worker.
//
// This app is Blazor SERVER (live SignalR connection), so the interactive pages
// cannot run offline — they need the server. We therefore use a conservative
// strategy: cache the static "app shell" (CSS, icons, manifest, fonts) for fast
// loads, always go to the network for navigations and the Blazor framework, and
// fall back to a friendly offline page when the network is unreachable.
//
// Bump CACHE_VERSION on each release so old shell assets are evicted.
const CACHE_VERSION = 'acp-shell-v3';
const OFFLINE_URL = '/offline.html';

const SHELL_ASSETS = [
    OFFLINE_URL,
    '/app.css',
    '/manifest.json',
    '/icon-192.png',
    '/icon-512.png',
    '/apple-touch-icon.png',
    '/favicon.png'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_VERSION)
            .then(cache => cache.addAll(SHELL_ASSETS).catch(() => { /* tolerate a missing asset */ }))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(k => k !== CACHE_VERSION).map(k => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    const req = event.request;
    if (req.method !== 'GET') return;

    const url = new URL(req.url);
    if (url.origin !== self.location.origin) return; // let cross-origin (QR API, fonts CDN) pass through

    // NEVER cache the Blazor framework, SignalR, or API calls — always network.
    if (url.pathname.startsWith('/_framework') ||
        url.pathname.startsWith('/_blazor') ||
        url.pathname.startsWith('/api/')) {
        return; // default browser handling (network)
    }

    // Navigations: network-first, fall back to the offline page when truly offline.
    if (req.mode === 'navigate') {
        event.respondWith(
            fetch(req).catch(() => caches.match(OFFLINE_URL))
        );
        return;
    }

    // Static shell assets: NETWORK-FIRST so a new deploy is picked up immediately
    // (no manual "clear cache" needed), falling back to the cached copy only when the
    // network is unreachable (offline). Freshness is then governed by the HTTP cache
    // headers the server sends (app.css = 5 min, versioned libs/fonts = 30 days).
    event.respondWith(
        fetch(req).then(resp => {
            if (resp && resp.status === 200 && resp.type === 'basic') {
                const copy = resp.clone();
                caches.open(CACHE_VERSION).then(c => c.put(req, copy));
            }
            return resp;
        }).catch(() => caches.match(req))
    );
});

// ---- Web Push (renewal reminders). Wired in Phase 3; safe to ship now. ----
self.addEventListener('push', event => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch (e) { data = { body: event.data ? event.data.text() : '' }; }
    const title = data.title || 'HM-Gym';
    const options = {
        body: data.body || '',
        icon: '/icon-192.png',
        badge: '/icon-192.png',
        dir: 'auto',
        data: { url: data.url || '/my' }
    };
    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const target = (event.notification.data && event.notification.data.url) || '/my';
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(list => {
            for (const c of list) { if ('focus' in c) { c.navigate(target); return c.focus(); } }
            if (clients.openWindow) return clients.openWindow(target);
        })
    );
});
