(function () {
    "use strict";

    if (!window.DevPilotApi || !window.DevPilotApi.isAuthenticated()) {
        window.location.href = "login.html?returnUrl=" + encodeURIComponent("dashboard.html");
        return;
    }

    const leaders = [
        { name: "Sara K.", score: 1840, rankClass: "" },
        { name: "Omar H.", score: 1620, rankClass: "silver" },
        { name: "Nour A.", score: 1495, rankClass: "bronze" },
        { name: "You", score: 980, rankClass: "silver" }
    ];

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

    function renderLeaderboard() {
        const box = document.getElementById("leaderboard");
        box.innerHTML = leaders.map(function (item, index) {
            return (
                '<div class="leaderboard-item">' +
                '<div class="rank-pill ' + item.rankClass + '">' + (index + 1) + "</div>" +
                "<div class=\"flex-grow-1\">" +
                "<strong>" + escapeHtml(item.name) + "</strong>" +
                '<div class="text-gray small">Challenge points</div>' +
                "</div>" +
                '<strong class="text-accent-brand">' + item.score + "</strong>" +
                "</div>"
            );
        }).join("");
        document.getElementById("stat-rank").textContent = "#4";
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

    async function loadProblems() {
        const box = document.getElementById("recent-problems");
        try {
            const url = new URL("/api/Problems/search?page=1&pageSize=8", API_BASE).href;
            const res = await window.DevPilotApi.request(url);
            const items = (res.data && (res.data.items || res.data.Items)) || [];

            let solved = 0;
            let open = 0;
            items.forEach(function (p) {
                const status = (p.status || p.Status || "").toLowerCase();
                if (status === "solved") solved += 1;
                else open += 1;
            });

            document.getElementById("stat-solved").textContent = String(solved);
            document.getElementById("stat-open").textContent = String(open);

            if (!items.length) {
                box.innerHTML =
                    '<div class="empty-state">' +
                    "<p>No problems yet. Submit your first bug and let AI walk you through the fix.</p>" +
                    '<a class="btn btn-primary mt-2" href="submit-problem.html">Debug with AI</a>' +
                    "</div>";
                return;
            }

            box.innerHTML = items.map(function (p) {
                const id = p.problemId || p.ProblemId;
                const code = p.problemCode || p.ProblemCode || "";
                const title = p.title || p.Title || "Untitled problem";
                const status = p.status || p.Status || "";
                return (
                    '<div class="list-row">' +
                    "<div>" +
                    '<a class="link-blue" href="article-details.html?id=' + encodeURIComponent(id) + '">' +
                    escapeHtml(title) +
                    "</a>" +
                    '<div class="text-gray small">' + escapeHtml(code) + "</div>" +
                    "</div>" +
                    statusBadge(status) +
                    "</div>"
                );
            }).join("");
        } catch (e) {
            box.innerHTML = '<div class="empty-state">Could not load recent problems right now.</div>';
        }
    }

    renderLeaderboard();
    loadProfile();
    loadProblems();
})();
