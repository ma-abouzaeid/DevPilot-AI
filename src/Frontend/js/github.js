(function () {
    "use strict";

    if (!window.DevPilotApi || !window.DevPilotApi.isAuthenticated()) {
        window.location.href = "login.html?returnUrl=" + encodeURIComponent("github.html");
        return;
    }

    const alertBox = document.getElementById("page-alert");
    const connectionText = document.getElementById("connection-text");
    const btnConnect = document.getElementById("btn-connect");
    const btnDisconnect = document.getElementById("btn-disconnect");
    const workspace = document.getElementById("workspace");
    const reposList = document.getElementById("repos-list");
    const filesList = document.getElementById("files-list");
    const branchLabel = document.getElementById("branch-label");
    const selectedFileTitle = document.getElementById("selected-file-title");
    const selectedFilePath = document.getElementById("selected-file-path");
    const debugFileGate = document.getElementById("debug-file-gate");
    const debugFields = document.getElementById("debug-fields");
    const errorMessageInput = document.getElementById("error-message");
    const debugNotesInput = document.getElementById("debug-notes");
    const btnDebug = document.getElementById("btn-debug");
    const resultCard = document.getElementById("result-card");
    const resultBody = document.getElementById("result-body");
    const resultHeading = document.getElementById("result-heading");
    const resultModeLabel = document.getElementById("result-mode-label");
    const resultCodeBadge = document.getElementById("result-code");
    const feedbackActions = document.getElementById("feedback-actions");
    const feedbackMessage = document.getElementById("feedback-message");
    const btnSolved = document.getElementById("btn-solved");
    const btnUnsolved = document.getElementById("btn-unsolved");

    let selectedRepo = null;
    let selectedPath = null;
    let mode = "debug";
    let lastDebugResult = null;
    let reviewsPage = 1;
    const reviewsHistoryList = document.getElementById("reviews-history-list");
    const reviewsPager = document.getElementById("reviews-pager");

    function pick(obj) {
        if (!obj) return "";
        const keys = Array.prototype.slice.call(arguments, 1);
        for (let i = 0; i < keys.length; i++) {
            const value = obj[keys[i]];
            if (value != null && String(value).trim() !== "") return value;
        }
        return "";
    }

    function normalizeDebugResult(data) {
        const steps = (data.steps || data.Steps || []).map(function (step) {
            return {
                order: step.order || step.Order || 0,
                title: step.title || step.Title || "",
                detail: step.detail || step.Detail || ""
            };
        });
        return {
            problemId: data.problemId || data.ProblemId,
            problemCode: pick(data, "problemCode", "ProblemCode"),
            debuggingAttemptId: data.debuggingAttemptId || data.DebuggingAttemptId,
            problem: pick(data, "problem", "Problem"),
            location: pick(data, "location", "Location"),
            rootCause: pick(data, "rootCause", "RootCause"),
            solutionSummary: pick(data, "solutionSummary", "SolutionSummary"),
            steps: steps,
            correctedCode: pick(data, "correctedCode", "CorrectedCode"),
            notes: pick(data, "notes", "Notes"),
            proposedSolution: pick(data, "proposedSolution", "ProposedSolution")
        };
    }

    function formatSourceCode(raw) {
        if (!raw) return "";
        let text = String(raw).replace(/\r\n/g, "\n").trim();
        const newlineCount = (text.match(/\n/g) || []).length;
        if (newlineCount >= 2) return text;
        return text;
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

    function ideBlock(code, filename) {
        const formatted = formatSourceCode(code || "// No code changes suggested");
        const lines = formatted.length ? formatted.split("\n") : [" "];
        let gutter = "";
        for (let i = 1; i <= lines.length; i++) gutter += i + (i < lines.length ? "\n" : "");
        return (
            '<div class="ide-editor ide-readonly article-ide">' +
            '<div class="ide-toolbar">' +
            '<div class="ide-dots" aria-hidden="true"><span></span><span></span><span></span></div>' +
            '<span class="ide-filename">' + escapeHtml(filename || "fixed.txt") + "</span>" +
            '<button type="button" class="ide-copy-btn" data-copy="' + encodeURIComponent(formatted) + '">' +
            '<i class="fa-regular fa-copy"></i> Copy</button>' +
            "</div>" +
            '<div class="ide-body ide-body-readonly">' +
            '<pre class="ide-gutter" aria-hidden="true">' + gutter + "</pre>" +
            '<pre class="ide-code"><code>' + escapeHtml(formatted) + "</code></pre>" +
            "</div></div>"
        );
    }

    function bindIdeCopyButtons(scope) {
        if (!scope) return;
        scope.querySelectorAll(".ide-copy-btn").forEach(function (btn) {
            btn.addEventListener("click", function () {
                const text = decodeURIComponent(btn.getAttribute("data-copy") || "");
                const done = function () {
                    btn.classList.add("is-copied");
                    btn.innerHTML = '<i class="fa-solid fa-check"></i> Copied';
                    setTimeout(function () {
                        btn.classList.remove("is-copied");
                        btn.innerHTML = '<i class="fa-regular fa-copy"></i> Copy';
                    }, 1600);
                };
                if (navigator.clipboard && navigator.clipboard.writeText) {
                    navigator.clipboard.writeText(text).then(done).catch(function () {
                        fallbackCopy(text);
                        done();
                    });
                } else {
                    fallbackCopy(text);
                    done();
                }
            });
        });
    }

    async function fetchSelectedFileContent() {
        if (!selectedRepo || !selectedPath) {
            throw new Error("Select a repository and file first.");
        }
        const parts = selectedRepo.fullName.split("/");
        const url = new URL(
            "/api/GitHub/repos/" + encodeURIComponent(parts[0]) + "/" + encodeURIComponent(parts[1]) + "/contents",
            API_BASE
        );
        url.searchParams.set("path", selectedPath);
        if (selectedRepo.branch) url.searchParams.set("branch", selectedRepo.branch);
        const res = await window.DevPilotApi.request(url.href);
        if (!res.ok || !res.data) throw new Error(res.message || "Failed to load file content");
        return pick(res.data, "content", "Content");
    }

    function fileNameFromPath(path) {
        const parts = String(path || "").split(/[/\\]/);
        return parts[parts.length - 1] || path || "Selected file";
    }

    function setDebugFieldsEnabled(enabled) {
        if (debugFileGate) debugFileGate.classList.toggle("d-none", enabled);
        if (debugFields) debugFields.classList.toggle("d-none", !enabled);
        if (errorMessageInput) {
            errorMessageInput.disabled = !enabled;
            if (!enabled) errorMessageInput.value = "";
        }
        if (debugNotesInput) {
            debugNotesInput.disabled = !enabled;
            if (!enabled) debugNotesInput.value = "";
        }
        if (btnDebug) btnDebug.disabled = !enabled;
        if (!enabled) {
            selectedFileTitle.textContent = "Selected file";
            if (selectedFilePath) selectedFilePath.textContent = "";
        }
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function showAlert(message, type) {
        alertBox.textContent = message;
        alertBox.className = "alert alert-" + (type || "danger");
        alertBox.classList.remove("d-none");
    }

    function hideAlert() {
        alertBox.classList.add("d-none");
    }

    function setMode(next) {
        mode = next;
        document.getElementById("panel-debug").classList.toggle("d-none", mode !== "debug");
        document.getElementById("panel-review").classList.toggle("d-none", mode !== "review");
        document.getElementById("tab-debug").classList.toggle("btn-primary", mode === "debug");
        document.getElementById("tab-debug").classList.toggle("btn-outline-card", mode !== "debug");
        document.getElementById("tab-review").classList.toggle("btn-primary", mode === "review");
        document.getElementById("tab-review").classList.toggle("btn-outline-card", mode !== "review");
    }

    function renderDisconnectedUi() {
        connectionText.textContent = "Not connected. Authorize GitHub to browse repos and files.";
        btnConnect.classList.remove("d-none");
        btnDisconnect.classList.add("d-none");
        workspace.classList.add("d-none");
        selectedRepo = null;
        selectedPath = null;
        setDebugFieldsEnabled(false);
        if (reposList) reposList.innerHTML = "";
        if (filesList) filesList.innerHTML = "";
    }

    function bindReconnectButton(id) {
        const reconnect = document.getElementById(id);
        if (!reconnect) return;
        reconnect.addEventListener("click", function () {
            reconnectGitHub().catch(function (err) {
                showAlert(err.message || "Reconnect failed", "danger");
            });
        });
    }

    async function loadStatus() {
        const url = new URL("/api/GitHub/status", API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        if (!res.ok || !res.data) throw new Error(res.message || "Failed to load GitHub status");

        const connected = res.data.isConnected || res.data.IsConnected;
        const username = res.data.username || res.data.Username;
        if (connected) {
            connectionText.textContent = "Connected as @" + (username || "github-user");
            btnConnect.classList.add("d-none");
            btnDisconnect.classList.remove("d-none");
            workspace.classList.remove("d-none");
            try {
                await loadRepos();
            } catch (err) {
                const msg = err && err.message ? String(err.message) : "";
                if (/expired|not connected|reconnect/i.test(msg)) {
                    renderDisconnectedUi();
                }
                showAlert(msg || "Failed to load repositories", "danger");
            }
        } else {
            renderDisconnectedUi();
        }
    }

    async function connect() {
        hideAlert();
        const url = new URL("/api/GitHub/connect", API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        const authorizeUrl = res.data && (res.data.authorizeUrl || res.data.AuthorizeUrl);
        if (!res.ok || !authorizeUrl) {
            throw new Error(res.message || "Could not start GitHub OAuth");
        }
        window.location.href = authorizeUrl;
    }

    async function disconnect() {
        hideAlert();
        const url = new URL("/api/GitHub/disconnect", API_BASE).href;
        let res = await window.DevPilotApi.request(url, { method: "POST" });
        if (!res.ok && (res.status === 405 || res.status === 404)) {
            res = await window.DevPilotApi.request(url, { method: "DELETE" });
        }
        if (!res.ok) {
            throw new Error(res.message || "Failed to disconnect GitHub");
        }
        renderDisconnectedUi();
        showAlert("GitHub disconnected. You can connect again anytime.", "success");
    }

    async function reconnectGitHub() {
        hideAlert();
        try {
            await disconnect();
        } catch (err) {
            // Reconnect anyway; OAuth replaces a bad token.
            console.warn("Disconnect before reconnect failed:", err);
        }
        await connect();
    }

    async function loadRepos() {
        reposList.innerHTML = '<div class="empty-state">Loading repositories…</div>';
        const url = new URL("/api/GitHub/repos", API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        if (!res.ok) {
            const needsReconnect = /reconnect|expired|invalid|not connected|GITHUB_NOT_CONNECTED/i.test(
                (res.message || "") + " " + (res.errorCode || "")
            );
            reposList.innerHTML =
                '<div class="empty-state">' +
                escapeHtml(res.message || "Failed to load repositories.") +
                '<div class="mt-2"><button type="button" class="btn btn-outline-card btn-sm" style="width:auto;" id="btn-reconnect-repos">' +
                (needsReconnect ? "Clear & reconnect GitHub" : "Disconnect & reconnect") +
                "</button></div>" +
                "</div>";
            bindReconnectButton("btn-reconnect-repos");
            if (needsReconnect) {
                // Stale token: show Connect so user isn't stuck on Disconnect only.
                btnConnect.classList.remove("d-none");
            }
            throw new Error(res.message || "Failed to load repositories");
        }

        const items = Array.isArray(res.data) ? res.data : [];
        if (!items.length) {
            reposList.innerHTML =
                '<div class="empty-state">' +
                "No repositories returned for this GitHub connection. Disconnect and reconnect, then approve the <strong>repo</strong> permission." +
                '<div class="mt-2"><button type="button" class="btn btn-outline-card btn-sm" style="width:auto;" id="btn-reconnect-repos-empty">Reconnect GitHub</button></div>' +
                "</div>";
            bindReconnectButton("btn-reconnect-repos-empty");
            return;
        }

        reposList.innerHTML = items.map(function (repo) {
            const fullName = repo.fullName || repo.FullName;
            const branch = repo.defaultBranch || repo.DefaultBranch || "main";
            const isPrivate = repo.isPrivate || repo.IsPrivate;
            return (
                '<button type="button" class="github-item" data-repo="' + escapeHtml(fullName) + '" data-branch="' + escapeHtml(branch) + '">' +
                "<strong>" + escapeHtml(fullName) + "</strong>" +
                '<div class="text-gray small">' + (isPrivate ? "Private" : "Public") + " · " + escapeHtml(branch) + "</div>" +
                "</button>"
            );
        }).join("");
    }

    async function loadTree(fullName, branch) {
        filesList.innerHTML = '<div class="empty-state">Loading files…</div>';
        branchLabel.textContent = branch || "";
        const parts = fullName.split("/");
        const url = new URL("/api/GitHub/repos/" + encodeURIComponent(parts[0]) + "/" + encodeURIComponent(parts[1]) + "/tree", API_BASE);
        if (branch) url.searchParams.set("branch", branch);
        const res = await window.DevPilotApi.request(url.href);
        if (!res.ok) {
            const needsReconnect = /reconnect|expired|invalid|unauthorized|not connected|GITHUB_/i.test(
                (res.message || "") + " " + (res.errorCode || "")
            );
            filesList.innerHTML =
                '<div class="empty-state">' +
                escapeHtml(res.message || "Failed to load files.") +
                '<div class="mt-2"><button type="button" class="btn btn-outline-card btn-sm" style="width:auto;" id="btn-reconnect-files">' +
                (needsReconnect ? "Clear & reconnect GitHub" : "Try reconnect") +
                "</button></div>" +
                "</div>";
            bindReconnectButton("btn-reconnect-files");
            if (needsReconnect) {
                btnConnect.classList.remove("d-none");
                connectionText.textContent = "GitHub connection expired. Reconnect to browse files.";
            }
            throw new Error(res.message || "Failed to load files");
        }

        const items = Array.isArray(res.data) ? res.data : [];
        if (!items.length) {
            filesList.innerHTML = '<div class="empty-state">No files found.</div>';
            return;
        }

        filesList.innerHTML = items.slice(0, 250).map(function (item) {
            const path = item.path || item.Path;
            return (
                '<button type="button" class="github-item" data-path="' + escapeHtml(path) + '">' +
                '<span class="font-monospace small">' + escapeHtml(path) + "</span>" +
                "</button>"
            );
        }).join("");
    }

    async function runDebug() {
        hideAlert();
        if (!selectedRepo || !selectedPath) {
            showAlert("Select a repository and a file first.", "danger");
            return;
        }
        const errorMessage = document.getElementById("error-message").value.trim();
        if (!errorMessage) {
            showAlert("Paste the error message related to this file.", "danger");
            return;
        }

        btnDebug.disabled = true;
        btnDebug.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Analyzing...';
        try {
            const url = new URL("/api/GitHub/debug", API_BASE).href;
            const res = await window.DevPilotApi.request(url, {
                method: "POST",
                body: JSON.stringify({
                    fullName: selectedRepo.fullName,
                    path: selectedPath,
                    branch: selectedRepo.branch,
                    errorMessage: errorMessage,
                    description: debugNotesInput.value.trim() || null
                })
            });
            if (!res.ok || !res.data) throw new Error(res.message || "Debug failed");
            renderDebugResult(normalizeDebugResult(res.data), { followUp: false });
        } catch (err) {
            showAlert(err.message || "Debug failed", "danger");
        } finally {
            btnDebug.disabled = !selectedPath;
            btnDebug.innerHTML = '<i class="fa-solid fa-bug"></i> Analyze file with AI';
        }
    }

    async function markSolved() {
        hideAlert();
        if (!lastDebugResult || !lastDebugResult.problemId || !lastDebugResult.debuggingAttemptId) {
            showAlert("No debug result to mark as solved.", "danger");
            return;
        }

        btnSolved.disabled = true;
        btnUnsolved.disabled = true;
        if (window.DevPilotBusy) {
            window.DevPilotBusy.show(
                "Creating your article & challenge…",
                "DevPilot is publishing knowledge from this fix. You’ll open the article next."
            );
        }
        try {
            const feedbackUrl = new URL(
                "/api/Problems/" + lastDebugResult.problemId + "/attempts/" + lastDebugResult.debuggingAttemptId + "/feedback",
                API_BASE
            ).href;
            const response = await window.DevPilotApi.request(feedbackUrl, {
                method: "POST",
                body: JSON.stringify({
                    attemptId: lastDebugResult.debuggingAttemptId,
                    isSolved: true,
                    comments: "Marked as solved from GitHub debug"
                })
            });

            if (!response.ok && response.success === false) {
                throw new Error(response.message || "Failed to submit feedback.");
            }

            const articleId =
                (response.data && (response.data.knowledgeArticleId || response.data.KnowledgeArticleId || response.data.articleId || response.data.ArticleId)) ||
                null;
            const target = articleId
                ? "article-details.html?id=" + encodeURIComponent(articleId)
                : "article-details.html?problemId=" + encodeURIComponent(lastDebugResult.problemId);

            if (window.DevPilotBusy) {
                window.DevPilotBusy.show("Almost done…", "Redirecting to your new article.");
            }
            window.location.href = target;
        } catch (err) {
            if (window.DevPilotBusy) window.DevPilotBusy.hide();
            showAlert(err.message || "Failed to mark as solved.", "danger");
            btnSolved.disabled = false;
            btnUnsolved.disabled = false;
        }
    }

    async function followUpSameProblem() {
        hideAlert();
        if (!lastDebugResult || !lastDebugResult.problemId || !lastDebugResult.debuggingAttemptId) {
            showAlert("No debug result to follow up.", "danger");
            return;
        }

        const errorMessage = errorMessageInput.value.trim();
        if (!errorMessage) {
            showAlert("Update the error message if needed, then try again.", "danger");
            return;
        }

        btnSolved.disabled = true;
        btnUnsolved.disabled = true;
        btnDebug.disabled = true;
        feedbackMessage.classList.remove("d-none");
        feedbackMessage.textContent = "Running follow-up analysis on the same ticket…";
        resultModeLabel.textContent = "Follow-up in progress…";

        try {
            const feedbackUrl = new URL(
                "/api/Problems/" + lastDebugResult.problemId + "/attempts/" + lastDebugResult.debuggingAttemptId + "/feedback",
                API_BASE
            ).href;
            await window.DevPilotApi.request(feedbackUrl, {
                method: "POST",
                body: JSON.stringify({
                    attemptId: lastDebugResult.debuggingAttemptId,
                    isSolved: false,
                    comments: "Still have a problem — GitHub follow-up"
                })
            });
        } catch (e) {
            console.warn("Unsolved feedback failed", e);
        }

        try {
            const sourceCode = await fetchSelectedFileContent();
            const followUpUrl = new URL(
                "/api/Problems/" + lastDebugResult.problemId + "/follow-up",
                API_BASE
            ).href;
            const response = await window.DevPilotApi.request(followUpUrl, {
                method: "POST",
                body: JSON.stringify({
                    sourceCode: sourceCode,
                    errorMessage: errorMessage,
                    description: debugNotesInput.value.trim() || ("Follow-up for GitHub file " + selectedRepo.fullName + ":" + selectedPath)
                })
            });

            if (!response.ok || !response.success || !response.data) {
                const detail = (response.errors && response.errors[0] && response.errors[0].message)
                    || response.message
                    || "Follow-up analysis failed.";
                throw new Error(detail);
            }

            renderDebugResult(normalizeDebugResult(response.data), { followUp: true });
            feedbackMessage.classList.remove("d-none");
            feedbackMessage.textContent = "Follow-up complete on ticket " + (response.data.problemCode || response.data.ProblemCode || "") + ". Review the revised analysis.";
        } catch (err) {
            showAlert(err.message || "Failed to run follow-up analysis.", "danger");
            feedbackActions.classList.remove("d-none");
            feedbackMessage.classList.add("d-none");
            resultModeLabel.textContent = "Follow-up failed — previous analysis still shown";
            btnSolved.disabled = false;
            btnUnsolved.disabled = false;
        } finally {
            btnDebug.disabled = !selectedPath;
        }
    }

    async function refreshReviewGate() {
        const gate = document.getElementById("review-plan-gate");
        const btn = document.getElementById("btn-review");
        try {
            const url = new URL("/api/Plans/me", API_BASE).href;
            const res = await window.DevPilotApi.request(url);
            const canReview = res.ok && res.data && (res.data.canUseCodeReview || res.data.CanUseCodeReview);
            gate.classList.toggle("d-none", !!canReview);
            btn.disabled = !canReview;
        } catch (e) {
            gate.classList.add("d-none");
        }
    }

    function fmtReviewDate(value) {
        if (!value) return "—";
        try { return new Date(value).toLocaleString(); } catch (e) { return String(value); }
    }

    async function loadSavedReviews() {
        if (!reviewsHistoryList) return;
        reviewsHistoryList.innerHTML = '<div class="empty-state">Loading saved reviews…</div>';
        try {
            const url = new URL("/api/GitHub/reviews", API_BASE);
            url.searchParams.set("page", String(reviewsPage));
            url.searchParams.set("pageSize", "10");
            const res = await window.DevPilotApi.request(url.href);
            if (!res.ok) throw new Error(res.message || "Failed to load saved reviews");

            const data = res.data || {};
            const items = data.items || data.Items || [];
            const total = data.totalCount || data.TotalCount || 0;
            const page = data.page || data.Page || reviewsPage;
            const pageSize = data.pageSize || data.PageSize || 10;

            if (!items.length) {
                reviewsHistoryList.innerHTML =
                    '<div class="empty-state">No saved reviews yet. Run a Code Review and it will show up here.</div>';
            } else {
                reviewsHistoryList.innerHTML = items.map(function (item) {
                    const id = item.codeReviewId || item.CodeReviewId;
                    const repo = item.repositoryFullName || item.RepositoryFullName || "Repository";
                    const branch = item.branch || item.Branch || "main";
                    const findings = item.findingsCount || item.FindingsCount || 0;
                    const preview = item.summaryPreview || item.SummaryPreview || "";
                    const when = fmtReviewDate(item.reviewedAt || item.ReviewedAt);
                    return (
                        '<button type="button" class="github-item" data-review-id="' + escapeHtml(String(id)) + '">' +
                        "<strong>" + escapeHtml(repo) + "</strong>" +
                        '<div class="text-gray small">' + escapeHtml(branch) + " · " + findings + " findings · " + escapeHtml(when) + "</div>" +
                        (preview ? '<div class="small mt-1">' + escapeHtml(preview) + "</div>" : "") +
                        "</button>"
                    );
                }).join("");
            }

            if (reviewsPager) {
                const pages = Math.max(1, Math.ceil(total / pageSize));
                reviewsPager.innerHTML =
                    '<span class="text-gray small">Page ' + page + " / " + pages + " · " + total + " saved</span>" +
                    '<div class="d-flex gap-2">' +
                    '<button type="button" class="btn btn-outline-card btn-sm" data-reviews-dir="-1"' + (page <= 1 ? " disabled" : "") + ">Prev</button>" +
                    '<button type="button" class="btn btn-outline-card btn-sm" data-reviews-dir="1"' + (page >= pages ? " disabled" : "") + ">Next</button>" +
                    "</div>";
                reviewsPager.querySelectorAll("button[data-reviews-dir]").forEach(function (btn) {
                    btn.addEventListener("click", function () {
                        reviewsPage = page + Number(btn.getAttribute("data-reviews-dir"));
                        loadSavedReviews().catch(function (err) {
                            showAlert(err.message || "Failed to load reviews", "danger");
                        });
                    });
                });
            }
        } catch (err) {
            reviewsHistoryList.innerHTML =
                '<div class="empty-state text-danger">' + escapeHtml(err.message || "Failed to load saved reviews.") + "</div>";
        }
    }

    async function openSavedReview(reviewId) {
        hideAlert();
        const url = new URL("/api/GitHub/reviews/" + encodeURIComponent(reviewId), API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        if (!res.ok || !res.data) throw new Error(res.message || "Failed to open review");
        if (reviewsHistoryList) {
            Array.prototype.forEach.call(reviewsHistoryList.querySelectorAll("[data-review-id]"), function (el) {
                el.classList.toggle("active", String(el.getAttribute("data-review-id")) === String(reviewId));
            });
        }
        // Result card lives outside #workspace so it still opens when disconnected.
        renderReviewResult(res.data, { fromHistory: true });
        resultCard.scrollIntoView({ behavior: "smooth", block: "start" });
    }

    async function runReview() {
        hideAlert();
        if (!selectedRepo) {
            showAlert("Select a repository first.", "danger");
            return;
        }

        const btn = document.getElementById("btn-review");
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Reviewing...';
        try {
            const url = new URL("/api/GitHub/review", API_BASE).href;
            const payload = {
                fullName: selectedRepo.fullName,
                branch: selectedRepo.branch,
                maxFiles: 6,
                paths: selectedPath ? [selectedPath] : []
            };
            const res = await window.DevPilotApi.request(url, {
                method: "POST",
                body: JSON.stringify(payload)
            });
            if (!res.ok || !res.data) {
                if (res.errorCode === "PLAN_FEATURE_REQUIRED" || res.errorCode === "INSUFFICIENT_CREDITS") {
                    document.getElementById("review-plan-gate").classList.remove("d-none");
                }
                throw new Error(res.message || "Review failed");
            }
            renderReviewResult(res.data, { fromHistory: false });
            reviewsPage = 1;
            loadSavedReviews().catch(function () { /* ignore */ });
            showAlert("Code review saved. You can reopen it anytime from Saved code reviews.", "success");
        } catch (err) {
            showAlert(err.message || "Review failed", "danger");
        } finally {
            btn.disabled = false;
            btn.innerHTML = '<i class="fa-solid fa-magnifying-glass-chart"></i> Run code review';
            refreshReviewGate();
        }
    }

    function renderDebugResult(data, options) {
        const opts = options || {};
        lastDebugResult = data;
        resultCard.classList.remove("d-none");
        if (resultHeading) resultHeading.textContent = opts.solved ? "Resolved" : "Debug result";
        if (resultModeLabel) {
            resultModeLabel.textContent = opts.solved
                ? "Marked as solved — article and challenge created"
                : (opts.followUp ? "Follow-up analysis on the same ticket" : "Initial analysis");
        }
        if (resultCodeBadge) resultCodeBadge.textContent = data.problemCode || "";

        const steps = (data.steps || []).map(function (step) {
            return (
                "<li><strong>" + escapeHtml(step.title || ("Step " + step.order)) + "</strong><br>" +
                escapeHtml(step.detail || "") +
                "</li>"
            );
        }).join("");

        const fileLabel = selectedPath ? fileNameFromPath(selectedPath) : "fixed.txt";
        resultBody.innerHTML =
            '<article class="result-block"><h3>1. Problem</h3><p>' + escapeHtml(data.problem || "") + "</p></article>" +
            '<article class="result-block"><h3>2. Location</h3><p>' + escapeHtml(data.location || "—") + "</p></article>" +
            '<article class="result-block"><h3>3. Root Cause</h3><p>' + escapeHtml(data.rootCause || "—") + "</p></article>" +
            '<article class="result-block"><h3>4. Solution Summary</h3><p>' + escapeHtml(data.solutionSummary || "") + "</p></article>" +
            '<article class="result-block"><h3>5. Steps</h3><ol class="result-steps">' + (steps || "<li class=\"text-gray\">No steps returned.</li>") + "</ol></article>" +
            '<article class="result-block"><h3>6. Corrected Code</h3>' + ideBlock(data.correctedCode, fileLabel) + "</article>" +
            (data.notes
                ? '<article class="result-block"><h3>7. Notes</h3><p>' + escapeHtml(data.notes) + "</p></article>"
                : "") +
            '<div class="d-flex flex-wrap gap-2 mt-2">' +
            '<a class="btn btn-outline-card" style="width:auto;" href="problem-details.html?id=' + encodeURIComponent(data.problemId) + '">Open ticket</a>' +
            (opts.solved
                ? '<a class="btn btn-primary" style="width:auto;" href="article-details.html?problemId=' + encodeURIComponent(data.problemId) + '">Open article</a>' +
                  '<a class="btn btn-outline-card" style="width:auto;" href="challenges.html">Practice challenges</a>'
                : "") +
            "</div>";

        bindIdeCopyButtons(resultBody);

        if (opts.solved) {
            feedbackActions.classList.add("d-none");
            feedbackMessage.classList.remove("d-none");
            feedbackMessage.textContent = "Great — solution saved. A published knowledge article and a practice challenge were created from this GitHub debug.";
            btnSolved.disabled = true;
            btnUnsolved.disabled = true;
        } else {
            feedbackActions.classList.remove("d-none");
            feedbackMessage.classList.add("d-none");
            btnSolved.disabled = false;
            btnUnsolved.disabled = false;
        }

        resultCard.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    function renderReviewResult(data, options) {
        const opts = options || {};
        resultCard.classList.remove("d-none");
        lastDebugResult = null;
        const reviewId = data.codeReviewId || data.CodeReviewId;
        const repoName = data.repositoryFullName || data.RepositoryFullName || "";
        const reviewedAt = data.reviewedAt || data.ReviewedAt;
        if (resultHeading) resultHeading.textContent = "Code review";
        if (resultModeLabel) {
            resultModeLabel.textContent = opts.fromHistory
                ? ("Saved review" + (reviewedAt ? (" · " + fmtReviewDate(reviewedAt)) : ""))
                : ("Repository review" + (repoName ? (" · " + repoName) : ""));
        }
        if (resultCodeBadge) resultCodeBadge.textContent = opts.fromHistory ? "Saved" : "Review";
        if (feedbackActions) feedbackActions.classList.add("d-none");
        if (feedbackMessage) feedbackMessage.classList.add("d-none");

        const findings = (data.findings || data.Findings || []).map(function (f) {
            return (
                '<div class="result-block">' +
                "<strong>" + escapeHtml(f.severity || f.Severity || "Medium") + "</strong> · " +
                '<span class="text-gray">' + escapeHtml(f.filePath || f.FilePath || "") +
                ((f.lineNumber || f.LineNumber) ? (":" + (f.lineNumber || f.LineNumber)) : "") + "</span>" +
                "<p class=\"mb-1 mt-2\">" + escapeHtml(f.description || f.Description || "") + "</p>" +
                "<p class=\"mb-0 text-gray small\">Fix: " + escapeHtml(f.suggestedFix || f.SuggestedFix || "") + "</p>" +
                "</div>"
            );
        }).join("") || '<p class="text-gray">No findings.</p>';

        const paths = (data.reviewedPaths || data.ReviewedPaths || []).map(escapeHtml).join(", ");
        resultBody.innerHTML =
            (repoName ? '<p class="mb-1"><strong>' + escapeHtml(repoName) + "</strong> · " + escapeHtml(data.branch || data.Branch || "") + "</p>" : "") +
            (reviewId ? '<p class="mb-2 text-gray small">Saved review #' + escapeHtml(String(reviewId)) + "</p>" : "") +
            "<h3 class=\"h6\">Summary</h3><p>" + escapeHtml(data.summary || data.Summary || "") + "</p>" +
            '<p class="text-gray small">Reviewed: ' + (paths || "—") + "</p>" +
            "<h3 class=\"h6\">Findings</h3>" + findings;
        resultCard.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    reposList.addEventListener("click", async function (e) {
        const btn = e.target.closest("[data-repo]");
        if (!btn) return;
        hideAlert();
        selectedRepo = {
            fullName: btn.getAttribute("data-repo"),
            branch: btn.getAttribute("data-branch") || "main"
        };
        selectedPath = null;
        setDebugFieldsEnabled(false);
        Array.prototype.forEach.call(reposList.querySelectorAll(".github-item"), function (el) {
            el.classList.toggle("active", el === btn);
        });
        try {
            await loadTree(selectedRepo.fullName, selectedRepo.branch);
        } catch (err) {
            showAlert(err.message || "Failed to load files", "danger");
        }
    });

    filesList.addEventListener("click", function (e) {
        const btn = e.target.closest("[data-path]");
        if (!btn) return;
        selectedPath = btn.getAttribute("data-path");
        setDebugFieldsEnabled(true);
        selectedFileTitle.textContent = fileNameFromPath(selectedPath);
        if (selectedFilePath) {
            selectedFilePath.textContent = "Path: " + selectedPath;
        }
        Array.prototype.forEach.call(filesList.querySelectorAll(".github-item"), function (el) {
            el.classList.toggle("active", el === btn);
        });
    });

    document.getElementById("tab-debug").addEventListener("click", function () { setMode("debug"); });
    document.getElementById("tab-review").addEventListener("click", function () {
        setMode("review");
        refreshReviewGate();
    });
    btnConnect.addEventListener("click", function () { connect().catch(function (err) { showAlert(err.message, "danger"); }); });
    btnDisconnect.addEventListener("click", function () {
        disconnect().catch(function (err) { showAlert(err.message, "danger"); });
    });
    btnDebug.addEventListener("click", runDebug);
    if (btnSolved) btnSolved.addEventListener("click", markSolved);
    if (btnUnsolved) btnUnsolved.addEventListener("click", followUpSameProblem);
    setDebugFieldsEnabled(false);
    document.getElementById("btn-review").addEventListener("click", runReview);
    const btnRefreshReviews = document.getElementById("btn-refresh-reviews");
    if (btnRefreshReviews) {
        btnRefreshReviews.addEventListener("click", function () {
            loadSavedReviews().catch(function (err) {
                showAlert(err.message || "Failed to load reviews", "danger");
            });
        });
    }
    if (reviewsHistoryList) {
        reviewsHistoryList.addEventListener("click", function (e) {
            const btn = e.target.closest("[data-review-id]");
            if (!btn) return;
            openSavedReview(btn.getAttribute("data-review-id")).catch(function (err) {
                showAlert(err.message || "Failed to open review", "danger");
            });
        });
    }
    refreshReviewGate();
    loadSavedReviews().catch(function () { /* ignore */ });
    if (window.DevPilotApi && typeof window.DevPilotApi.refreshRolesFromProfile === "function") {
        window.DevPilotApi.refreshRolesFromProfile().catch(function () { /* ignore */ });
    }

    const params = new URLSearchParams(window.location.search);
    const initialMode = (params.get("mode") || "").toLowerCase() === "review" ? "review" : "debug";
    if (params.get("connected") === "1") {
        showAlert("GitHub connected successfully" + (params.get("user") ? (" as @" + params.get("user")) : "") + ".", "success");
        window.history.replaceState({}, "", "github.html" + (initialMode === "review" ? "?mode=review" : ""));
    } else if (params.get("connected") === "0" || params.get("error")) {
        const err = params.get("error") || "unknown";
        const msg = params.get("message");
        if (err === "access_denied") {
            showAlert("GitHub authorization was cancelled. You can connect again anytime.", "warning");
        } else {
            showAlert(msg || ("GitHub connection failed: " + err), "danger");
        }
        window.history.replaceState({}, "", "github.html");
    }

    setMode(initialMode);
    if (initialMode === "review") {
        refreshReviewGate();
    }
    const deepReviewId = params.get("reviewId");
    if (deepReviewId) {
        openSavedReview(deepReviewId).catch(function (err) {
            showAlert(err.message || "Failed to open saved review", "danger");
        });
        window.history.replaceState({}, "", "github.html" + (initialMode === "review" ? "?mode=review" : ""));
    }
    loadStatus().catch(function (err) {
        showAlert(err.message || "Failed to load GitHub status", "danger");
    });
})();
