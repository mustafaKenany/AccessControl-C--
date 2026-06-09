// Web-push client helper for member renewal reminders.
// Called from the Blazor /my page via IJSRuntime. Returns JSON strings so the C#
// side can parse without a typed interop contract.
window.acpPush = {
    _toKey: function (base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw = atob(base64);
        const arr = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; ++i) arr[i] = raw.charCodeAt(i);
        return arr;
    },

    // Returns 'unsupported' | 'denied' | 'subscribed' | 'none'
    status: async function () {
        try {
            if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window))
                return 'unsupported';
            if (Notification.permission === 'denied') return 'denied';
            const reg = await navigator.serviceWorker.ready;
            const sub = await reg.pushManager.getSubscription();
            return sub ? 'subscribed' : 'none';
        } catch (e) { return 'none'; }
    },

    // Requests permission, subscribes, and returns the subscription JSON
    // ({endpoint, keys:{p256dh, auth}}) — or {"error":"..."} on failure.
    subscribe: async function (publicKey) {
        try {
            if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window))
                return JSON.stringify({ error: 'unsupported' });

            const perm = await Notification.requestPermission();
            if (perm !== 'granted') return JSON.stringify({ error: 'denied' });

            const reg = await navigator.serviceWorker.ready;
            let sub = await reg.pushManager.getSubscription();
            if (!sub) {
                sub = await reg.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: this._toKey(publicKey)
                });
            }
            return JSON.stringify(sub);
        } catch (e) {
            return JSON.stringify({ error: (e && e.message) ? e.message : 'error' });
        }
    }
};
