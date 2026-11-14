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

    async function loadArticles(q, technology) {
        grid.innerHTML = '<div class="col-12"><div class="empty-state">Searching knowledge base…</div></div>';
        try {
            const url = new URL("/api/Problems/articles", API_BASE);
            url.searchParams.set("page", "1");
            url.searchParams.set("pageSize", "12");
            if (q) url.searchParams.set("q", q);
            if (technology) url.searchParams.set("technology", technology);

            const res = await window.DevPilotApi.request(url.href, { skipAuth: true });
            const items = (res.data && (res.data.items || res.data.Items)) || [];

            if (!items.length) {
                grid.innerHTML =
                    '<div class="col-12"><div class="empty-state surface-card">' +
                    "<p>No published articles yet. Solve a problem and it becomes teachable knowledge.</p>" +
                    '<a class="btn btn-primary mt-2" href="submit-problem.html">Submit a Problem</a>' +
                    "</div></div>";
                return;
            }

            grid.innerHTML = items.map(function (a) {
                const articleId = a.knowledgeArticleId || a.KnowledgeArticleId;
                const code = a.articleCode || a.ArticleCode || a.problemCode || a.ProblemCode || "";
                const title = a.title || a.Title || "Untitled";
                const summary = a.summary || a.Summary || "Structured debugging knowledge from a real developer issue.";
                const framework = a.framework || a.Framework || "";
                return (
                    '<div class="col-md-6 col-lg-4">' +
                    '<a class="article-tile" href="article-details.html?id=' + encodeURIComponent(articleId) + '">' +
                    '<div class="d-flex justify-content-between align-items-center">' +
                    '<span class="badge-soft ai">' + escapeHtml(code) + "</span>" +
                    '<span class="badge-soft solved">Published</span>' +
                    "</div>" +
                    "<h3>" + escapeHtml(title) + "</h3>" +
                    '<p class="text-gray mb-2">' + escapeHtml(summary) + "</p>" +
                    (framework ? '<div class="text-gray small">' + escapeHtml(framework) + "</div>" : "") +
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
