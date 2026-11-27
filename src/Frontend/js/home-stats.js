(function () {
    "use strict";

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function formatStat(value) {
        const n = Number(value) || 0;
        if (n >= 10000) return Math.floor(n / 1000) + "K+";
        if (n >= 1000) return (Math.round(n / 100) / 10) + "K+";
        return String(n);
    }

    function formatDate(value) {
        if (!value) return "";
        try {
            return new Date(value).toLocaleDateString(undefined, {
                year: "numeric",
                month: "short",
                day: "numeric"
            });
        } catch {
            return String(value);
        }
    }

    function techTagClass(framework) {
        const f = String(framework || "").toLowerCase();
        if (f.includes("react") || f.includes("node") || f.includes("js")) return "tag-green";
        if (f.includes("sql") || f.includes("postgres") || f.includes("mysql")) return "tag-orange";
        if (f.includes("docker") || f.includes("linux") || f.includes("python")) return "tag-blue";
        return "tag-purple";
    }

    function techLabel(framework) {
        const raw = String(framework || "").trim();
        if (!raw) return "General";
        return raw.length > 18 ? raw.slice(0, 18) + "…" : raw;
    }

    function truncate(text, max) {
        const value = String(text || "").trim();
        if (value.length <= max) return value;
        return value.slice(0, max - 1).trimEnd() + "…";
    }

    async function loadStats() {
        const nodes = {
            developers: document.getElementById("stat-developers"),
            solved: document.getElementById("stat-problems-solved"),
            articles: document.getElementById("stat-articles"),
            challenges: document.getElementById("stat-challenges")
        };
        if (!nodes.developers) return;

        try {
            const url = new URL("/api/Problems/stats", API_BASE).href;
            const res = await window.DevPilotApi.request(url, { skipAuth: true });
            if (!res.ok || !res.data) return;
            const d = res.data;
            nodes.developers.textContent = formatStat(d.developers ?? d.Developers);
            nodes.solved.textContent = formatStat(d.problemsSolved ?? d.ProblemsSolved);
            nodes.articles.textContent = formatStat(d.knowledgeArticles ?? d.KnowledgeArticles);
            nodes.challenges.textContent = formatStat(d.codingChallenges ?? d.CodingChallenges);
        } catch (e) {
            // Leave default placeholders on fetch error.
        }
    }

    async function loadFeaturedArticles() {
        const grid = document.getElementById("featured-articles");
        if (!grid) return;

        try {
            const url = new URL("/api/Problems/articles", API_BASE);
            url.searchParams.set("page", "1");
            url.searchParams.set("pageSize", "4");
            const res = await window.DevPilotApi.request(url.href, { skipAuth: true });
            const items = (res.data && (res.data.items || res.data.Items)) || [];

            if (!items.length) {
                grid.innerHTML =
                    '<div class="col-12"><div class="empty-state surface-card">' +
                    "<p>No published articles yet. Solve a bug and it becomes searchable knowledge.</p>" +
                    '<a class="btn btn-primary mt-2" href="submit-problem.html">Debug a problem</a>' +
                    "</div></div>";
                return;
            }

            grid.innerHTML = items.map(function (a) {
                const articleId = a.knowledgeArticleId || a.KnowledgeArticleId;
                const code = a.articleCode || a.ArticleCode || a.problemCode || a.ProblemCode || "";
                const title = a.title || a.Title || "Untitled";
                const summary = a.summary || a.Summary || "Structured debugging knowledge from a real developer issue.";
                const framework = a.framework || a.Framework || "";
                const resolved = a.resolvedAt || a.ResolvedAt || a.createdAt || a.CreatedAt;
                return (
                    '<div class="col article-col">' +
                    '<a href="article-details.html?id=' + encodeURIComponent(articleId) + '" class="article-card">' +
                    '<div class="article-top">' +
                    '<span class="tech-tag ' + techTagClass(framework) + '">' + escapeHtml(techLabel(framework)) + "</span>" +
                    '<span class="article-code">' + escapeHtml(code) + "</span>" +
                    "<h6>" + escapeHtml(truncate(title, 72)) + "</h6>" +
                    "</div>" +
                    '<div class="article-body">' +
                    "<p>" + escapeHtml(truncate(summary, 110)) + "</p>" +
                    '<div class="article-footer d-flex justify-content-between align-items-center">' +
                    '<small class="text-muted"><i class="fa-regular fa-calendar"></i> ' +
                    escapeHtml(formatDate(resolved) || "Published") +
                    "</small>" +
                    '<span class="level level-easy">Published</span>' +
                    "</div></div></a></div>"
                );
            }).join("");
        } catch (e) {
            grid.innerHTML =
                '<div class="col-12"><div class="empty-state">Could not load articles right now.</div></div>';
        }
    }

    if (typeof API_BASE === "undefined" || !window.DevPilotApi) return;
    loadStats();
    loadFeaturedArticles();
})();
