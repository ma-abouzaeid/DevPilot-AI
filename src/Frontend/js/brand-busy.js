(function (global) {
    "use strict";

    var overlayEl = null;
    var messageEl = null;
    var detailEl = null;

    function ensureOverlay() {
        if (overlayEl) return overlayEl;
        overlayEl = document.createElement("div");
        overlayEl.id = "brand-busy-overlay";
        overlayEl.className = "brand-busy-overlay d-none";
        overlayEl.setAttribute("role", "status");
        overlayEl.setAttribute("aria-live", "polite");
        overlayEl.setAttribute("aria-busy", "true");
        overlayEl.innerHTML =
            '<div class="brand-busy-card">' +
            '<div class="brand-busy-logo-wrap" aria-hidden="true">' +
            '<img class="brand-busy-logo" src="images/logo-mark.svg" alt="">' +
            '<span class="brand-busy-ring"></span>' +
            "</div>" +
            '<p class="brand-busy-title" id="brand-busy-message">Working…</p>' +
            '<p class="brand-busy-detail" id="brand-busy-detail"></p>' +
            "</div>";
        document.body.appendChild(overlayEl);
        messageEl = document.getElementById("brand-busy-message");
        detailEl = document.getElementById("brand-busy-detail");
        return overlayEl;
    }

    function show(message, detail) {
        ensureOverlay();
        if (messageEl) messageEl.textContent = message || "Working…";
        if (detailEl) {
            detailEl.textContent = detail || "";
            detailEl.classList.toggle("d-none", !detail);
        }
        overlayEl.classList.remove("d-none");
        document.body.classList.add("brand-busy-open");
    }

    function hide() {
        if (!overlayEl) return;
        overlayEl.classList.add("d-none");
        document.body.classList.remove("brand-busy-open");
    }

    global.DevPilotBusy = {
        show: show,
        hide: hide
    };
})(window);
