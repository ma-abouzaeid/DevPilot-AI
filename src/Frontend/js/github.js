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
    const resultCard = document.getElementById("result-card");
    const resultBody = document.getElementById("result-body");

    let selectedRepo = null;
    let selectedPath = null;
    let mode = "debug";

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
            await loadRepos();
        } else {
            connectionText.textContent = "Not connected. Authorize GitHub to browse repos and files.";
            btnConnect.classList.remove("d-none");
            btnDisconnect.classList.add("d-none");
            workspace.classList.add("d-none");
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
        await window.DevPilotApi.request(url, { method: "DELETE" });
        selectedRepo = null;
        selectedPath = null;
        await loadStatus();
    }

    async function loadRepos() {
        reposList.innerHTML = '<div class="empty-state">Loading repositories…</div>';
        const url = new URL("/api/GitHub/repos", API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        const items = res.data || [];
        if (!items.length) {
            reposList.innerHTML = '<div class="empty-state">No repositories found for this account.</div>';
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
        const items = res.data || [];
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

        const btn = document.getElementById("btn-debug");
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Analyzing...';
        try {
            const url = new URL("/api/GitHub/debug", API_BASE).href;
            const res = await window.DevPilotApi.request(url, {
                method: "POST",
                body: JSON.stringify({
                    fullName: selectedRepo.fullName,
                    path: selectedPath,
                    branch: selectedRepo.branch,
                    errorMessage: errorMessage,
                    description: document.getElementById("debug-notes").value.trim() || null
                })
            });
            if (!res.ok || !res.data) throw new Error(res.message || "Debug failed");
            renderDebugResult(res.data);
        } catch (err) {
            showAlert(err.message || "Debug failed", "danger");
        } finally {
            btn.disabled = false;
            btn.innerHTML = '<i class="fa-solid fa-bug"></i> Analyze file with AI';
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
            renderReviewResult(res.data);
        } catch (err) {
            showAlert(err.message || "Review failed", "danger");
        } finally {
            btn.disabled = false;
            btn.innerHTML = '<i class="fa-solid fa-magnifying-glass-chart"></i> Run code review';
            refreshReviewGate();
        }
    }

    function renderDebugResult(data) {
        resultCard.classList.remove("d-none");
        const steps = (data.steps || []).map(function (step) {
            return "<li><strong>" + escapeHtml(step.title || "") + "</strong><br>" + escapeHtml(step.detail || "") + "</li>";
        }).join("");
        resultBody.innerHTML =
            '<p class="mb-2"><span class="badge-soft ai">' + escapeHtml(data.problemCode || "") + "</span> · Provider: <strong>" + escapeHtml(data.providerUsed || "") + "</strong></p>" +
            "<h3 class=\"h6\">Problem</h3><p>" + escapeHtml(data.problem || "") + "</p>" +
            "<h3 class=\"h6\">Root cause</h3><p>" + escapeHtml(data.rootCause || "") + "</p>" +
            "<h3 class=\"h6\">Steps</h3><ol>" + steps + "</ol>" +
            "<h3 class=\"h6\">Corrected code</h3><pre class=\"result-code\"><code>" + escapeHtml(data.correctedCode || "") + "</code></pre>" +
            '<a class="btn btn-outline-card mt-3" style="width:auto;" href="submit-problem.html">Open full debug page</a>';
        resultCard.scrollIntoView({ behavior: "smooth", block: "start" });
    }

    function renderReviewResult(data) {
        resultCard.classList.remove("d-none");
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
            '<p class="mb-2">Provider: <strong>' + escapeHtml(data.providerUsed || data.ProviderUsed || "") + "</strong></p>" +
            "<h3 class=\"h6\">Summary</h3><p>" + escapeHtml(data.summary || data.Summary || "") + "</p>" +
            '<p class="text-gray small">Reviewed: ' + paths + "</p>" +
            "<h3 class=\"h6\">Findings</h3>" + findings;
        resultCard.scrollIntoView({ behavior: "smooth", block: "start" });
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
        selectedFileTitle.textContent = "Selected file";
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
        selectedFileTitle.textContent = selectedPath;
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
    btnDisconnect.addEventListener("click", function () { disconnect().catch(function (err) { showAlert(err.message, "danger"); }); });
    document.getElementById("btn-debug").addEventListener("click", runDebug);
    document.getElementById("btn-review").addEventListener("click", runReview);
    refreshReviewGate();

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
    loadStatus().catch(function (err) {
        showAlert(err.message || "Failed to load GitHub status", "danger");
    });
})();
