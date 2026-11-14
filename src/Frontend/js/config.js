// Base API URL
(function (window) {
    const host = window.location.hostname;
    const isLocalHost = host === "localhost" || host === "127.0.0.1" || host === "";
    const configured = window.DEVPILOT_API_BASE;
    const productionApi = "http://devpilot-ai.runasp.net";

    // On Vercel HTTPS keep same-origin so /api and /hubs use vercel.json rewrites
    // (avoids mixed-content blocks to the HTTP MonsterASP host).
    window.API_BASE = configured
        || (isLocalHost ? "http://localhost:5281" : (
            window.location.protocol === "https:"
                ? window.location.origin
                : productionApi
        ));

    // SignalR over Vercel proxy: prefer LongPolling (WebSockets usually fail through rewrites).
    window.DEVPILOT_SIGNALR_LONG_POLLING =
        !isLocalHost && window.location.protocol === "https:" && !configured;
})(window);

const API_BASE = window.API_BASE;

// Cloudflare Turnstile Site Key
const TURNSTILE_SITE_KEY = "0x4AAAAAAFTM4ariHx3i33R0"

