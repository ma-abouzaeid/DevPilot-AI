(function () {
    "use strict";

    const root = document.getElementById("problem-root");
    const params = new URLSearchParams(window.location.search);
    const id = params.get("id") || params.get("problemId");

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

    function statusBadge(status) {
        const s = (status || "").toLowerCase();
        if (s === "solved") return '<span class="badge-soft solved">Solved</span>';
        if (s === "inprogress" || s === "in_progress") return '<span class="badge-soft progress">In Progress</span>';
        return '<span class="badge-soft open">Open</span>';
    }

    function preBlock(label, text) {
        if (!text) return "";
        return (
            "<h3 class=\"h6 mt-3\">" + escapeHtml(label) + "</h3>" +
            '<pre class="code-panel mb-0" style="white-space:pre-wrap;max-height:320px;overflow:auto;">' +
            escapeHtml(text) +
            "</pre>"
        );
    }

    async function load() {
        if (!id) {
            root.innerHTML = '<div class="surface-card empty-state">Missing problem id.</div>';
            return;
        }

        const url = new URL("/api/Problems/" + encodeURIComponent(id) + "/history", API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        if (!res.ok || !res.data) throw new Error(res.message || "Problem not found");

        const d = res.data;
        const problemId = d.problemId || d.ProblemId;
        const title = d.title || d.Title || "Problem";
        const code = d.problemCode || d.ProblemCode || "";
        const status = d.status || d.Status || "";
        const summary = d.summary || d.Summary || "";
        const isOwner = d.isOwner || d.IsOwner;
        const requests = d.requests || d.Requests || [];
        const attempts = d.attempts || d.Attempts || [];
        const solutions = d.solutions || d.Solutions || [];
        const solved = String(status).toLowerCase() === "solved";

        const actions = [];
        if (isOwner && !solved) {
            actions.push('<a class="btn btn-primary" href="submit-problem.html?problemId=' + encodeURIComponent(problemId) + '">Continue debugging</a>');
        }
        if (solved) {
            actions.push('<a class="btn btn-primary" href="article-details.html?problemId=' + encodeURIComponent(problemId) + '">Open article</a>');
            actions.push('<a class="btn btn-outline-card" href="challenges.html">Practice challenges</a>');
        }
        if (isOwner) {
            actions.push('<a class="btn btn-outline-card" href="dashboard.html">Back to dashboard</a>');
        }

        const requestHtml = requests.length
            ? requests.map(function (r, index) {
                return (
                    '<div class="surface-card mb-3">' +
                    '<div class="d-flex justify-content-between align-items-center mb-2">' +
                    "<h3 class=\"h6 mb-0\">Request #" + (index + 1) + "</h3>" +
                    '<span class="text-gray small">' + escapeHtml(formatDate(r.createdAt || r.CreatedAt)) + "</span>" +
                    "</div>" +
                    (r.framework || r.Framework ? '<span class="badge-soft progress mb-2">' + escapeHtml(r.framework || r.Framework) + "</span>" : "") +
                    (r.description || r.Description ? "<p>" + escapeHtml(r.description || r.Description) + "</p>" : "") +
                    preBlock("Error", r.errorMessage || r.ErrorMessage) +
                    preBlock("Stack trace", r.stackTrace || r.StackTrace) +
                    preBlock("Source code", r.sourceCode || r.SourceCode) +
                    preBlock("Logs", r.logs || r.Logs) +
                    "</div>"
                );
            }).join("")
            : '<div class="empty-state">No requests yet.</div>';

        const attemptHtml = attempts.length
            ? attempts.map(function (a) {
                return (
                    '<div class="surface-card mb-3">' +
                    '<div class="d-flex justify-content-between align-items-center mb-2">' +
                    "<h3 class=\"h6 mb-0\">AI attempt #" + escapeHtml(a.attemptNumber || a.AttemptNumber) + "</h3>" +
                    '<span class="text-gray small">' + escapeHtml(formatDate(a.createdAt || a.CreatedAt)) + "</span>" +
                    "</div>" +
                    "<p><strong>Root cause</strong><br>" + escapeHtml(a.rootCauseAnalysis || a.RootCauseAnalysis || "—") + "</p>" +
                    "<p><strong>Explanation</strong><br>" + escapeHtml(a.explanation || a.Explanation || "—") + "</p>" +
                    "<p class=\"mb-0\"><strong>Proposed solution</strong><br>" + escapeHtml(a.proposedSolution || a.ProposedSolution || "—") + "</p>" +
                    "</div>"
                );
            }).join("")
            : '<div class="empty-state">No AI attempts yet.</div>';

        const solutionHtml = solutions.length
            ? solutions.map(function (s) {
                return (
                    '<div class="surface-card mb-3">' +
                    "<h3 class=\"h6\">Confirmed solution</h3>" +
                    "<p><strong>Root cause</strong><br>" + escapeHtml(s.rootCause || s.RootCause || "—") + "</p>" +
                    "<p><strong>Summary</strong><br>" + escapeHtml(s.summary || s.Summary || "—") + "</p>" +
                    "<p class=\"mb-0\"><strong>Steps</strong><br>" + escapeHtml(s.detailedSteps || s.DetailedSteps || "—") + "</p>" +
                    "</div>"
                );
            }).join("")
            : "";

        root.innerHTML =
            '<div class="surface-card mb-4">' +
            '<div class="d-flex flex-wrap gap-2 mb-3">' +
            '<span class="badge-soft ai">' + escapeHtml(code) + "</span>" +
            statusBadge(status) +
            (isOwner ? '<span class="badge-soft progress">Your ticket</span>' : "") +
            "</div>" +
            '<h1 class="page-title mb-2">' + escapeHtml(title) + "</h1>" +
            (summary ? '<p class="text-gray">' + escapeHtml(summary) + "</p>" : "") +
            '<p class="text-gray small mb-3">Opened ' + escapeHtml(formatDate(d.createdAt || d.CreatedAt)) +
            (d.resolvedAt || d.ResolvedAt ? " · Resolved " + escapeHtml(formatDate(d.resolvedAt || d.ResolvedAt)) : "") +
            "</p>" +
            (actions.length ? '<div class="d-flex flex-wrap gap-2">' + actions.join("") + "</div>" : "") +
            "</div>" +

            '<div class="row g-4">' +
            '<div class="col-lg-6">' +
            '<h2 class="h5 mb-3">Submission history</h2>' + requestHtml +
            "</div>" +
            '<div class="col-lg-6">' +
            '<h2 class="h5 mb-3">AI debug trail</h2>' + attemptHtml + solutionHtml +
            "</div>" +
            "</div>";
    }

    load().catch(function (err) {
        root.innerHTML = '<div class="surface-card empty-state">' + escapeHtml(err.message || "Failed to load") + "</div>";
    });
})();
