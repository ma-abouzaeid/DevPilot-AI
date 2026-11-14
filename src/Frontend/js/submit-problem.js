(function () {
    "use strict";

    const form = document.getElementById("problem-form");
    const alertBox = document.getElementById("form-alert");
    const resultPanel = document.getElementById("result-panel");
    const submitBtn = document.getElementById("submit-btn");
    const feedbackMessage = document.getElementById("feedback-message");
    const sourceCode = document.getElementById("sourceCode");
    const gutter = document.getElementById("code-gutter");
    const codeMeta = document.getElementById("code-meta");
    const ideBody = document.querySelector(".ide-body");
    const SUBMIT_URL = new URL("/api/Problems", API_BASE).href;

    let lastResult = null;

    function showAlert(message, type) {
        alertBox.textContent = message;
        alertBox.className = "alert alert-" + (type || "danger");
        alertBox.classList.remove("d-none");
    }

    function hideAlert() {
        alertBox.classList.add("d-none");
    }

    function requireAuth() {
        if (!window.DevPilotApi || !window.DevPilotApi.isAuthenticated()) {
            window.location.href = "login.html?returnUrl=" + encodeURIComponent("submit-problem.html");
            return false;
        }
        return true;
    }

    function updateIdeChrome() {
        const value = sourceCode.value;
        const lines = value.length ? value.split("\n").length : 1;
        let gutterHtml = "";
        for (let i = 1; i <= lines; i++) {
            gutterHtml += i + (i < lines ? "\n" : "");
        }
        gutter.textContent = gutterHtml || "1";

        const pos = sourceCode.selectionStart || 0;
        const before = value.slice(0, pos);
        const row = before.split("\n").length;
        const col = before.length - before.lastIndexOf("\n");
        codeMeta.textContent = "Ln " + row + " · Col " + col;
    }

    function syncGutterScroll() {
        gutter.scrollTop = sourceCode.scrollTop;
    }

    sourceCode.addEventListener("input", updateIdeChrome);
    sourceCode.addEventListener("click", updateIdeChrome);
    sourceCode.addEventListener("keyup", updateIdeChrome);
    sourceCode.addEventListener("scroll", syncGutterScroll);
    sourceCode.addEventListener("keydown", function (e) {
        if (e.key === "Tab") {
            e.preventDefault();
            const start = sourceCode.selectionStart;
            const end = sourceCode.selectionEnd;
            const val = sourceCode.value;
            sourceCode.value = val.slice(0, start) + "    " + val.slice(end);
            sourceCode.selectionStart = sourceCode.selectionEnd = start + 4;
            updateIdeChrome();
        }
    });

    if (ideBody) {
        ideBody.addEventListener("click", function (e) {
            if (e.target !== sourceCode) {
                sourceCode.focus();
            }
        });
    }

    updateIdeChrome();

    function renderResult(data) {
        lastResult = data;
        resultPanel.classList.remove("d-none");
        document.getElementById("result-code").textContent = data.problemCode || "";
        document.getElementById("result-provider").textContent = data.providerUsed || "AI";
        document.getElementById("result-problem").textContent = data.problem || "";
        document.getElementById("result-location").textContent = data.location || "";
        document.getElementById("result-rootCause").textContent = data.rootCause || "";
        document.getElementById("result-summary").textContent = data.solutionSummary || "";
        document.getElementById("result-correctedCode").textContent = data.correctedCode || "// No code changes suggested";

        const stepsEl = document.getElementById("result-steps");
        stepsEl.innerHTML = "";
        (data.steps || []).forEach(function (step) {
            const li = document.createElement("li");
            li.innerHTML = "<strong>" + escapeHtml(step.title || ("Step " + step.order)) + "</strong><br>" +
                escapeHtml(step.detail || "");
            stepsEl.appendChild(li);
        });

        const notes = (data.notes || "").trim();
        document.getElementById("notes-block").classList.toggle("d-none", !notes);
        document.getElementById("result-notes").textContent = notes;

        feedbackMessage.classList.add("d-none");
        document.getElementById("feedback-actions").classList.remove("d-none");
        resultPanel.scrollIntoView({ behavior: "smooth", block: "start" });
    }

    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function readFormPayload() {
        return {
            sourceCode: sourceCode.value.trim(),
            errorMessage: document.getElementById("errorMessage").value.trim(),
            description: document.getElementById("description").value.trim()
        };
    }

    function validatePayload(payload) {
        if (!payload.sourceCode) {
            showAlert("Paste the code involved in the issue.", "danger");
            sourceCode.focus();
            return false;
        }
        if (!payload.errorMessage) {
            showAlert("Paste the error message.", "danger");
            document.getElementById("errorMessage").focus();
            return false;
        }
        return true;
    }

    form.addEventListener("submit", async function (e) {
        e.preventDefault();
        hideAlert();
        if (!requireAuth()) return;

        const payload = readFormPayload();
        if (!validatePayload(payload)) return;

        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Analyzing...';

        try {
            const response = await window.DevPilotApi.request(SUBMIT_URL, {
                method: "POST",
                body: JSON.stringify({
                    userId: 0,
                    sourceCode: payload.sourceCode,
                    errorMessage: payload.errorMessage,
                    description: payload.description || null
                })
            });

            if (!response.ok || !response.success || !response.data) {
                const detail = (response.errors && response.errors[0] && response.errors[0].message)
                    || response.message
                    || "Failed to analyze the problem.";
                throw new Error(detail);
            }

            renderResult(response.data);
        } catch (err) {
            showAlert(err.message || "Something went wrong while analyzing the problem.", "danger");
        } finally {
            submitBtn.disabled = false;
            submitBtn.innerHTML = '<i class="fa-solid fa-bug"></i> Analyze with AI';
        }
    });

    async function markSolved() {
        if (!lastResult || !requireAuth()) return;

        try {
            const feedbackUrl = new URL(
                "/api/Problems/" + lastResult.problemId + "/attempts/" + lastResult.debuggingAttemptId + "/feedback",
                API_BASE
            ).href;

            const response = await window.DevPilotApi.request(feedbackUrl, {
                method: "POST",
                body: JSON.stringify({
                    attemptId: lastResult.debuggingAttemptId,
                    isSolved: true,
                    comments: "Marked as solved from submit-problem page"
                })
            });

            feedbackMessage.classList.remove("d-none");
            feedbackMessage.textContent = "Great — solution saved and a published knowledge article was created.";
            document.getElementById("feedback-actions").classList.add("d-none");

            if (response && response.data && response.data.problemCode) {
                renderResult(response.data);
                document.getElementById("feedback-actions").classList.add("d-none");
                feedbackMessage.classList.remove("d-none");
            }
        } catch (err) {
            showAlert(err.message || "Failed to submit feedback.", "danger");
        }
    }

    async function followUpSameProblem() {
        if (!lastResult || !requireAuth()) return;
        hideAlert();

        const payload = readFormPayload();
        if (!validatePayload(payload)) {
            feedbackMessage.classList.remove("d-none");
            feedbackMessage.textContent = "Update the code and/or error above, then click Still have a problem again.";
            return;
        }

        // Record unsolved feedback on the previous attempt (best-effort), then re-debug same ticket.
        try {
            const feedbackUrl = new URL(
                "/api/Problems/" + lastResult.problemId + "/attempts/" + lastResult.debuggingAttemptId + "/feedback",
                API_BASE
            ).href;
            await window.DevPilotApi.request(feedbackUrl, {
                method: "POST",
                body: JSON.stringify({
                    attemptId: lastResult.debuggingAttemptId,
                    isSolved: false,
                    comments: "Still have a problem — requesting follow-up analysis"
                })
            });
        } catch (e) {
            console.warn("Unsolved feedback failed", e);
        }

        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Follow-up analysis...';
        document.getElementById("btn-unsolved").disabled = true;

        try {
            const followUpUrl = new URL(
                "/api/Problems/" + lastResult.problemId + "/follow-up",
                API_BASE
            ).href;

            const response = await window.DevPilotApi.request(followUpUrl, {
                method: "POST",
                body: JSON.stringify({
                    sourceCode: payload.sourceCode,
                    errorMessage: payload.errorMessage,
                    description: payload.description || null
                })
            });

            if (!response.ok || !response.success || !response.data) {
                const detail = (response.errors && response.errors[0] && response.errors[0].message)
                    || response.message
                    || "Follow-up analysis failed.";
                throw new Error(detail);
            }

            renderResult(response.data);
            feedbackMessage.classList.remove("d-none");
            feedbackMessage.textContent = "Follow-up complete on the same ticket (" + (response.data.problemCode || "") + "). Review the new analysis.";
        } catch (err) {
            showAlert(err.message || "Failed to run follow-up analysis.", "danger");
        } finally {
            submitBtn.disabled = false;
            submitBtn.innerHTML = '<i class="fa-solid fa-bug"></i> Analyze with AI';
            document.getElementById("btn-unsolved").disabled = false;
        }
    }

    document.getElementById("btn-solved").addEventListener("click", markSolved);
    document.getElementById("btn-unsolved").addEventListener("click", followUpSameProblem);

    requireAuth();
})();
