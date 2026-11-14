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
        if (s === "inprogress" || s === "in_progress") return '<span class="badge-soft status-progress">In Progress</span>';
        return '<span class="badge-soft open">Open</span>';
    }

    function pick(obj) {
        const keys = Array.prototype.slice.call(arguments, 1);
        for (let i = 0; i < keys.length; i++) {
            const value = obj[keys[i]];
            if (value != null && String(value).trim() !== "") return value;
        }
        return "";
    }

    function formatSourceCode(raw) {
        if (!raw) return "";
        let text = String(raw).replace(/\r\n/g, "\n").trim();
        const newlineCount = (text.match(/\n/g) || []).length;
        if (newlineCount >= 2) return text;

        let out = "";
        let indent = 0;
        let inString = false;
        let stringQuote = "";
        let escaped = false;

        function newline() {
            out += "\n" + "    ".repeat(Math.max(indent, 0));
        }

        for (let i = 0; i < text.length; i++) {
            const ch = text[i];
            if (inString) {
                out += ch;
                if (escaped) escaped = false;
                else if (ch === "\\") escaped = true;
                else if (ch === stringQuote) inString = false;
                continue;
            }
            if (ch === "\"" || ch === "'") {
                inString = true;
                stringQuote = ch;
                out += ch;
                continue;
            }
            if (ch === "{") {
                out = out.replace(/[ \t]+$/, "");
                out += " {";
                indent += 1;
                newline();
                continue;
            }
            if (ch === "}") {
                indent = Math.max(indent - 1, 0);
                out = out.replace(/[ \t]+$/, "");
                newline();
                out += "}";
                const next = text[i + 1];
                if (next && next !== ";" && next !== ")" && next !== "," && next !== "}") newline();
                continue;
            }
            if (ch === ";") {
                out += ";";
                const next = text[i + 1];
                if (next && next !== "}" && next !== ")") newline();
                continue;
            }
            out += ch;
        }

        return out
            .split("\n")
            .map(function (line) { return line.replace(/[ \t]+$/g, ""); })
            .join("\n")
            .replace(/\n{3,}/g, "\n\n")
            .trim();
    }

    function ideBlock(label, code, filename) {
        if (!code || !String(code).trim()) return "";
        const formatted = formatSourceCode(code);
        const lines = formatted.length ? formatted.split("\n") : [" "];
        let gutter = "";
        for (let i = 1; i <= lines.length; i++) gutter += i + (i < lines.length ? "\n" : "");

        return (
            '<div class="problem-section">' +
            (label ? '<h3 class="problem-section-title">' + escapeHtml(label) + "</h3>" : "") +
            '<div class="ide-editor ide-readonly article-ide">' +
            '<div class="ide-toolbar">' +
            '<div class="ide-dots" aria-hidden="true"><span></span><span></span><span></span></div>' +
            '<span class="ide-filename">' + escapeHtml(filename || "snippet.txt") + "</span>" +
            "</div>" +
            '<div class="ide-body ide-body-readonly">' +
            '<pre class="ide-gutter" aria-hidden="true">' + gutter + "</pre>" +
            '<pre class="ide-code"><code>' + escapeHtml(formatted) + "</code></pre>" +
            "</div></div></div>"
        );
    }

    function textSection(label, text) {
        if (!text || !String(text).trim()) return "";
        return (
            '<div class="problem-section">' +
            '<h3 class="problem-section-title">' + escapeHtml(label) + "</h3>" +
            '<p class="problem-section-text">' + escapeHtml(text) + "</p>" +
            "</div>"
        );
    }

    function parseExplanation(explanation) {
        const text = String(explanation || "").replace(/\r\n/g, "\n").trim();
        if (!text) return { problem: "", location: "" };
        const lines = text.split("\n");
        let locationIndex = -1;
        for (let i = 0; i < lines.length; i++) {
            if (/^Location:\s*/i.test(lines[i])) {
                locationIndex = i;
                break;
            }
        }
        if (locationIndex >= 0) {
            return {
                problem: lines.slice(0, locationIndex).join("\n").trim(),
                location: lines[locationIndex].replace(/^Location:\s*/i, "").trim()
            };
        }
        return { problem: text, location: "" };
    }

    function parseProposedSolution(proposed) {
        const text = String(proposed || "").replace(/\r\n/g, "\n").trim();
        if (!text) return { summary: "", steps: [], code: "" };

        let body = text;
        let code = "";
        const codeMarker = /Corrected code:\s*/i;
        const codeMatch = text.search(codeMarker);
        if (codeMatch >= 0) {
            const after = text.slice(codeMatch).replace(codeMarker, "");
            code = after.trim();
            body = text.slice(0, codeMatch).trim();
        }

        const lines = body.split("\n");
        const summaryLines = [];
        const steps = [];
        let inSteps = false;

        lines.forEach(function (rawLine) {
            const line = rawLine.trim();
            const stepMatch = line.match(/^(\d+)\.\s*([^:]+):\s*(.*)$/);
            if (stepMatch) {
                inSteps = true;
                steps.push({
                    order: Number(stepMatch[1]),
                    title: stepMatch[2].trim(),
                    detail: stepMatch[3].trim()
                });
                return;
            }
            if (!inSteps) {
                if (line) summaryLines.push(line);
            } else if (steps.length && line) {
                steps[steps.length - 1].detail += " " + line;
            }
        });

        return {
            summary: summaryLines.join(" ").trim(),
            steps: steps,
            code: code
        };
    }

    function renderSteps(steps) {
        if (!steps || !steps.length) return "";
        return (
            '<div class="problem-section">' +
            '<h3 class="problem-section-title">Steps</h3>' +
            '<ol class="article-steps">' +
            steps.map(function (step) {
                return (
                    "<li><strong>" + escapeHtml(step.title || ("Step " + step.order)) + "</strong>" +
                    (step.detail ? "<br>" + escapeHtml(step.detail) : "") +
                    "</li>"
                );
            }).join("") +
            "</ol></div>"
        );
    }

    function renderRequestCard(r, index) {
        const framework = pick(r, "framework", "Framework");
        const description = pick(r, "description", "Description");
        const errorMessage = pick(r, "errorMessage", "ErrorMessage");
        const stackTrace = pick(r, "stackTrace", "StackTrace");
        const sourceCode = pick(r, "sourceCode", "SourceCode");
        const logs = pick(r, "logs", "Logs");

        return (
            '<article class="surface-card problem-history-card mb-3">' +
            '<div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">' +
            '<h3 class="h6 mb-0">Request #' + (index + 1) + "</h3>" +
            '<span class="text-gray small">' + escapeHtml(formatDate(pick(r, "createdAt", "CreatedAt"))) + "</span>" +
            "</div>" +
            (framework ? '<div class="mb-2"><span class="badge-soft status-progress">' + escapeHtml(framework) + "</span></div>" : "") +
            textSection("Notes", description) +
            (errorMessage
                ? '<div class="problem-section"><h3 class="problem-section-title">Error</h3>' +
                  '<pre class="problem-error-box">' + escapeHtml(errorMessage) + "</pre></div>"
                : "") +
            (stackTrace
                ? '<div class="problem-section"><h3 class="problem-section-title">Stack trace</h3>' +
                  '<pre class="problem-error-box">' + escapeHtml(stackTrace) + "</pre></div>"
                : "") +
            ideBlock("Source code", sourceCode, "source.txt") +
            ideBlock("Logs", logs, "logs.txt") +
            "</article>"
        );
    }

    function renderAttemptCard(a) {
        const rootCause = pick(a, "rootCause", "RootCause", "rootCauseAnalysis", "RootCauseAnalysis");
        const explanation = pick(a, "explanation", "Explanation");
        const proposed = pick(a, "proposedSolution", "ProposedSolution");
        const parsedExplain = parseExplanation(explanation);
        const parsedSolution = parseProposedSolution(proposed);

        return (
            '<article class="surface-card problem-history-card mb-3">' +
            '<div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">' +
            '<h3 class="h6 mb-0">AI attempt #' + escapeHtml(pick(a, "attemptNumber", "AttemptNumber") || "1") + "</h3>" +
            '<span class="text-gray small">' + escapeHtml(formatDate(pick(a, "createdAt", "CreatedAt"))) + "</span>" +
            "</div>" +
            textSection("Problem", parsedExplain.problem) +
            textSection("Location", parsedExplain.location) +
            textSection("Root cause", rootCause) +
            textSection("Solution summary", parsedSolution.summary) +
            renderSteps(parsedSolution.steps) +
            ideBlock("Corrected code", parsedSolution.code, "fixed.txt") +
            (!parsedSolution.summary && !parsedSolution.steps.length && !parsedSolution.code && proposed
                ? textSection("Proposed solution", proposed)
                : "") +
            "</article>"
        );
    }

    function renderSolutionCard(s) {
        const detailed = pick(s, "detailedSteps", "DetailedSteps");
        const parsed = parseProposedSolution(detailed);
        return (
            '<article class="surface-card problem-history-card mb-3">' +
            '<h3 class="h6 mb-3">Confirmed solution</h3>' +
            textSection("Root cause", pick(s, "rootCause", "RootCause")) +
            textSection("Summary", pick(s, "summary", "Summary")) +
            textSection("Solution summary", parsed.summary) +
            renderSteps(parsed.steps) +
            ideBlock("Corrected code", parsed.code, "fixed.txt") +
            (!parsed.summary && !parsed.steps.length && !parsed.code && detailed
                ? textSection("Steps", detailed)
                : "") +
            "</article>"
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
        const problemId = pick(d, "problemId", "ProblemId");
        const title = pick(d, "title", "Title") || "Problem";
        const code = pick(d, "problemCode", "ProblemCode");
        const status = pick(d, "status", "Status");
        const summary = pick(d, "summary", "Summary");
        const isOwner = d.isOwner || d.IsOwner;
        const requests = d.requests || d.Requests || [];
        const attempts = d.attempts || d.Attempts || [];
        const solutions = d.solutions || d.Solutions || [];
        const solved = String(status).toLowerCase() === "solved";

        const actions = [];
        if (isOwner && !solved) {
            actions.push('<a class="btn btn-primary" style="width:auto;" href="submit-problem.html?problemId=' + encodeURIComponent(problemId) + '">Continue debugging</a>');
        }
        if (solved) {
            actions.push('<a class="btn btn-primary" style="width:auto;" href="article-details.html?problemId=' + encodeURIComponent(problemId) + '">Open article</a>');
            actions.push('<a class="btn btn-outline-card" style="width:auto;" href="challenges.html">Practice challenges</a>');
        }
        actions.push('<a class="btn btn-outline-card" style="width:auto;" href="dashboard.html">Back to dashboard</a>');

        root.innerHTML =
            '<div class="surface-card mb-4">' +
            '<div class="d-flex flex-wrap gap-2 mb-3">' +
            '<span class="badge-soft ai">' + escapeHtml(code) + "</span>" +
            statusBadge(status) +
            (isOwner ? '<span class="badge-soft status-progress">Your ticket</span>' : "") +
            "</div>" +
            '<h1 class="page-title mb-2">' + escapeHtml(title) + "</h1>" +
            (summary ? '<p class="page-lead mb-2">' + escapeHtml(summary) + "</p>" : "") +
            '<p class="text-gray small mb-3">Opened ' + escapeHtml(formatDate(pick(d, "createdAt", "CreatedAt"))) +
            (pick(d, "resolvedAt", "ResolvedAt") ? " · Resolved " + escapeHtml(formatDate(pick(d, "resolvedAt", "ResolvedAt"))) : "") +
            "</p>" +
            '<div class="d-flex flex-wrap gap-2">' + actions.join("") + "</div>" +
            "</div>" +

            '<div class="row g-4">' +
            '<div class="col-lg-6">' +
            '<div class="surface-card mb-0">' +
            '<h2 class="h5 mb-3">Submission history</h2>' +
            (requests.length
                ? requests.map(renderRequestCard).join("")
                : '<div class="empty-state">No requests yet.</div>') +
            "</div></div>" +
            '<div class="col-lg-6">' +
            '<div class="surface-card mb-0">' +
            '<h2 class="h5 mb-3">AI debug trail</h2>' +
            (attempts.length
                ? attempts.map(renderAttemptCard).join("")
                : '<div class="empty-state">No AI attempts yet.</div>') +
            (solutions.length ? solutions.map(renderSolutionCard).join("") : "") +
            "</div></div>" +
            "</div>";
    }

    load().catch(function (err) {
        root.innerHTML = '<div class="surface-card empty-state">' + escapeHtml(err.message || "Failed to load") + "</div>";
    });
})();
