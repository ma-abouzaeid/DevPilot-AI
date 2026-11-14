(function () {
    "use strict";

    if (!window.DevPilotApi || !window.DevPilotApi.isAuthenticated()) {
        window.location.href = "login.html?returnUrl=" + encodeURIComponent("challenges.html");
        return;
    }

    const grid = document.getElementById("challenges-grid");
    const board = document.getElementById("leaderboard");
    const myStats = document.getElementById("my-challenge-stats");

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function difficultyBadge(diff) {
        const d = (diff || "").toLowerCase();
        if (d === "beginner") return '<span class="badge-soft progress">Beginner</span>';
        if (d === "advanced") return '<span class="badge-soft open">Advanced</span>';
        return '<span class="badge-soft ai">Intermediate</span>';
    }

    async function loadLeaderboard() {
        const url = new URL("/api/Challenges/leaderboard?top=8", API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        if (!res.ok || !res.data) throw new Error(res.message || "Failed to load leaderboard");

        const data = res.data;
        const points = data.totalPoints ?? data.TotalPoints ?? 0;
        const solved = data.challengesSolved ?? data.ChallengesSolved ?? 0;
        const rank = data.rank ?? data.Rank;
        myStats.innerHTML =
            "<strong>" + points + " pts</strong> · " +
            solved + " solved" +
            (rank ? (" · Rank #" + rank) : " · Unranked");

        const top = data.top || data.Top || [];
        if (!top.length) {
            board.innerHTML = '<div class="empty-state">No solvers yet. Be the first.</div>';
            return;
        }

        board.innerHTML = top.map(function (row) {
            const r = row.rank ?? row.Rank;
            const name = row.displayName || row.DisplayName || "Developer";
            const pts = row.totalPoints ?? row.TotalPoints ?? 0;
            const cls = r === 1 ? "" : (r === 2 ? "silver" : (r === 3 ? "bronze" : ""));
            return (
                '<div class="leaderboard-item">' +
                '<div class="rank-pill ' + cls + '">' + r + "</div>" +
                '<div class="flex-grow-1"><strong>' + escapeHtml(name) + "</strong>" +
                '<div class="text-gray small">' + (row.challengesSolved ?? row.ChallengesSolved ?? 0) + " challenges</div></div>" +
                '<strong class="text-accent-brand">' + pts + "</strong>" +
                "</div>"
            );
        }).join("");
    }

    async function loadChallenges() {
        grid.innerHTML = '<div class="col-12"><div class="empty-state">Loading challenges…</div></div>';
        const url = new URL("/api/Challenges", API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        const items = res.data || [];
        if (!items.length) {
            grid.innerHTML =
                '<div class="col-12"><div class="empty-state surface-card">' +
                "<p>No challenges yet. Solve a bug and mark it solved — DevPilot will generate a practice challenge.</p>" +
                '<a class="btn btn-primary mt-2" href="submit-problem.html">Debug a problem</a>' +
                "</div></div>";
            return;
        }

        grid.innerHTML = items.map(function (c) {
            const id = c.challengeId || c.ChallengeId;
            const title = c.title || c.Title || "Challenge";
            const description = c.description || c.Description || "";
            const difficulty = c.difficulty || c.Difficulty || "Intermediate";
            const maxPoints = c.maxPoints ?? c.MaxPoints ?? 100;
            const solved = c.solvedByCurrentUser || c.SolvedByCurrentUser;
            const best = c.bestPointsForCurrentUser ?? c.BestPointsForCurrentUser;
            return (
                '<div class="col-md-6 col-lg-4">' +
                '<div class="surface-card h-100 d-flex flex-column">' +
                '<div class="d-flex justify-content-between align-items-center mb-2">' +
                difficultyBadge(difficulty) +
                (solved ? '<span class="badge-soft solved">Solved</span>' : "") +
                "</div>" +
                "<h3 class=\"h5\">" + escapeHtml(title) + "</h3>" +
                '<p class="text-gray flex-grow-1">' + escapeHtml(description) + "</p>" +
                '<div class="d-flex justify-content-between align-items-center mt-2">' +
                '<span class="text-accent-brand"><i class="fa-solid fa-star"></i> ' + maxPoints + " pts" +
                (best != null ? (' · best ' + best) : "") + "</span>" +
                '<a class="btn btn-primary btn-sm" href="challenge-details.html?id=' + encodeURIComponent(id) + '">' +
                (solved ? "Retry" : "Solve") + "</a>" +
                "</div></div></div>"
            );
        }).join("");
    }

    Promise.all([loadChallenges(), loadLeaderboard()]).catch(function (err) {
        grid.innerHTML = '<div class="col-12"><div class="empty-state">' + escapeHtml(err.message || "Failed to load") + "</div></div>";
    });
})();
