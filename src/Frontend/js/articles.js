(function () {
    "use strict";

    const grid = document.getElementById("articles-grid");
    const form = document.getElementById("search-form");

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
        return '<span class="badge-soft progress">In Progress</span>';
    }

    async function loadArticles(q, technology) {
        grid.innerHTML = '<div class="col-12"><div class="empty-state">Searching knowledge base…</div></div>';
        try {
            const url = new URL("/api/Problems/search", API_BASE);
            url.searchParams.set("page", "1");
            url.searchParams.set("pageSize", "12");
            if (q) url.searchParams.set("q", q);
            if (technology) url.searchParams.set("technology", technology);

            const res = await window.DevPilotApi.request(url.href, { skipAuth: true });
            const items = (res.data && (res.data.items || res.data.Items)) || [];

            if (!items.length) {
                grid.innerHTML =
                    '<div class="col-12"><div class="empty-state surface-card">' +
                    "<p>No articles found yet. Solve a problem and it becomes teachable knowledge.</p>" +
                    '<a class="btn btn-primary mt-2" href="submit-problem.html">Submit a Problem</a>' +
                    "</div></div>";
                return;
            }

            grid.innerHTML = items.map(function (p) {
                const id = p.problemId || p.ProblemId;
                const code = p.problemCode || p.ProblemCode || "";
                const title = p.title || p.Title || "Untitled";
                const summary = p.summary || p.Summary || "Structured debugging knowledge from a real developer issue.";
                const status = p.status || p.Status || "";
                return (
                    '<div class="col-md-6 col-lg-4">' +
                    '<a class="article-tile" href="article-details.html?id=' + encodeURIComponent(id) + '">' +
                    '<div class="d-flex justify-content-between align-items-center">' +
                    '<span class="badge-soft ai">' + escapeHtml(code) + "</span>" +
                    statusBadge(status) +
                    "</div>" +
                    "<h3>" + escapeHtml(title) + "</h3>" +
                    '<p class="text-gray mb-0">' + escapeHtml(summary) + "</p>" +
                    "</a></div>"
                );
            }).join("");
        } catch (e) {
            grid.innerHTML = '<div class="col-12"><div class="empty-state">Could not load articles. Check API connection.</div></div>';
        }
    }

    form.addEventListener("submit", function (e) {
        e.preventDefault();
        loadArticles(
            document.getElementById("search-q").value.trim(),
            document.getElementById("search-tech").value.trim()
        );
    });

    loadArticles("", "");
})();
