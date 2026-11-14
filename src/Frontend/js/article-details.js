(function () {
    "use strict";

    const root = document.getElementById("article-root");
    const params = new URLSearchParams(window.location.search);
    const id = params.get("id");
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

    async function load() {
        if (!id && !code) {
            root.innerHTML = '<div class="surface-card empty-state">Missing article id.</div>';
            return;
        }

        try {
            const endpoint = id
                ? "/api/Problems/" + encodeURIComponent(id) + "/history"
                : "/api/Problems/code/" + encodeURIComponent(code);
            const url = new URL(endpoint, API_BASE).href;
            const res = await window.DevPilotApi.request(url, { skipAuth: true });
            if (!res.ok || !res.data) {
                throw new Error(res.message || "Article not found");
            }

            const data = res.data;
            const title = data.title || data.Title || "Problem details";
            const problemCode = data.problemCode || data.ProblemCode || "";
            const status = data.status || data.Status || "";
            const attempts = data.attempts || data.Attempts || [];
            const solutions = data.solutions || data.Solutions || [];
            const latestAttempt = attempts[attempts.length - 1];
            const latestSolution = solutions[solutions.length - 1];

            const rootCause = (latestSolution && (latestSolution.rootCause || latestSolution.RootCause))
                || (latestAttempt && (latestAttempt.rootCause || latestAttempt.RootCause))
                || "Root cause will appear after AI analysis.";
            const steps = (latestSolution && (latestSolution.detailedSteps || latestSolution.DetailedSteps))
                || (latestAttempt && (latestAttempt.proposedSolution || latestAttempt.ProposedSolution))
                || "Solution steps will appear after AI analysis.";
            const explanation = (latestAttempt && (latestAttempt.explanation || latestAttempt.Explanation)) || "";

            root.innerHTML =
                '<div class="surface-card mb-4">' +
                '<div class="d-flex flex-wrap gap-2 mb-3">' +
                '<span class="badge-soft ai">' + escapeHtml(problemCode) + "</span>" +
                '<span class="badge-soft ' + ((status || "").toLowerCase() === "solved" ? "solved" : "progress") + '">' + escapeHtml(status || "Unknown") + "</span>" +
                "</div>" +
                '<h1 class="page-title mb-2">' + escapeHtml(title) + "</h1>" +
                '<p class="text-gray mb-0">Updated ' + escapeHtml(formatDate(data.resolvedAt || data.ResolvedAt || data.createdAt || data.CreatedAt)) + "</p>" +
                "</div>" +

                '<div class="row g-4">' +
                '<div class="col-lg-8">' +
                '<article class="surface-card content-prose">' +
                "<h2>1. Problem</h2><p>" + escapeHtml(title) + "</p>" +
                "<h2>2. Location</h2><p>" + escapeHtml(explanation || "See debugging attempt details.") + "</p>" +
                "<h2>3. Root Cause</h2><p>" + escapeHtml(rootCause) + "</p>" +
                "<h2>4. Solution</h2><pre class=\"result-code\"><code>" + escapeHtml(steps) + "</code></pre>" +
                "</article>" +
                "</div>" +
                '<div class="col-lg-4">' +
                '<div class="surface-card mb-3">' +
                "<h3 class=\"h5\">Practice this fix</h3>" +
                '<p class="text-gray">Take a short challenge to apply the solution yourself.</p>' +
                '<a class="btn btn-primary w-100 mb-2" href="challenges.html">Start Challenge</a>' +
                '<a class="btn btn-outline-light w-100" href="submit-problem.html">Debug your own issue</a>' +
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
