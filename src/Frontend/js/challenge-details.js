(function () {
    "use strict";

    if (!window.DevPilotApi || !window.DevPilotApi.isAuthenticated()) {
        window.location.href = "login.html?returnUrl=" + encodeURIComponent(window.location.pathname + window.location.search);
        return;
    }

    const root = document.getElementById("challenge-root");
    const alertBox = document.getElementById("page-alert");
    const params = new URLSearchParams(window.location.search);
    const id = params.get("id");
    const modeParam = (params.get("mode") || "").toLowerCase();
    const viewMode = modeParam === "view";
    const retryMode = modeParam === "retry" || params.get("retry") === "1";

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function showAlert(message, type) {
        alertBox.textContent = message;
        alertBox.className = "alert alert-" + (type || "danger") + " mt-3";
        alertBox.classList.remove("d-none");
    }

    function pick(obj) {
        if (!obj) return "";
        const keys = Array.prototype.slice.call(arguments, 1);
        for (let i = 0; i < keys.length; i++) {
            const value = obj[keys[i]];
            if (value != null && String(value).trim() !== "") return value;
        }
        return "";
    }

    function splitRequirements(raw) {
        const text = String(raw || "").replace(/\r\n/g, "\n").trim();
        if (!text) return ["Fix the bug and keep the behavior correct."];

        if (text.indexOf("\n") >= 0) {
            return text.split("\n")
                .map(function (line) { return line.replace(/^\d+\.\s*/, "").trim(); })
                .filter(Boolean);
        }

        const numbered = text.split(/(?=\d+\.\s+)/)
            .map(function (part) { return part.replace(/^\d+\.\s*/, "").trim(); })
            .filter(Boolean);

        if (numbered.length >= 2) return numbered;
        return [text];
    }

    function renderRequirements(raw) {
        const items = splitRequirements(raw);
        return '<ol class="challenge-requirements">' +
            items.map(function (item) {
                return "<li>" + escapeHtml(item) + "</li>";
            }).join("") +
            "</ol>";
    }

    function updateIdeChrome(textarea, gutter, meta) {
        const value = textarea.value;
        const lines = value.length ? value.split("\n").length : 1;
        let html = "";
        for (let i = 1; i <= lines; i++) html += i + (i < lines ? "\n" : "");
        gutter.textContent = html || "1";
        const pos = textarea.selectionStart || 0;
        const before = value.slice(0, pos);
        const row = before.split("\n").length;
        const col = before.length - before.lastIndexOf("\n");
        meta.textContent = "Ln " + row + " · Col " + col;
    }

    function scoreRow(label, value) {
        const score = Math.max(0, Math.min(100, Number(value) || 0));
        return (
            '<div class="challenge-score-row">' +
            '<div class="challenge-score-meta">' +
            "<span>" + escapeHtml(label) + "</span>" +
            "<strong>" + score + "</strong>" +
            "</div>" +
            '<div class="challenge-score-track" aria-hidden="true">' +
            '<span class="challenge-score-fill" style="width:' + score + '%"></span>' +
            "</div></div>"
        );
    }

    function normalizeAttempt(data, maxPointsFallback) {
        if (!data) return null;
        return {
            challengeAttemptId: data.challengeAttemptId || data.ChallengeAttemptId,
            isPassed: !!(data.isPassed || data.IsPassed),
            correctnessScore: data.correctnessScore ?? data.CorrectnessScore ?? 0,
            codeQualityScore: data.codeQualityScore ?? data.CodeQualityScore ?? 0,
            securityScore: data.securityScore ?? data.SecurityScore ?? 0,
            pointsAwarded: data.pointsAwarded ?? data.PointsAwarded ?? 0,
            maxPoints: data.maxPoints ?? data.MaxPoints ?? maxPointsFallback ?? 0,
            feedbackSummary: pick(data, "feedbackSummary", "FeedbackSummary"),
            advice: pick(data, "advice", "Advice"),
            detailedReport: pick(data, "detailedReport", "DetailedReport"),
            submittedCode: pick(data, "submittedCode", "SubmittedCode"),
            submittedAt: pick(data, "submittedAt", "SubmittedAt")
        };
    }

    function renderFeedbackHtml(data) {
        return (
            '<div class="challenge-feedback">' +
            '<div class="challenge-feedback-header">' +
            (data.isPassed
                ? '<span class="badge-soft solved">Passed</span>'
                : '<span class="badge-soft open">Needs work</span>') +
            '<span class="challenge-feedback-points"><i class="fa-solid fa-star"></i> ' +
            data.pointsAwarded + " / " + data.maxPoints + " pts</span>" +
            "</div>" +
            (data.feedbackSummary
                ? '<section class="challenge-feedback-block"><h3>Feedback</h3><p>' + escapeHtml(data.feedbackSummary) + "</p></section>"
                : "") +
            (data.advice
                ? '<section class="challenge-feedback-block"><h3>Advice</h3><p>' + escapeHtml(data.advice) + "</p></section>"
                : "") +
            '<section class="challenge-feedback-block">' +
            "<h3>Scores</h3>" +
            '<div class="challenge-score-list">' +
            scoreRow("Correctness", data.correctnessScore) +
            scoreRow("Code quality", data.codeQualityScore) +
            scoreRow("Security", data.securityScore) +
            "</div></section>" +
            (data.detailedReport
                ? '<section class="challenge-feedback-block"><h3>Details</h3><p>' + escapeHtml(data.detailedReport) + "</p></section>"
                : "") +
            '<div class="challenge-feedback-actions">' +
            '<a class="btn btn-outline-card" style="width:auto;" href="challenges.html">Back to challenges</a>' +
            '<a class="btn btn-primary" style="width:auto;" href="challenge-details.html?id=' + encodeURIComponent(id) + '&mode=retry">' +
            '<i class="fa-solid fa-rotate"></i> Retry challenge</a>' +
            "</div></div>"
        );
    }

    function renderResult(data) {
        const card = document.getElementById("result-card");
        const body = document.getElementById("result-body");
        if (!card || !body) return;
        card.classList.remove("d-none");
        body.innerHTML = renderFeedbackHtml(normalizeAttempt(data, data.maxPoints || data.MaxPoints));
        card.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    function wireEditor(textarea, gutter, meta, initialCode, readOnly) {
        textarea.value = initialCode || "";
        textarea.readOnly = !!readOnly;
        if (readOnly) {
            textarea.classList.add("is-readonly");
        }
        const refresh = function () { updateIdeChrome(textarea, gutter, meta); };
        textarea.addEventListener("input", refresh);
        textarea.addEventListener("click", refresh);
        textarea.addEventListener("keyup", refresh);
        textarea.addEventListener("scroll", function () { gutter.scrollTop = textarea.scrollTop; });
        if (!readOnly) {
            textarea.addEventListener("keydown", function (e) {
                if (e.key === "Tab") {
                    e.preventDefault();
                    const start = textarea.selectionStart;
                    const end = textarea.selectionEnd;
                    textarea.value = textarea.value.slice(0, start) + "    " + textarea.value.slice(end);
                    textarea.selectionStart = textarea.selectionEnd = start + 4;
                    refresh();
                }
            });
        }
        refresh();
    }

    async function load() {
        if (!id) {
            root.innerHTML = '<div class="surface-card empty-state">Missing challenge id.</div>';
            return;
        }

        const url = new URL("/api/Challenges/" + encodeURIComponent(id), API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        if (!res.ok || !res.data) throw new Error(res.message || "Challenge not found");

        const c = res.data;
        const title = c.title || c.Title;
        const description = c.description || c.Description || "";
        const requirements = c.requirements || c.Requirements || "";
        const starter = c.starterCode || c.StarterCode || "";
        const difficulty = c.difficulty || c.Difficulty || "Intermediate";
        const maxPoints = c.maxPoints ?? c.MaxPoints ?? 100;
        const solved = !!(c.solvedByCurrentUser || c.SolvedByCurrentUser);
        const best = c.bestPointsForCurrentUser ?? c.BestPointsForCurrentUser;
        const latestAttempt = normalizeAttempt(c.latestAttempt || c.LatestAttempt, maxPoints);
        const showReview = viewMode || (!retryMode && solved && latestAttempt);
        const editorCode = showReview && latestAttempt && latestAttempt.submittedCode
            ? latestAttempt.submittedCode
            : (retryMode && latestAttempt && latestAttempt.submittedCode
                ? latestAttempt.submittedCode
                : starter);

        root.innerHTML =
            '<div class="row g-4">' +
            '<div class="col-lg-4">' +
            '<div class="surface-card mb-3">' +
            '<div class="d-flex flex-wrap gap-2 mb-2">' +
            '<span class="badge-soft ai">' + escapeHtml(difficulty) + "</span>" +
            (solved ? '<span class="badge-soft solved">Solved</span>' : "") +
            "</div>" +
            '<h1 class="h3 mb-2">' + escapeHtml(title) + "</h1>" +
            '<p class="text-gray">' + escapeHtml(description) + "</p>" +
            '<p class="mb-0"><strong class="text-accent-brand"><i class="fa-solid fa-star"></i> ' + maxPoints + " pts max</strong>" +
            (best != null ? ' <span class="text-gray">· best ' + best + "</span>" : "") +
            "</p></div>" +
            '<div class="surface-card">' +
            "<h2 class=\"h6 mb-3\">Requirements</h2>" +
            renderRequirements(requirements) +
            "</div></div>" +

            '<div class="col-lg-8">' +
            '<div class="surface-card mb-3">' +
            '<div class="d-flex justify-content-between align-items-center mb-2">' +
            '<label class="form-label mb-0" for="solution-code">' +
            (showReview ? "Your submitted solution" : "Your solution") +
            "</label>" +
            '<span class="ide-meta" id="code-meta">Ln 1 · Col 1</span>' +
            "</div>" +
            '<div class="ide-editor" id="ide-editor">' +
            '<div class="ide-toolbar"><div class="ide-dots"><span></span><span></span><span></span></div>' +
            '<span class="ide-filename">challenge.cs</span><span class="ide-lang">' +
            (showReview ? "Review" : "Editor") +
            "</span></div>" +
            '<div class="ide-body"><pre class="ide-gutter" id="code-gutter" aria-hidden="true">1</pre>' +
            '<textarea class="ide-textarea" id="solution-code" spellcheck="false" wrap="off"></textarea>' +
            "</div></div>" +
            (showReview
                ? '<div class="d-flex flex-wrap gap-2 mt-3">' +
                  '<a class="btn btn-primary" style="width:auto;" href="challenge-details.html?id=' + encodeURIComponent(id) + '&mode=retry">' +
                  '<i class="fa-solid fa-rotate"></i> Retry challenge</a>' +
                  '<a class="btn btn-outline-card" style="width:auto;" href="challenges.html">Back to list</a>' +
                  "</div>"
                : '<button type="button" class="btn btn-primary mt-3" id="btn-submit">' +
                  '<i class="fa-solid fa-paper-plane"></i> Submit solution</button>') +
            "</div>" +
            '<div class="surface-card' + (showReview && latestAttempt ? "" : " d-none") + '" id="result-card">' +
            "<h2 class=\"h5 mb-3\">AI Feedback</h2><div id=\"result-body\"></div></div>" +
            "</div></div>";

        const textarea = document.getElementById("solution-code");
        const gutter = document.getElementById("code-gutter");
        const meta = document.getElementById("code-meta");
        wireEditor(textarea, gutter, meta, editorCode, showReview);

        if (showReview && latestAttempt) {
            document.getElementById("result-body").innerHTML = renderFeedbackHtml(latestAttempt);
            document.getElementById("result-card").classList.remove("d-none");
        }

        const btnSubmit = document.getElementById("btn-submit");
        if (btnSubmit) {
            btnSubmit.addEventListener("click", async function () {
                const code = textarea.value.trim();
                if (!code) {
                    showAlert("Write your solution first.", "danger");
                    return;
                }
                btnSubmit.disabled = true;
                btnSubmit.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Evaluating...';
                try {
                    const submitUrl = new URL("/api/Challenges/" + encodeURIComponent(id) + "/submit", API_BASE).href;
                    const submitRes = await window.DevPilotApi.request(submitUrl, {
                        method: "POST",
                        body: JSON.stringify({ submittedCode: code })
                    });
                    if (!submitRes.ok || !submitRes.data) throw new Error(submitRes.message || "Evaluation failed");
                    renderResult(submitRes.data);
                } catch (err) {
                    showAlert(err.message || "Evaluation failed", "danger");
                } finally {
                    btnSubmit.disabled = false;
                    btnSubmit.innerHTML = '<i class="fa-solid fa-paper-plane"></i> Submit solution';
                }
            });
        }
    }

    if (window.DevPilotApi && typeof window.DevPilotApi.refreshRolesFromProfile === "function") {
        window.DevPilotApi.refreshRolesFromProfile().catch(function () { /* ignore */ });
    }

    load().catch(function (err) {
        root.innerHTML = '<div class="surface-card empty-state">' + escapeHtml(err.message || "Failed to load") + "</div>";
    });
})();
