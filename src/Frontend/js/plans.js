(function () {
    "use strict";

    const grid = document.getElementById("plans-grid");
    const alertBox = document.getElementById("plans-alert");
    const usageCard = document.getElementById("my-usage-card");
    const usageText = document.getElementById("my-usage-text");
    const creditsEl = document.getElementById("my-credits-remaining");
    const loggedIn = window.DevPilotApi && window.DevPilotApi.isAuthenticated();

    let currentPlanCode = null;

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function showAlert(message, type) {
        alertBox.textContent = message;
        alertBox.className = "alert alert-" + (type || "danger");
        alertBox.classList.remove("d-none");
    }

    function featureLabel(key) {
        const map = {
            ai_debugging: "AI Debugging",
            monthly_ai_credits: "Monthly AI Credits",
            github_code_review: "GitHub Code Review"
        };
        return map[key] || key;
    }

    function priceLabel(plan) {
        const price = plan.price ?? plan.Price;
        const currency = plan.currency || plan.Currency || "USD";
        if (!price || Number(price) <= 0) return "Free";
        return "$" + Number(price).toFixed(0) + " / mo";
    }

    async function loadUsage() {
        if (!loggedIn) return;
        usageCard.classList.remove("d-none");
        const url = new URL("/api/Plans/me", API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        if (!res.ok || !res.data) throw new Error(res.message || "Failed to load usage");
        const d = res.data;
        currentPlanCode = (d.planCode || d.PlanCode || "").toUpperCase();
        const unlimited = d.unlimitedCredits || d.UnlimitedCredits;
        const remaining = d.creditsRemaining ?? d.CreditsRemaining ?? 0;
        const used = d.creditsUsed ?? d.CreditsUsed ?? 0;
        const allocated = d.creditsAllocated ?? d.CreditsAllocated ?? 0;
        const canReview = d.canUseCodeReview || d.CanUseCodeReview;
        creditsEl.textContent = unlimited ? "Unlimited" : String(remaining);
        usageText.textContent = unlimited
            ? (d.planName || d.PlanName || currentPlanCode) + " · Admin unlimited credits" +
              (canReview ? " · Code Review enabled" : "")
            : (d.planName || d.PlanName || currentPlanCode) +
              " · " + used + " / " + allocated + " credits used" +
              (canReview ? " · Code Review enabled" : " · Code Review requires Pro");
    }

    async function activate(planCode) {
        if (!loggedIn) {
            window.location.href = "login.html?returnUrl=" + encodeURIComponent("plans.html");
            return;
        }
        try {
            const url = new URL("/api/Plans/activate", API_BASE).href;
            const res = await window.DevPilotApi.request(url, {
                method: "POST",
                body: JSON.stringify({ planCode: planCode })
            });
            if (!res.ok || !res.data) throw new Error(res.message || "Activation failed");
            showAlert((res.data.planName || res.data.PlanName || planCode) + " is now active.", "success");
            await loadUsage();
            await loadPlans();
        } catch (err) {
            showAlert(err.message || "Activation failed", "danger");
        }
    }

    async function loadPlans() {
        const url = new URL("/api/Plans", API_BASE).href;
        const res = await window.DevPilotApi.request(url, { skipAuth: true });
        const items = res.data || [];
        if (!items.length) {
            grid.innerHTML = '<div class="col-12"><div class="empty-state">No plans configured.</div></div>';
            return;
        }

        grid.innerHTML = items.map(function (p) {
            const code = (p.code || p.Code || "").toUpperCase();
            const name = p.name || p.Name || code;
            const description = p.description || p.Description || "";
            const credits = p.creditsPerPeriod ?? p.CreditsPerPeriod ?? 0;
            const features = p.features || p.Features || [];
            const isCurrent = loggedIn && currentPlanCode === code;
            const isPro = code === "PRO";
            const featureHtml = features.map(function (f) {
                return "<li><i class=\"fa-solid fa-check text-accent-brand me-2\"></i>" + escapeHtml(featureLabel(f)) + "</li>";
            }).join("") || "<li class=\"text-gray\">Core AI debugging</li>";

            const cta = isCurrent
                ? '<button type="button" class="btn btn-outline-card w-100" disabled>Current plan</button>'
                : ('<button type="button" class="btn ' + (isPro ? "btn-primary" : "btn-outline-card") +
                    ' w-100 btn-activate" data-plan="' + escapeHtml(code) + '">' +
                    (isPro ? "Upgrade to Pro" : "Switch to Free") + "</button>");

            return (
                '<div class="col-md-6">' +
                '<div class="surface-card h-100 d-flex flex-column">' +
                (isPro ? '<span class="badge-soft ai mb-2">Recommended</span>' : '<span class="badge-soft progress mb-2">Starter</span>') +
                '<h2 class="h3 mb-1">' + escapeHtml(name) + "</h2>" +
                '<p class="stat-value mb-2" style="font-size:2rem;">' + escapeHtml(priceLabel(p)) + "</p>" +
                '<p class="text-gray">' + escapeHtml(description) + "</p>" +
                '<p class="mb-2"><strong class="text-accent-brand">' + credits + " credits</strong> / month</p>" +
                '<ul class="list-unstyled flex-grow-1 mb-3">' + featureHtml + "</ul>" +
                cta +
                "</div></div>"
            );
        }).join("");

        grid.querySelectorAll(".btn-activate").forEach(function (btn) {
            btn.addEventListener("click", function () {
                activate(btn.getAttribute("data-plan"));
            });
        });
    }

    Promise.resolve()
        .then(function () { return loggedIn ? loadUsage() : null; })
        .then(loadPlans)
        .catch(function (err) {
            grid.innerHTML = '<div class="col-12"><div class="empty-state">' + escapeHtml(err.message || "Failed to load") + "</div></div>";
        });
})();
