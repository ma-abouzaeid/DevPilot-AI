(function () {
    "use strict";

    const form = document.getElementById("problem-form");
    const alertBox = document.getElementById("form-alert");
    const resultPanel = document.getElementById("result-panel");
    const solutionBody = document.getElementById("solution-body");
    const followupBanner = document.getElementById("followup-banner");
    const submitBtn = document.getElementById("submit-btn");
    const feedbackMessage = document.getElementById("feedback-message");
    const feedbackActions = document.getElementById("feedback-actions");
    const btnSolved = document.getElementById("btn-solved");
    const btnUnsolved = document.getElementById("btn-unsolved");
    const sourceCode = document.getElementById("sourceCode");
    const gutter = document.getElementById("code-gutter");
    const codeMeta = document.getElementById("code-meta");
    const ideBody = document.querySelector(".ide-body");
    const correctedHost = document.getElementById("corrected-code-host");
    const providerRow = document.getElementById("provider-row");
    const analysisRail = document.getElementById("analysis-rail");
    const analysisLog = document.getElementById("analysis-log");
    const analysisIdle = document.getElementById("analysis-idle");
    const analysisStatus = document.getElementById("analysis-status");
    const resultModeLabel = document.getElementById("result-mode-label");
    const SUBMIT_URL = new URL("/api/Problems", API_BASE).href;

    let lastResult = null;
    let resumedProblemId = null;
    let analysisTimer = null;
    let analysisStepIndex = 0;
    let copyResetTimer = null;

    const ANALYSIS_STEPS_INITIAL = [
        "Validating code and error message",
        "Creating debugging ticket",
        "Checking similar solved problems",
        "Running AI root-cause analysis",
        "Building structured fix and corrected code"
    ];

    const ANALYSIS_STEPS_FOLLOWUP = [
        "Recording previous attempt as unresolved",
        "Attaching updated code to the same ticket",
        "Re-running AI analysis with new context",
        "Comparing against the earlier proposal",
        "Preparing a revised structured solution"
    ];

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

    function isAdminViewer() {
        return !!(window.DevPilotApi && typeof window.DevPilotApi.isAdmin === "function" && window.DevPilotApi.isAdmin());
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

    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function formatSourceCode(raw) {
        if (!raw) return "";
        let text = String(raw).replace(/\r\n/g, "\n").trim();
        const newlineCount = (text.match(/\n/g) || []).length;
        if (newlineCount >= 3) {
            return text;
        }

        // Recover readable formatting from minified AI output.
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
                if (escaped) {
                    escaped = false;
                } else if (ch === "\\") {
                    escaped = true;
                } else if (ch === stringQuote) {
                    inString = false;
                }
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
                if (next && next !== ";" && next !== ")" && next !== "," && next !== "}") {
                    newline();
                }
                continue;
            }

            if (ch === ";") {
                out += ";";
                const next = text[i + 1];
                if (next && next !== "}" && next !== ")") {
                    newline();
                }
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

    function buildGutter(lineCount) {
        const lines = [];
        for (let i = 1; i <= lineCount; i++) {
            lines.push(String(i));
        }
        return lines.join("\n") || "1";
    }

    function mountIdeViewer(host, code, filename) {
        host.innerHTML = "";
        const formatted = formatSourceCode(code || "// No code changes suggested");
        const lineCount = formatted.length ? formatted.split("\n").length : 1;

        const editor = document.createElement("div");
        editor.className = "ide-editor ide-readonly";

        const toolbar = document.createElement("div");
        toolbar.className = "ide-toolbar";
        toolbar.innerHTML =
            '<div class="ide-dots" aria-hidden="true"><span></span><span></span><span></span></div>' +
            '<span class="ide-filename">' + escapeHtml(filename || "fixed.cs") + "</span>";

        const copyBtn = document.createElement("button");
        copyBtn.type = "button";
        copyBtn.className = "ide-copy-btn";
        copyBtn.innerHTML = '<i class="fa-regular fa-copy"></i> Copy';
        copyBtn.addEventListener("click", function () {
            const done = function () {
                copyBtn.classList.add("is-copied");
                copyBtn.innerHTML = '<i class="fa-solid fa-check"></i> Copied';
                if (copyResetTimer) clearTimeout(copyResetTimer);
                copyResetTimer = setTimeout(function () {
                    copyBtn.classList.remove("is-copied");
                    copyBtn.innerHTML = '<i class="fa-regular fa-copy"></i> Copy';
                }, 1600);
            };

            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(formatted).then(done).catch(function () {
                    fallbackCopy(formatted);
                    done();
                });
            } else {
                fallbackCopy(formatted);
                done();
            }
        });
        toolbar.appendChild(copyBtn);

        const body = document.createElement("div");
        body.className = "ide-body ide-body-readonly";

        const gutterEl = document.createElement("pre");
        gutterEl.className = "ide-gutter";
        gutterEl.setAttribute("aria-hidden", "true");
        gutterEl.textContent = buildGutter(lineCount);

        const codeEl = document.createElement("pre");
        codeEl.className = "ide-code";
        const codeInner = document.createElement("code");
        codeInner.textContent = formatted;
        codeEl.appendChild(codeInner);

        codeEl.addEventListener("scroll", function () {
            gutterEl.scrollTop = codeEl.scrollTop;
        });

        body.appendChild(gutterEl);
        body.appendChild(codeEl);
        editor.appendChild(toolbar);
        editor.appendChild(body);
        host.appendChild(editor);
    }

    function fallbackCopy(text) {
        const ta = document.createElement("textarea");
        ta.value = text;
        ta.setAttribute("readonly", "");
        ta.style.position = "absolute";
        ta.style.left = "-9999px";
        document.body.appendChild(ta);
        ta.select();
        try { document.execCommand("copy"); } catch (e) { /* ignore */ }
        document.body.removeChild(ta);
    }

    function clearAnalysisTimer() {
        if (analysisTimer) {
            clearInterval(analysisTimer);
            analysisTimer = null;
        }
    }

    function setAnalysisStatus(text, mode) {
        analysisStatus.textContent = text;
        analysisStatus.className = "analysis-status" + (mode ? " is-" + mode : "");
    }

    function startAnalysisLog(mode) {
        clearAnalysisTimer();
        const steps = mode === "followup" ? ANALYSIS_STEPS_FOLLOWUP : ANALYSIS_STEPS_INITIAL;
        analysisStepIndex = 0;
        analysisIdle.classList.add("d-none");
        analysisLog.classList.remove("d-none");
        analysisLog.innerHTML = "";
        analysisRail.classList.add("is-active");
        setAnalysisStatus(mode === "followup" ? "Follow-up" : "Analyzing", "running");

        steps.forEach(function (label, index) {
            const li = document.createElement("li");
            li.className = "analysis-log-item" + (index === 0 ? " is-active" : " is-pending");
            li.innerHTML =
                '<span class="analysis-log-marker" aria-hidden="true"></span>' +
                '<span class="analysis-log-text">' + escapeHtml(label) + "</span>";
            analysisLog.appendChild(li);
        });

        analysisTimer = setInterval(function () {
            const items = analysisLog.querySelectorAll(".analysis-log-item");
            if (analysisStepIndex >= items.length - 1) {
                clearAnalysisTimer();
                return;
            }
            items[analysisStepIndex].classList.remove("is-active");
            items[analysisStepIndex].classList.add("is-done");
            analysisStepIndex += 1;
            items[analysisStepIndex].classList.remove("is-pending");
            items[analysisStepIndex].classList.add("is-active");
        }, 1100);
    }

    function finishAnalysisLog(success) {
        clearAnalysisTimer();
        const items = analysisLog.querySelectorAll(".analysis-log-item");
        items.forEach(function (item) {
            item.classList.remove("is-active", "is-pending");
            item.classList.add(success ? "is-done" : "is-failed");
        });
        setAnalysisStatus(success ? "Complete" : "Failed", success ? "done" : "failed");
        if (!success) {
            analysisRail.classList.remove("is-active");
        }
    }

    function enterFollowUpPendingUi() {
        resultPanel.classList.remove("d-none");
        resultPanel.classList.add("is-follow-up-pending");
        followupBanner.classList.remove("d-none");
        solutionBody.classList.add("d-none");
        feedbackActions.classList.add("d-none");
        btnSolved.classList.add("d-none");
        feedbackMessage.classList.add("d-none");
        startAnalysisLog("followup");
    }

    function exitFollowUpPendingUi() {
        resultPanel.classList.remove("is-follow-up-pending");
        followupBanner.classList.add("d-none");
        solutionBody.classList.remove("d-none");
        btnSolved.classList.remove("d-none");
    }

    function pickField(obj) {
        if (!obj) return "";
        const keys = Array.prototype.slice.call(arguments, 1);
        for (let i = 0; i < keys.length; i++) {
            const value = obj[keys[i]];
            if (value != null && String(value).trim() !== "") return value;
        }
        return "";
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
            code = text.slice(codeMatch).replace(codeMarker, "").trim();
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

        return { summary: summaryLines.join(" ").trim(), steps: steps, code: code };
    }

    function attemptToResult(history, attempt) {
        const explanation = pickField(attempt, "explanation", "Explanation");
        const proposed = pickField(attempt, "proposedSolution", "ProposedSolution");
        const parsedExplain = parseExplanation(explanation);
        const parsedSolution = parseProposedSolution(proposed);
        return {
            problemId: history.problemId || history.ProblemId,
            problemCode: pickField(history, "problemCode", "ProblemCode"),
            debuggingAttemptId: attempt.debuggingAttemptId || attempt.DebuggingAttemptId,
            problem: parsedExplain.problem || pickField(history, "title", "Title"),
            location: parsedExplain.location,
            rootCause: pickField(attempt, "rootCause", "RootCause", "rootCauseAnalysis", "RootCauseAnalysis"),
            solutionSummary: parsedSolution.summary,
            steps: parsedSolution.steps,
            correctedCode: parsedSolution.code,
            notes: null,
            providerUsed: "Saved attempt",
            proposedSolution: proposed
        };
    }

    function renderResult(data, options) {
        const opts = options || {};
        lastResult = data;
        if (data && data.problemId) resumedProblemId = data.problemId;
        exitFollowUpPendingUi();
        resultPanel.classList.remove("d-none");

        document.getElementById("result-code").textContent = data.problemCode || "";
        document.getElementById("result-provider").textContent = data.providerUsed || "AI";
        providerRow.classList.toggle("d-none", !isAdminViewer());

        resultModeLabel.textContent = opts.resumed
            ? "Continuing ticket — confirm if this solved it"
            : (opts.followUp
                ? "Follow-up analysis on the same ticket"
                : "Initial analysis");

        document.getElementById("result-problem").textContent = data.problem || "";
        document.getElementById("result-location").textContent = data.location || "";
        document.getElementById("result-rootCause").textContent = data.rootCause || "";
        document.getElementById("result-summary").textContent = data.solutionSummary || "";

        mountIdeViewer(
            correctedHost,
            data.correctedCode || "// No code changes suggested",
            "fixed." + guessExtension(data)
        );

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
        feedbackActions.classList.remove("d-none");
        btnSolved.classList.remove("d-none");
        btnUnsolved.disabled = false;

        resultPanel.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    function guessExtension(data) {
        const blob = [
            data.framework || "",
            data.correctedCode || "",
            data.location || ""
        ].join(" ").toLowerCase();
        if (blob.indexOf("python") >= 0 || blob.indexOf(".py") >= 0) return "py";
        if (blob.indexOf("typescript") >= 0 || blob.indexOf(".ts") >= 0) return "ts";
        if (blob.indexOf("javascript") >= 0 || blob.indexOf(".js") >= 0) return "js";
        if (blob.indexOf("java") >= 0 && blob.indexOf("javascript") < 0) return "java";
        return "cs";
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
        resultPanel.classList.add("d-none");
        const isFollowUp = !!resumedProblemId;
        startAnalysisLog(isFollowUp ? "followup" : "initial");

        try {
            let response;
            if (isFollowUp) {
                const followUpUrl = new URL(
                    "/api/Problems/" + resumedProblemId + "/follow-up",
                    API_BASE
                ).href;
                response = await window.DevPilotApi.request(followUpUrl, {
                    method: "POST",
                    body: JSON.stringify({
                        sourceCode: payload.sourceCode,
                        errorMessage: payload.errorMessage,
                        description: payload.description || null
                    })
                });
            } else {
                response = await window.DevPilotApi.request(SUBMIT_URL, {
                    method: "POST",
                    body: JSON.stringify({
                        userId: 0,
                        sourceCode: payload.sourceCode,
                        errorMessage: payload.errorMessage,
                        description: payload.description || null
                    })
                });
            }

            if (!response.ok || !response.success || !response.data) {
                const detail = (response.errors && response.errors[0] && response.errors[0].message)
                    || response.message
                    || "Failed to analyze the problem.";
                throw new Error(detail);
            }

            finishAnalysisLog(true);
            renderResult(response.data, { followUp: isFollowUp });
        } catch (err) {
            finishAnalysisLog(false);
            showAlert(err.message || "Something went wrong while analyzing the problem.", "danger");
        } finally {
            submitBtn.disabled = false;
            submitBtn.innerHTML = '<i class="fa-solid fa-bug"></i> Analyze with AI';
        }
    });

    async function markSolved() {
        if (!lastResult || !requireAuth()) return;

        btnSolved.disabled = true;
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

            if (!response.ok && response.success === false) {
                throw new Error(response.message || "Failed to submit feedback.");
            }

            // Keep the current structured solution on screen. Replacing it with the
            // feedback payload used to wipe corrected code when parsing fell back.
            if (response && response.data) {
                const incoming = response.data;
                if (incoming.correctedCode || incoming.CorrectedCode) {
                    lastResult.correctedCode = incoming.correctedCode || incoming.CorrectedCode;
                }
                if (incoming.location || incoming.Location) {
                    lastResult.location = incoming.location || incoming.Location;
                }
                if (incoming.rootCause || incoming.RootCause) {
                    lastResult.rootCause = incoming.rootCause || incoming.RootCause;
                }
                if (incoming.solutionSummary || incoming.SolutionSummary) {
                    lastResult.solutionSummary = incoming.solutionSummary || incoming.SolutionSummary;
                }
                if ((incoming.steps || incoming.Steps || []).length) {
                    lastResult.steps = incoming.steps || incoming.Steps;
                }
                renderResult(lastResult, { followUp: false });
            }

            resultModeLabel.textContent = "Marked as solved — knowledge article published";
            providerRow.classList.add("d-none");
            feedbackActions.classList.add("d-none");
            feedbackMessage.classList.remove("d-none");
            feedbackMessage.textContent = "Great — solution saved. A published knowledge article and a practice challenge were created.";
        } catch (err) {
            showAlert(err.message || "Failed to submit feedback.", "danger");
        } finally {
            btnSolved.disabled = false;
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

        enterFollowUpPendingUi();
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Follow-up analysis...';
        btnUnsolved.disabled = true;

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

            finishAnalysisLog(true);
            renderResult(response.data, { followUp: true });
            feedbackMessage.classList.remove("d-none");
            feedbackMessage.textContent = "Follow-up complete on ticket " + (response.data.problemCode || "") + ". Review the revised analysis.";
        } catch (err) {
            finishAnalysisLog(false);
            exitFollowUpPendingUi();
            solutionBody.classList.remove("d-none");
            feedbackActions.classList.remove("d-none");
            showAlert(err.message || "Failed to run follow-up analysis.", "danger");
        } finally {
            submitBtn.disabled = false;
            submitBtn.innerHTML = '<i class="fa-solid fa-bug"></i> Analyze with AI';
            btnUnsolved.disabled = false;
        }
    }

    btnSolved.addEventListener("click", markSolved);
    btnUnsolved.addEventListener("click", followUpSameProblem);

    async function resumeTicketFromQuery() {
        const params = new URLSearchParams(window.location.search);
        const problemId = params.get("problemId") || params.get("id");
        if (!problemId || !requireAuth()) return;

        try {
            const url = new URL("/api/Problems/" + encodeURIComponent(problemId) + "/history", API_BASE).href;
            const res = await window.DevPilotApi.request(url);
            if (!res.ok || !res.data) {
                throw new Error(res.message || "Could not load this ticket.");
            }

            const history = res.data;
            const status = String(pickField(history, "status", "Status")).toLowerCase();
            resumedProblemId = history.problemId || history.ProblemId || Number(problemId);

            if (status === "solved") {
                window.location.href = "article-details.html?problemId=" + encodeURIComponent(resumedProblemId);
                return;
            }

            const requests = history.requests || history.Requests || [];
            const attempts = history.attempts || history.Attempts || [];
            const latestRequest = requests.length ? requests[requests.length - 1] : null;

            if (latestRequest) {
                sourceCode.value = pickField(latestRequest, "sourceCode", "SourceCode") || "";
                document.getElementById("errorMessage").value =
                    pickField(latestRequest, "errorMessage", "ErrorMessage") || "";
                document.getElementById("description").value =
                    pickField(latestRequest, "description", "Description") || "";
                updateIdeChrome();
            }

            const code = pickField(history, "problemCode", "ProblemCode");
            const title = pickField(history, "title", "Title");
            const pageLead = document.querySelector(".login-section .text-gray.mb-4");
            if (pageLead) {
                pageLead.textContent = "Continuing " + (code || ("ticket #" + resumedProblemId)) +
                    (title ? (" — " + title) : "") +
                    ". Your previous code and error are loaded below.";
            }

            if (attempts.length) {
                const latestAttempt = attempts[attempts.length - 1];
                const result = attemptToResult(history, latestAttempt);
                renderResult(result, { resumed: true });
                feedbackMessage.classList.remove("d-none");
                feedbackMessage.textContent = "Previous AI solution loaded. Mark it solved, or update the code/error and continue.";
            } else {
                showAlert(
                    "Ticket loaded with your code and error. Click Analyze with AI to generate a solution.",
                    "info"
                );
            }
        } catch (err) {
            showAlert(err.message || "Failed to resume this ticket.", "danger");
        }
    }

    if (requireAuth() && window.DevPilotApi && typeof window.DevPilotApi.refreshRolesFromProfile === "function") {
        window.DevPilotApi.refreshRolesFromProfile().catch(function () { /* ignore */ });
    }

    resumeTicketFromQuery();
})();
