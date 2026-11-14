(function () {
    "use strict";

    if (!window.DevPilotApi || !window.DevPilotApi.isAuthenticated()) {
        window.location.href = "login.html?returnUrl=" + encodeURIComponent("dashboard.html");
        return;
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function statusBadge(status) {
        const s = (status || "").toLowerCase();
        if (s === "solved") return '<span class="badge-soft solved">Solved</span>';
        if (s === "inprogress" || s === "in_progress") return '<span class="badge-soft progress">In Progress</span>';
        return '<span class="badge-soft open">Open</span>';
    }

    function continueHref(p) {
        const status = (p.status || p.Status || "").toLowerCase();
        const id = p.problemId || p.ProblemId;
        if (status === "solved") {
            return "article-details.html?problemId=" + encodeURIComponent(id);
        }
        return "submit-problem.html?problemId=" + encodeURIComponent(id);
    }

    async function loadProfile() {
        try {
            const url = new URL("/api/Users/me", API_BASE).href;
            const res = await window.DevPilotApi.request(url);
            if (res.ok && res.data) {
                document.getElementById("user-name").textContent = res.data.name || res.data.email || "Developer";
            }
        } catch (e) {
            console.warn("Profile load failed", e);
        }
    }

    async function loadMyProblems() {
        const box = document.getElementById("recent-problems");
        const activity = document.getElementById("leaderboard");
        try {
            const url = new URL("/api/Problems/mine?recentCount=8", API_BASE).href;
            const res = await window.DevPilotApi.request(url);
            if (!res.ok || !res.data) {
                throw new Error(res.message || "Failed to load dashboard");
            }

            const data = res.data;
            const openCount = data.openCount ?? data.OpenCount ?? 0;
            const solvedCount = data.solvedCount ?? data.SolvedCount ?? 0;
            const totalCount = data.totalCount ?? data.TotalCount ?? 0;
            const items = data.recent || data.Recent || [];

            document.getElementById("stat-open").textContent = String(openCount);
            document.getElementById("stat-solved").textContent = String(solvedCount);
            document.getElementById("stat-rank").textContent = String(totalCount);
            document.getElementById("stat-credits").textContent = "—";

            if (activity) {
                activity.innerHTML =
                    '<div class="list-row"><div><strong>Total tickets</strong><div class="text-gray small">Your debugging history</div></div><strong>' + totalCount + "</strong></div>" +
                    '<div class="list-row"><div><strong>Still open</strong><div class="text-gray small">Need follow-up</div></div><strong>' + openCount + "</strong></div>" +
                    '<div class="list-row"><div><strong>Solved</strong><div class="text-gray small">Published as knowledge</div></div><strong>' + solvedCount + "</strong></div>";
            }

            if (!items.length) {
                box.innerHTML =
                    '<div class="empty-state">' +
                    "<p>No problems yet. Submit your first bug and let AI walk you through the fix.</p>" +
                    '<a class="btn btn-primary mt-2" href="submit-problem.html">Debug with AI</a>' +
                    "</div>";
                return;
            }

            box.innerHTML = items.map(function (p) {
                const code = p.problemCode || p.ProblemCode || "";
                const title = p.title || p.Title || "Untitled problem";
                const status = p.status || p.Status || "";
                return (
                    '<div class="list-row">' +
                    "<div>" +
                    '<a class="link-blue" href="' + continueHref(p) + '">' +
                    escapeHtml(title) +
                    "</a>" +
                    '<div class="text-gray small">' + escapeHtml(code) + "</div>" +
                    "</div>" +
                    statusBadge(status) +
                    "</div>"
                );
            }).join("");
        } catch (e) {
            box.innerHTML = '<div class="empty-state">Could not load your recent problems right now.</div>';
            if (activity) {
                activity.innerHTML = '<div class="empty-state">Activity unavailable.</div>';
            }
        }
    }

    loadProfile();
    loadMyProblems();
})();
