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
            // totalCount kept for future widgets
            void totalCount;

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
        }
    }

    async function loadChallengeStats() {
        const activity = document.getElementById("leaderboard");
        const rankEl = document.getElementById("stat-rank");
        const pointsEl = document.getElementById("stat-credits");
        try {
            const url = new URL("/api/Challenges/leaderboard?top=5", API_BASE).href;
            const res = await window.DevPilotApi.request(url);
            if (!res.ok || !res.data) throw new Error("leaderboard failed");

            const data = res.data;
            const points = data.totalPoints ?? data.TotalPoints ?? 0;
            const solved = data.challengesSolved ?? data.ChallengesSolved ?? 0;
            const rank = data.rank ?? data.Rank;
            rankEl.textContent = rank ? ("#" + rank) : "—";
            pointsEl.textContent = String(points);

            const top = data.top || data.Top || [];
            if (!top.length) {
                activity.innerHTML =
                    '<div class="empty-state">No challenge points yet. <a class="link-blue" href="challenges.html">Solve a challenge</a></div>';
                return;
            }

            activity.innerHTML = top.map(function (row) {
                const r = row.rank ?? row.Rank;
                const name = row.displayName || row.DisplayName || "Developer";
                const pts = row.totalPoints ?? row.TotalPoints ?? 0;
                const cls = r === 1 ? "" : (r === 2 ? "silver" : (r === 3 ? "bronze" : ""));
                return (
                    '<div class="leaderboard-item">' +
                    '<div class="rank-pill ' + cls + '">' + r + "</div>" +
                    '<div class="flex-grow-1"><strong>' + escapeHtml(name) + "</strong>" +
                    '<div class="text-gray small">' + (row.challengesSolved ?? row.ChallengesSolved ?? 0) + " solved</div></div>" +
                    '<strong class="text-accent-brand">' + pts + "</strong></div>"
                );
            }).join("") +
                '<div class="text-gray small mt-2">You: ' + points + " pts · " + solved + " solved" +
                (rank ? (" · #" + rank) : "") + "</div>";
        } catch (e) {
            rankEl.textContent = "—";
            pointsEl.textContent = "—";
            if (activity) {
                activity.innerHTML = '<div class="empty-state">Challenge stats unavailable.</div>';
            }
        }
    }

    loadProfile();
    loadMyProblems();
    loadChallengeStats();
})();
