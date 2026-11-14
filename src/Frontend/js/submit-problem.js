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
        resultPanel.scrollIntoView({ behavior: "smooth", block: "start" });
    }

    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    form.addEventListener("submit", async function (e) {
        e.preventDefault();
        hideAlert();
        if (!requireAuth()) return;

        const code = sourceCode.value.trim();
        const errorMessage = document.getElementById("errorMessage").value.trim();
        const description = document.getElementById("description").value.trim();

        if (!code) {
            showAlert("Paste the code involved in the issue.", "danger");
            sourceCode.focus();
            return;
        }
        if (!errorMessage) {
            showAlert("Paste the error message.", "danger");
            document.getElementById("errorMessage").focus();
            return;
        }

        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Analyzing...';

        const payload = {
            userId: 0,
            sourceCode: code,
            errorMessage: errorMessage,
            description: description || null
        };

        try {
            const response = await window.DevPilotApi.request(SUBMIT_URL, {
                method: "POST",
                body: JSON.stringify(payload)
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

    async function sendFeedback(isSolved) {
        if (!lastResult || !requireAuth()) return;

        const comments = isSolved
            ? "Marked as solved from submit-problem page"
            : "Still have a problem after first attempt";

        try {
            const feedbackUrl = new URL(
                "/api/Problems/" + lastResult.problemId + "/attempts/" + lastResult.debuggingAttemptId + "/feedback",
                API_BASE
            ).href;

            const response = await window.DevPilotApi.request(feedbackUrl, {
                method: "POST",
                body: JSON.stringify({
                    attemptId: lastResult.debuggingAttemptId,
                    isSolved: isSolved,
                    comments: comments
                })
            });

            feedbackMessage.classList.remove("d-none");
            if (isSolved) {
                feedbackMessage.textContent = "Great — solution saved and a knowledge article draft was created.";
            } else {
                feedbackMessage.textContent = "Got it. Refine the code or error above and submit again.";
            }

            if (response && response.data && response.data.problemCode) {
                renderResult(response.data);
            }
        } catch (err) {
            showAlert(err.message || "Failed to submit feedback.", "danger");
        }
    }

    document.getElementById("btn-solved").addEventListener("click", function () {
        sendFeedback(true);
    });
    document.getElementById("btn-unsolved").addEventListener("click", function () {
        sendFeedback(false);
    });

    requireAuth();
})();
