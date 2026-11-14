(function () {
    "use strict";

    const root = document.getElementById("article-root");
    const params = new URLSearchParams(window.location.search);
    const id = params.get("id");
    const problemId = params.get("problemId");
    const code = params.get("code");

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function formatDate(value) {
        if (!value) return "";
        try { return new Date(value).toLocaleString(); } catch { return String(value); }
    }

    function renderMarkdownLite(markdown) {
        const text = String(markdown || "");
        const escaped = escapeHtml(text);
        return escaped
            .replace(/^### (.+)$/gm, "<h3>$1</h3>")
            .replace(/^## (.+)$/gm, "<h2>$1</h2>")
            .replace(/^# (.+)$/gm, "<h2>$1</h2>")
            .replace(/\n\n/g, "</p><p>")
            .replace(/\n/g, "<br>");
    }

    async function load() {
        const key = id || problemId || code;
        if (!key) {
            root.innerHTML = '<div class="surface-card empty-state">Missing article id.</div>';
            return;
        }

        try {
            const url = new URL("/api/Problems/articles/" + encodeURIComponent(key), API_BASE).href;
            const res = await window.DevPilotApi.request(url, { skipAuth: true });
            if (!res.ok || !res.data) {
                throw new Error(res.message || "Article not found");
            }

            const data = res.data;
            const title = data.title || data.Title || "Knowledge article";
            const articleCode = data.articleCode || data.ArticleCode || "";
            const problemCode = data.problemCode || data.ProblemCode || "";
            const status = data.status || data.Status || "Solved";
            const framework = data.framework || data.Framework || "";
            const summary = data.summary || data.Summary || "";
            const rootCause = data.rootCause || data.RootCause || "";
            const content = data.contentMarkdown || data.ContentMarkdown || summary;

            root.innerHTML =
                '<div class="surface-card mb-4">' +
                '<div class="d-flex flex-wrap gap-2 mb-3">' +
                '<span class="badge-soft ai">' + escapeHtml(articleCode || problemCode) + "</span>" +
                '<span class="badge-soft solved">' + escapeHtml(status) + "</span>" +
                (framework ? '<span class="badge-soft progress">' + escapeHtml(framework) + "</span>" : "") +
                "</div>" +
                '<h1 class="page-title mb-2">' + escapeHtml(title) + "</h1>" +
                '<p class="text-gray mb-0">Updated ' + escapeHtml(formatDate(data.resolvedAt || data.ResolvedAt || data.createdAt || data.CreatedAt)) + "</p>" +
                "</div>" +

                '<div class="row g-4">' +
                '<div class="col-lg-8">' +
                '<article class="surface-card content-prose">' +
                (summary ? "<h2>Summary</h2><p>" + escapeHtml(summary) + "</p>" : "") +
                (rootCause ? "<h2>Root Cause</h2><p>" + escapeHtml(rootCause) + "</p>" : "") +
                "<h2>Article</h2><div><p>" + renderMarkdownLite(content) + "</p></div>" +
                "</article>" +
                "</div>" +
                '<div class="col-lg-4">' +
                '<div class="surface-card mb-3">' +
                "<h3 class=\"h5\">Practice this fix</h3>" +
                '<p class="text-gray">Take a short challenge to apply the solution yourself.</p>' +
                '<a class="btn btn-primary w-100 mb-2" href="challenges.html">Start Challenge</a>' +
                '<a class="btn btn-outline-card w-100" href="submit-problem.html">Debug your own issue</a>' +
                "</div>" +
                '<div class="alert alert-success-soft">' +
                '<i class="fa-solid fa-circle-check text-success-brand me-2"></i>' +
                "Every solved problem becomes searchable knowledge for the next developer." +
                "</div>" +
                "</div>" +
                "</div>";
        } catch (e) {
            root.innerHTML = '<div class="surface-card empty-state">' + escapeHtml(e.message || "Failed to load article") + "</div>";
        }
    }

    load();
})();