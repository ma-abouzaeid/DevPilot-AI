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

        root.innerHTML =
            '<div class="row g-4">' +
            '<div class="col-lg-4">' +
            '<div class="surface-card mb-3">' +
            '<span class="badge-soft ai mb-2">' + escapeHtml(difficulty) + "</span>" +
            '<h1 class="h3 mb-2">' + escapeHtml(title) + "</h1>" +
            '<p class="text-gray">' + escapeHtml(description) + "</p>" +
            '<p class="mb-0"><strong class="text-accent-brand"><i class="fa-solid fa-star"></i> ' + maxPoints + " pts max</strong></p>" +
            "</div>" +
            '<div class="surface-card">' +
            "<h2 class=\"h6 mb-3\">Requirements</h2>" +
            renderRequirements(requirements) +
            "</div></div>" +

            '<div class="col-lg-8">' +
            '<div class="surface-card mb-3">' +
            '<div class="d-flex justify-content-between align-items-center mb-2">' +
            '<label class="form-label mb-0" for="solution-code">Your solution</label>' +
            '<span class="ide-meta" id="code-meta">Ln 1 · Col 1</span>' +
            "</div>" +
            '<div class="ide-editor" id="ide-editor">' +
            '<div class="ide-toolbar"><div class="ide-dots"><span></span><span></span><span></span></div>' +
            '<span class="ide-filename">challenge.cs</span><span class="ide-lang">Editor</span></div>' +
            '<div class="ide-body"><pre class="ide-gutter" id="code-gutter" aria-hidden="true">1</pre>' +
            '<textarea class="ide-textarea" id="solution-code" spellcheck="false" wrap="off"></textarea>' +
            "</div></div>" +
            '<button type="button" class="btn btn-primary mt-3" id="btn-submit">' +
            '<i class="fa-solid fa-paper-plane"></i> Submit solution' +
            "</button></div>" +
            '<div class="surface-card d-none" id="result-card"><h2 class="h5 mb-3">AI Feedback</h2><div id="result-body"></div></div>' +
            "</div></div>";

        const textarea = document.getElementById("solution-code");
        const gutter = document.getElementById("code-gutter");
        const meta = document.getElementById("code-meta");
        textarea.value = starter;
        const refresh = function () { updateIdeChrome(textarea, gutter, meta); };
        textarea.addEventListener("input", refresh);
        textarea.addEventListener("click", refresh);
        textarea.addEventListener("keyup", refresh);
        textarea.addEventListener("scroll", function () { gutter.scrollTop = textarea.scrollTop; });
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
        refresh();

        document.getElementById("btn-submit").addEventListener("click", async function () {
            const code = textarea.value.trim();
            if (!code) {
                showAlert("Write your solution first.", "danger");
                return;
            }
            const btn = document.getElementById("btn-submit");
            btn.disabled = true;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Evaluating...';
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
                btn.disabled = false;
                btn.innerHTML = '<i class="fa-solid fa-paper-plane"></i> Submit solution';
            }
        });
    }

    function isAdminViewer() {
        return !!(window.DevPilotApi && typeof window.DevPilotApi.isAdmin === "function" && window.DevPilotApi.isAdmin());
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

    function renderResult(data) {
        const card = document.getElementById("result-card");
        const body = document.getElementById("result-body");
        const passed = data.isPassed || data.IsPassed;
        const points = data.pointsAwarded ?? data.PointsAwarded ?? 0;
        const max = data.maxPoints ?? data.MaxPoints ?? 0;
        const provider = data.providerUsed || data.ProviderUsed || "";
        const feedback = data.feedbackSummary || data.FeedbackSummary || "";
        const advice = data.advice || data.Advice || "";
        const details = data.detailedReport || data.DetailedReport || "";

        card.classList.remove("d-none");
        body.innerHTML =
            '<div class="challenge-feedback">' +
            '<div class="challenge-feedback-header">' +
            (passed
                ? '<span class="badge-soft solved">Passed</span>'
                : '<span class="badge-soft open">Needs work</span>') +
            '<span class="challenge-feedback-points"><i class="fa-solid fa-star"></i> ' +
            points + " / " + max + " pts</span>" +
            (isAdminViewer() && provider
                ? '<span class="challenge-feedback-provider">Provider: ' + escapeHtml(provider) + "</span>"
                : "") +
            "</div>" +

            (feedback
                ? '<section class="challenge-feedback-block"><h3>Feedback</h3><p>' + escapeHtml(feedback) + "</p></section>"
                : "") +
            (advice
                ? '<section class="challenge-feedback-block"><h3>Advice</h3><p>' + escapeHtml(advice) + "</p></section>"
                : "") +

            '<section class="challenge-feedback-block">' +
            "<h3>Scores</h3>" +
            '<div class="challenge-score-list">' +
            scoreRow("Correctness", data.correctnessScore ?? data.CorrectnessScore) +
            scoreRow("Code quality", data.codeQualityScore ?? data.CodeQualityScore) +
            scoreRow("Security", data.securityScore ?? data.SecurityScore) +
            "</div></section>" +

            (details
                ? '<section class="challenge-feedback-block"><h3>Details</h3><p>' + escapeHtml(details) + "</p></section>"
                : "") +

            '<div class="challenge-feedback-actions">' +
            '<a class="btn btn-outline-card" style="width:auto;" href="challenges.html">Back to challenges</a>' +
            "</div></div>";

        card.scrollIntoView({ behavior: "smooth", block: "start" });
    }

    load().catch(function (err) {
        root.innerHTML = '<div class="surface-card empty-state">' + escapeHtml(err.message || "Failed to load") + "</div>";
    });
})();
