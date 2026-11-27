// Base API URL
(function (window) {
    const host = window.location.hostname;
    const isLocalHost = host === "localhost" || host === "127.0.0.1" || host === "";
    const configured = window.DEVPILOT_API_BASE;
    const productionApi = "http://devpilot-ai.runasp.net";

    // Vercel HTTPS: same-origin /api and /hubs via rewrites (no mixed content).
    window.API_BASE = configured
        || (isLocalHost ? "http://localhost:5281" : (
            window.location.protocol === "https:"
                ? window.location.origin
                : productionApi
        ));

    // SignalR on Vercel: LongPolling (WebSockets often fail).
    window.DEVPILOT_SIGNALR_LONG_POLLING =
        !isLocalHost && window.location.protocol === "https:" && !configured;
})(window);

const API_BASE = window.API_BASE;

// Cloudflare Turnstile Site Key
const TURNSTILE_SITE_KEY = "0x4AAAAAAFTM4ariHx3i33R0"

