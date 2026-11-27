(function () {
    "use strict";

    if (!window.DevPilotApi || !window.DevPilotApi.isAuthenticated()) {
        window.location.href = "login.html?returnUrl=" + encodeURIComponent("admin.html");
        return;
    }

    async function ensureAdminAccess() {
        if (window.DevPilotApi.isAdmin()) return true;
        await window.DevPilotApi.refreshRolesFromProfile();
        return window.DevPilotApi.isAdmin();
    }

    const alertBox = document.getElementById("admin-alert");
    const state = {
        usersPage: 1,
        articlesPage: 1,
        aiPage: 1,
        auditPage: 1,
        errorsPage: 1,
        plans: [],
        detailsCache: {},
        aiCapacity: null
    };

    function prettyJson(raw) {
        if (raw == null || raw === "") return "{}";
        if (typeof raw === "object") {
            try { return JSON.stringify(raw, null, 2); } catch (e) { return String(raw); }
        }
        var text = String(raw);
        try {
            return JSON.stringify(JSON.parse(text), null, 2);
        } catch (e) {
            return text;
        }
    }

    function parseDetailsObject(raw) {
        if (raw == null || raw === "") return null;
        if (typeof raw === "object") return raw;
        try { return JSON.parse(String(raw)); } catch (e) { return null; }
    }

    function openDetailsPopup(title, rawJson) {
        var titleEl = document.getElementById("admin-details-title");
        var jsonEl = document.getElementById("admin-details-json");
        var modalEl = document.getElementById("admin-details-modal");
        if (!titleEl || !jsonEl || !modalEl) {
            window.alert((title || "Details") + "\n\n" + prettyJson(rawJson));
            return;
        }
        titleEl.textContent = title || "Details";
        jsonEl.textContent = prettyJson(rawJson);
        try {
            if (window.bootstrap && bootstrap.Modal) {
                bootstrap.Modal.getOrCreateInstance(modalEl).show();
                return;
            }
        } catch (err) { /* fall through */ }
        modalEl.classList.add("show");
        modalEl.style.display = "block";
        modalEl.removeAttribute("aria-hidden");
        modalEl.setAttribute("aria-modal", "true");
        document.body.classList.add("modal-open");
        if (!document.getElementById("admin-details-backdrop")) {
            var backdrop = document.createElement("div");
            backdrop.className = "modal-backdrop fade show";
            backdrop.id = "admin-details-backdrop";
            document.body.appendChild(backdrop);
        }
    }

    function cacheDetails(key, json) {
        state.detailsCache[key] = json;
        return key;
    }

    function resolveProviderModel(e) {
        var provider = val(e, "providerName", "ProviderName") || "";
        var model = val(e, "modelName", "ModelName") || "";
        if (provider && model) return { provider: provider, model: model };
        var details = parseDetailsObject(val(e, "detailsJson", "DetailsJson"));
        if (details) {
            provider = provider || details.provider || details.Provider || "";
            model = model || details.model || details.Model || "";
            var req = details.aiRequest || details.AIRequest;
            if (req) {
                provider = provider || req.provider || req.Provider || "";
                model = model || req.model || req.Model || "";
            }
        }
        return { provider: provider || "—", model: model || "—" };
    }

    function showAlert(message, type) {
        alertBox.textContent = message;
        alertBox.className = "alert alert-" + (type || "danger");
        alertBox.classList.remove("d-none");
    }

    function clearAlert() {
        alertBox.classList.add("d-none");
        alertBox.textContent = "";
    }

    function val(obj) {
        var keys = Array.prototype.slice.call(arguments, 1);
        for (var i = 0; i < keys.length; i++) {
            if (obj && obj[keys[i]] !== undefined && obj[keys[i]] !== null) return obj[keys[i]];
        }
        return undefined;
    }

    function fmtDate(value) {
        if (!value) return "—";
        try {
            return new Date(value).toLocaleString();
        } catch (e) {
            return String(value);
        }
    }

    function escapeHtml(text) {
        return String(text == null ? "" : text)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function apiUrl(path, params) {
        var url = new URL(path, API_BASE);
        if (params) {
            Object.keys(params).forEach(function (key) {
                var v = params[key];
                if (v !== undefined && v !== null && v !== "") url.searchParams.set(key, v);
            });
        }
        return url.href;
    }

    async function api(path, options, params) {
        var res = await window.DevPilotApi.request(apiUrl(path, params), options || {});
        if (!res.ok) throw new Error(res.message || "Request failed");
        return res;
    }

    async function downloadExcel(path, params, fallbackFileName) {
        var url = apiUrl(path, params);
        var token = window.DevPilotApi.getAccessToken();
        var headers = { "Accept": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" };
        if (token) headers["Authorization"] = "Bearer " + token;

        var response = await fetch(url, { method: "GET", headers: headers });
        if (response.status === 401 && window.DevPilotApi.getRefreshToken) {
            try {
                await window.DevPilotApi.request(apiUrl("/api/Admin/dashboard"), {});
                token = window.DevPilotApi.getAccessToken();
                if (token) headers["Authorization"] = "Bearer " + token;
                response = await fetch(url, { method: "GET", headers: headers });
            } catch (refreshErr) { /* fall through */ }
        }

        if (!response.ok) {
            var errText = "";
            try { errText = await response.text(); } catch (e) { /* ignore */ }
            var msg = "Export failed (" + response.status + ").";
            try {
                var parsed = JSON.parse(errText);
                if (parsed && parsed.message) msg = parsed.message;
            } catch (e) { /* ignore */ }
            throw new Error(msg);
        }

        var blob = await response.blob();
        var fileName = fallbackFileName || "export.xlsx";
        var disposition = response.headers.get("Content-Disposition") || response.headers.get("content-disposition") || "";
        var match = /filename\*?=(?:UTF-8'')?["']?([^"';]+)/i.exec(disposition);
        if (match && match[1]) {
            try { fileName = decodeURIComponent(match[1].trim()); } catch (e) { fileName = match[1].trim(); }
        }

        var objectUrl = URL.createObjectURL(blob);
        var link = document.createElement("a");
        link.href = objectUrl;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(objectUrl); }, 1500);
        showAlert("Excel exported: " + fileName, "success");
        setTimeout(clearAlert, 2500);
    }

    function renderPager(el, page, pageSize, total, onPage) {
        var pages = Math.max(1, Math.ceil(total / pageSize));
        el.innerHTML =
            '<span class="text-gray small">Page ' + page + " / " + pages + " · " + total + " total</span>" +
            '<div class="d-flex gap-2">' +
            '<button type="button" class="btn btn-outline-card btn-sm" data-dir="-1"' + (page <= 1 ? " disabled" : "") + ">Prev</button>" +
            '<button type="button" class="btn btn-outline-card btn-sm" data-dir="1"' + (page >= pages ? " disabled" : "") + ">Next</button>" +
            "</div>";
        el.querySelectorAll("button").forEach(function (btn) {
            btn.addEventListener("click", function () {
                onPage(page + Number(btn.getAttribute("data-dir")));
            });
        });
    }

    function switchTab(tab) {
        document.querySelectorAll("#admin-tabs .nav-link").forEach(function (btn) {
            btn.classList.toggle("active", btn.getAttribute("data-tab") === tab);
        });
        document.querySelectorAll(".admin-panel").forEach(function (panel) {
            panel.classList.toggle("d-none", panel.id !== "tab-" + tab);
        });
        clearAlert();
        if (tab === "overview") loadOverview();
        if (tab === "users") loadUsers();
        if (tab === "plans") loadPlans();
        if (tab === "credits") loadCreditCosts();
        if (tab === "articles") loadArticles();
        if (tab === "ai") {
            loadAiCapacity().catch(onError);
            loadAi().catch(onError);
        }
        if (tab === "audit") loadAudit();
        if (tab === "errors") loadErrors();
        if (tab === "tickets") loadTicketsTab();
        if (tab === "payments") loadPaymentsAdmin();
        if (tab === "feedback") loadFeedbackAdmin().catch(onError);
    }

    async function loadCreditCosts() {
        var res = await api("/api/Admin/credit-costs");
        var items = res.data || [];
        var host = document.getElementById("credits-list");
        if (!items.length) {
            host.innerHTML = '<div class="col-12"><div class="empty-state">No credit costs found.</div></div>';
            return;
        }

        host.innerHTML = items.map(function (item) {
            var key = val(item, "actionKey", "ActionKey");
            var name = val(item, "displayName", "DisplayName") || key;
            var credits = val(item, "credits", "Credits") || 0;
            var show = val(item, "showOnPlansPage", "ShowOnPlansPage");
            return (
                '<div class="col-md-6 col-xl-4">' +
                '<div class="surface-card h-100">' +
                '<label class="form-label">' + escapeHtml(name) + "</label>" +
                '<input type="hidden" class="credit-action-key" value="' + escapeHtml(key) + '">' +
                '<div class="input-group mb-2">' +
                '<input class="form-control credit-action-credits" type="number" min="0" value="' + credits + '">' +
                '<span class="input-group-text">credits</span>' +
                "</div>" +
                '<div class="form-check">' +
                '<input class="form-check-input credit-action-visible" type="checkbox" ' + (show ? "checked" : "") + ">" +
                '<label class="form-check-label">Show on Plans page</label>' +
                "</div></div></div>"
            );
        }).join("");
    }

    async function loadOverview() {
        var res = await api("/api/Admin/overview");
        var d = res.data || {};
        var cards = [
            { label: "Active users", value: val(d, "activeUsers", "ActiveUsers"), icon: "fa-users", tone: "primary" },
            { label: "Deactivated", value: val(d, "deletedUsers", "DeletedUsers"), icon: "fa-user-slash", tone: "error" },
            { label: "Admins", value: val(d, "adminUsers", "AdminUsers"), icon: "fa-shield-halved", tone: "accent" },
            { label: "Published articles", value: val(d, "publishedArticles", "PublishedArticles"), icon: "fa-book-open", tone: "success" },
            { label: "AI calls today", value: val(d, "aiRequestsToday", "AiRequestsToday"), icon: "fa-robot", tone: "primary" },
            { label: "Credits charged today", value: val(d, "creditsChargedToday", "CreditsChargedToday"), icon: "fa-coins", tone: "accent" },
            { label: "Open problems", value: val(d, "openProblems", "OpenProblems"), icon: "fa-bug", tone: "error" },
            { label: "Active subscriptions", value: val(d, "activeSubscriptions", "ActiveSubscriptions"), icon: "fa-crown", tone: "success" }
        ];
        document.getElementById("overview-stats").innerHTML = cards.map(function (c) {
            return (
                '<div class="col-6 col-lg-3"><div class="surface-card stat-card h-100">' +
                '<div class="stat-icon ' + c.tone + '"><i class="fa-solid ' + c.icon + '"></i></div>' +
                '<div class="stat-label">' + c.label + "</div>" +
                '<p class="stat-value">' + (c.value == null ? "—" : c.value) + "</p>" +
                "</div></div>"
            );
        }).join("");
    }

    async function ensurePlans() {
        if (state.plans.length) return state.plans;
        var res = await api("/api/Admin/plans");
        state.plans = res.data || [];
        return state.plans;
    }

    async function loadUsers() {
        var search = document.getElementById("users-search").value.trim();
        var includeDeleted = document.getElementById("users-include-deleted").checked;
        var res = await api("/api/Admin/users", null, {
            search: search,
            includeDeleted: includeDeleted,
            page: state.usersPage,
            pageSize: 20
        });
        var data = res.data || {};
        var items = val(data, "items", "Items") || [];
        var tbody = document.getElementById("users-tbody");
        if (!items.length) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-gray">No users found.</td></tr>';
        } else {
            tbody.innerHTML = items.map(function (u) {
                var id = val(u, "userId", "UserId");
                var name = escapeHtml(val(u, "name", "Name"));
                var email = escapeHtml(val(u, "email", "Email"));
                var plan = escapeHtml(val(u, "planCode", "PlanCode") || "—");
                var credits = val(u, "creditsRemaining", "CreditsRemaining");
                var roles = (val(u, "roles", "Roles") || []).map(escapeHtml).join(", ") || "Developer";
                var deleted = val(u, "isDeleted", "IsDeleted");
                var isAdmin = (val(u, "roles", "Roles") || []).some(function (r) { return String(r).toLowerCase() === "admin"; });
                var unlimited = !!(val(u, "hasUnlimitedCredits", "HasUnlimitedCredits")) || isAdmin;
                return (
                    "<tr>" +
                    "<td><strong>" + name + "</strong><div class='text-gray small'>" + email + " · #" + id + "</div></td>" +
                    "<td>" + plan + "</td>" +
                    "<td>" + (unlimited ? "Unlimited" : (credits == null ? "—" : credits)) + "</td>" +
                    "<td>" + roles + "</td>" +
                    "<td>" + (deleted ? '<span class="badge text-bg-danger">Deactivated</span>' : '<span class="badge text-bg-success">Active</span>') + "</td>" +
                    "<td class='text-end admin-actions'>" +
                    "<button type='button' class='btn btn-sm btn-outline-card' data-act='edit' data-id='" + id + "'>Edit</button> " +
                    "<button type='button' class='btn btn-sm btn-outline-card' data-act='plan' data-id='" + id + "' data-email='" + email + "'>Plan</button> " +
                    "<button type='button' class='btn btn-sm btn-outline-card' data-act='unlimited' data-id='" + id + "' data-unlimited='" +
                    (val(u, "hasUnlimitedCredits", "HasUnlimitedCredits") ? "0" : "1") + "'>" +
                    (val(u, "hasUnlimitedCredits", "HasUnlimitedCredits") ? "Revoke unlimited" : "Make unlimited") + "</button> " +
                    "<button type='button' class='btn btn-sm btn-outline-card' data-act='admin' data-id='" + id + "' data-admin='" + (isAdmin ? "0" : "1") + "'>" +
                    (isAdmin ? "Revoke admin" : "Make admin") + "</button> " +
                    (deleted
                        ? "<button type='button' class='btn btn-sm btn-primary' data-act='restore' data-id='" + id + "'>Restore</button>"
                        : "<button type='button' class='btn btn-sm btn-outline-danger' data-act='deactivate' data-id='" + id + "'>Deactivate</button>") +
                    "</td></tr>"
                );
            }).join("");
        }
        renderPager(
            document.getElementById("users-pager"),
            val(data, "page", "Page") || state.usersPage,
            val(data, "pageSize", "PageSize") || 20,
            val(data, "totalCount", "TotalCount") || 0,
            function (p) { state.usersPage = p; loadUsers().catch(onError); }
        );
    }

    async function loadPlans() {
        state.plans = [];
        var plans = await ensurePlans();
        var box = document.getElementById("plans-list");
        box.innerHTML = plans.map(function (p) {
            var id = val(p, "planId", "PlanId");
            var features = val(p, "features", "Features") || [];
            var hasReview = features.some(function (f) { return String(f).toLowerCase() === "github_code_review"; });
            return (
                '<div class="col-lg-6"><div class="surface-card border h-100">' +
                "<h3 class='h5 mb-1'>" + escapeHtml(val(p, "name", "Name")) +
                " <span class='badge-soft'>" + escapeHtml(val(p, "code", "Code")) + "</span></h3>" +
                "<p class='text-gray small mb-3'>" + escapeHtml(val(p, "description", "Description") || "") + "</p>" +
                '<form class="plan-edit-form" data-id="' + id + '">' +
                '<div class="row g-2">' +
                '<div class="col-md-6"><label class="form-label">Name</label><input class="form-control" name="name" value="' + escapeHtml(val(p, "name", "Name")) + '" required></div>' +
                '<div class="col-md-3"><label class="form-label">Credits</label><input class="form-control" type="number" name="credits" min="0" value="' + (val(p, "creditsPerPeriod", "CreditsPerPeriod") || 0) + '" required></div>' +
                '<div class="col-md-3"><label class="form-label">Price</label><input class="form-control" type="number" step="0.01" name="price" value="' + (val(p, "price", "Price") == null ? "" : val(p, "price", "Price")) + '"></div>' +
                '<div class="col-md-4"><label class="form-label">Currency</label><input class="form-control" name="currency" value="' + escapeHtml(val(p, "currency", "Currency") || "USD") + '"></div>' +
                '<div class="col-md-4"><label class="form-label">Status</label><select class="form-select" name="status">' +
                '<option value="Active"' + (val(p, "status", "Status") === "Active" ? " selected" : "") + '>Active</option>' +
                '<option value="Inactive"' + (val(p, "status", "Status") === "Inactive" ? " selected" : "") + '>Inactive</option>' +
                "</select></div>" +
                '<div class="col-md-4 d-flex align-items-end"><div class="form-check mb-2">' +
                '<input class="form-check-input" type="checkbox" name="isPublic"' + (val(p, "isPublic", "IsPublic") ? " checked" : "") + ">" +
                '<label class="form-check-label">Public</label></div></div>' +
                '<div class="col-md-4"><div class="form-check">' +
                '<input class="form-check-input" type="checkbox" name="isDefault"' + (val(p, "isDefault", "IsDefault") ? " checked" : "") + ">" +
                '<label class="form-check-label">Default</label></div></div>' +
                '<div class="col-md-4"><div class="form-check">' +
                '<input class="form-check-input" type="checkbox" name="enableCodeReview"' + (hasReview ? " checked" : "") + ">" +
                '<label class="form-check-label">Code Review</label></div></div>' +
                '<div class="col-md-4"><div class="form-check">' +
                '<input class="form-check-input" type="checkbox" name="saleEnabled"' + (val(p, "saleEnabled", "SaleEnabled") ? " checked" : "") + ">" +
                '<label class="form-check-label">Sale</label></div></div>' +
                '<div class="col-md-4"><label class="form-label">Sale discount %</label>' +
                '<input class="form-control" type="number" name="saleDiscountPercent" min="0" max="100" step="0.01" value="' +
                (val(p, "saleDiscountPercent", "SaleDiscountPercent") || 0) + '"></div>' +
                '<div class="col-12"><label class="form-label">Description</label><textarea class="form-control" name="description" rows="2">' + escapeHtml(val(p, "description", "Description") || "") + "</textarea></div>" +
                '<div class="col-12"><small class="text-gray">Features: ' + escapeHtml(features.join(", ") || "—") +
                (val(p, "saleEnabled", "SaleEnabled")
                    ? " · Sale active → effective $" + Number(val(p, "effectivePrice", "EffectivePrice") || 0).toFixed(2)
                    : "") +
                "</small></div>" +
                '<div class="col-12"><button type="submit" class="btn btn-primary btn-sm">Save plan</button></div>' +
                "</div></form></div></div>"
            );
        }).join("");
    }

    async function fillPlanSelect(selectEl, selected) {
        await ensurePlans();
        selectEl.innerHTML = '<option value="">Default plan</option>' + state.plans.map(function (p) {
            var code = val(p, "code", "Code");
            return '<option value="' + escapeHtml(code) + '"' + (selected && selected === code ? " selected" : "") + ">" +
                escapeHtml(val(p, "name", "Name")) + " (" + escapeHtml(code) + ")</option>";
        }).join("");
    }

    async function openCreateUserModal() {
        document.getElementById("user-form-title").textContent = "Add user";
        document.getElementById("user-form-id").value = "";
        document.getElementById("user-form-name").value = "";
        document.getElementById("user-form-email").value = "";
        document.getElementById("user-form-password").value = "";
        document.getElementById("user-form-password").required = true;
        document.getElementById("user-form-password-hint").textContent = "Required. At least 8 chars with upper, lower, and a number.";
        document.getElementById("user-form-plan-wrap").classList.remove("d-none");
        document.getElementById("user-form-admin-wrap").classList.remove("d-none");
        document.getElementById("user-form-admin").checked = false;
        document.getElementById("user-form-verified").checked = true;
        await fillPlanSelect(document.getElementById("user-form-plan"));
        bootstrap.Modal.getOrCreateInstance(document.getElementById("user-form-modal")).show();
    }

    async function openEditUserModal(userId) {
        var res = await api("/api/Admin/users/" + userId);
        var u = res.data || {};
        document.getElementById("user-form-title").textContent = "Edit user";
        document.getElementById("user-form-id").value = val(u, "userId", "UserId");
        document.getElementById("user-form-name").value = val(u, "name", "Name") || "";
        document.getElementById("user-form-email").value = val(u, "email", "Email") || "";
        document.getElementById("user-form-password").value = "";
        document.getElementById("user-form-password").required = false;
        document.getElementById("user-form-password-hint").textContent = "Leave blank to keep the current password.";
        document.getElementById("user-form-plan-wrap").classList.add("d-none");
        document.getElementById("user-form-admin-wrap").classList.add("d-none");
        document.getElementById("user-form-verified").checked = !!(val(u, "emailVerifiedAt", "EmailVerifiedAt"));
        bootstrap.Modal.getOrCreateInstance(document.getElementById("user-form-modal")).show();
    }

    async function loadArticles() {
        var published = document.getElementById("articles-published").value;
        var res = await api("/api/Admin/articles", null, {
            search: document.getElementById("articles-search").value.trim(),
            isPublished: published === "" ? undefined : published,
            includeDeleted: document.getElementById("articles-include-deleted").checked,
            page: state.articlesPage,
            pageSize: 20
        });
        var data = res.data || {};
        var items = val(data, "items", "Items") || [];
        var tbody = document.getElementById("articles-tbody");
        if (!items.length) {
            tbody.innerHTML = '<tr><td colspan="3" class="text-gray">No articles found.</td></tr>';
        } else {
            tbody.innerHTML = items.map(function (a) {
                var id = val(a, "knowledgeArticleId", "KnowledgeArticleId");
                var deleted = val(a, "isDeleted", "IsDeleted");
                var publishedFlag = val(a, "isPublished", "IsPublished");
                var stateBadge = deleted
                    ? '<span class="badge text-bg-secondary">Deleted</span>'
                    : (publishedFlag ? '<span class="badge text-bg-success">Published</span>' : '<span class="badge text-bg-warning">Disabled</span>');
                return (
                    "<tr>" +
                    "<td><strong>" + escapeHtml(val(a, "title", "Title")) + "</strong>" +
                    "<div class='text-gray small'>" + escapeHtml(val(a, "articleCode", "ArticleCode")) + "</div></td>" +
                    "<td>" + stateBadge + "</td>" +
                    "<td class='text-end admin-actions'>" +
                    "<button type='button' class='btn btn-sm btn-outline-card' data-act='edit' data-id='" + id + "'>Edit</button> " +
                    (!deleted && publishedFlag ? "<button type='button' class='btn btn-sm btn-outline-card' data-act='unpublish' data-id='" + id + "'>Disable</button> " : "") +
                    (!deleted && !publishedFlag ? "<button type='button' class='btn btn-sm btn-outline-card' data-act='publish' data-id='" + id + "'>Publish</button> " : "") +
                    (deleted
                        ? "<button type='button' class='btn btn-sm btn-primary' data-act='restore' data-id='" + id + "'>Restore</button>"
                        : "<button type='button' class='btn btn-sm btn-outline-danger' data-act='delete' data-id='" + id + "'>Delete</button>") +
                    "</td></tr>"
                );
            }).join("");
        }
        renderPager(
            document.getElementById("articles-pager"),
            val(data, "page", "Page") || state.articlesPage,
            val(data, "pageSize", "PageSize") || 20,
            val(data, "totalCount", "TotalCount") || 0,
            function (p) { state.articlesPage = p; loadArticles().catch(onError); }
        );
    }

    async function openArticle(id) {
        var res = await api("/api/Admin/articles/" + id);
        var a = res.data || {};
        document.getElementById("article-editor-empty").classList.add("d-none");
        document.getElementById("article-form").classList.remove("d-none");
        document.getElementById("article-id").value = val(a, "knowledgeArticleId", "KnowledgeArticleId");
        document.getElementById("article-title").value = val(a, "title", "Title") || "";
        document.getElementById("article-summary").value = val(a, "summary", "Summary") || "";
        document.getElementById("article-content").value = val(a, "contentMarkdown", "ContentMarkdown") || "";
        document.getElementById("article-reason").value = "";
    }

    function round1(n) {
        return Math.round((Number(n) || 0) * 10) / 10;
    }

    function formatInt(n) {
        var v = Math.round(Number(n) || 0);
        return v.toLocaleString();
    }

    function renderAiCapacityCards() {
        var host = document.getElementById("ai-capacity-cards");
        if (!host || !state.aiCapacity) return;
        var d = state.aiCapacity;
        var ops = val(d, "operations", "Operations") || [];
        var providers = val(d, "providers", "Providers") || [];
        var costs = val(d, "creditCosts", "CreditCosts") || [];
        var freeCredits = Number(val(d, "freePlanCredits", "FreePlanCredits") || 100);
        var proCredits = Number(val(d, "proPlanCredits", "ProPlanCredits") || 1000);
        var avgTokPerCredit = Number(val(d, "avgTokensPerChargedCredit", "AvgTokensPerChargedCredit") || 0);
        var chargedCount = Number(val(d, "chargedEventCount", "ChargedEventCount") || 0);
        var failedCount = Number(val(d, "failedEventCount", "FailedEventCount") || 0);
        var tokensPerFreeUser = avgTokPerCredit > 0 ? avgTokPerCredit * freeCredits : 0;

        var costCards = costs.map(function (c) {
            var key = val(c, "actionKey", "ActionKey") || "";
            var name = val(c, "displayName", "DisplayName") || key;
            var credits = Number(val(c, "credits", "Credits") || 0);
            var op = ops.find(function (o) {
                return String(val(o, "operation", "Operation") || "").toLowerCase() === String(key).toLowerCase();
            });
            var avgTokens = op ? Number(val(op, "avgTotalTokens", "AvgTotalTokens") || 0) : (avgTokPerCredit * credits);
            return (
                '<div class="col-6 col-lg-3"><div class="surface-card stat-card h-100">' +
                '<div class="stat-label">' + escapeHtml(name) + "</div>" +
                '<p class="stat-value">' + credits + " cr</p>" +
                '<div class="small text-gray">~' + formatInt(avgTokens) + " tokens / call</div>" +
                "</div></div>"
            );
        }).join("");

        var providerCards = providers.map(function (p) {
            var name = val(p, "provider", "Provider") || "—";
            var model = val(p, "model", "Model") || "";
            var ok = Number(val(p, "successCount", "SuccessCount") || 0);
            var bad = Number(val(p, "failCount", "FailCount") || 0);
            var total = ok + bad;
            var rate = total ? Math.round((ok / total) * 100) : null;
            var err = val(p, "lastError", "LastError") || "";
            var status = val(p, "status", "Status") || "Active";
            var priority = val(p, "priority", "Priority");
            var mainValue = rate == null ? status : (rate + "%");
            var detail = total
                ? (ok + " ok / " + bad + " fail")
                : "No calls yet";
            if (priority != null && priority !== "") detail = "P" + priority + " · " + detail;
            return (
                '<div class="col-6 col-lg-3"><div class="surface-card stat-card h-100">' +
                '<div class="stat-label">' + escapeHtml(name) + (model ? (" · " + escapeHtml(model)) : "") + "</div>" +
                '<p class="stat-value">' + escapeHtml(String(mainValue)) + "</p>" +
                '<div class="small text-gray">' + escapeHtml(detail) + "</div>" +
                (err ? '<div class="small text-danger mt-1">' + escapeHtml(String(err).slice(0, 90)) + "</div>" : "") +
                "</div></div>"
            );
        }).join("");

        var summaryCards =
            '<div class="col-6 col-lg-4"><div class="surface-card stat-card h-100">' +
            '<div class="stat-label">Tokens / credit (avg)</div>' +
            '<p class="stat-value">' + formatInt(avgTokPerCredit) + "</p>" +
            '<div class="small text-gray">From ' + chargedCount + " charged / " + failedCount + " failed</div>" +
            "</div></div>" +
            '<div class="col-6 col-lg-4"><div class="surface-card stat-card h-100">' +
            '<div class="stat-label">Tokens / Free user / month</div>' +
            '<p class="stat-value">' + (tokensPerFreeUser ? formatInt(tokensPerFreeUser) : "—") + "</p>" +
            '<div class="small text-gray">' + freeCredits + " Free credits × avg tokens</div>" +
            "</div></div>" +
            '<div class="col-6 col-lg-4"><div class="surface-card stat-card h-100">' +
            '<div class="stat-label">Plan credits</div>' +
            '<p class="stat-value">F ' + freeCredits + " / P " + proCredits + "</p>" +
            '<div class="small text-gray">Free / Pro per period</div>' +
            "</div></div>";

        var opRows = ops.map(function (o) {
            return (
                '<div class="col-6 col-lg-3"><div class="surface-card stat-card h-100">' +
                '<div class="stat-label">' + escapeHtml(val(o, "operation", "Operation") || "—") + "</div>" +
                '<p class="stat-value">' + formatInt(val(o, "avgTotalTokens", "AvgTotalTokens")) + "</p>" +
                '<div class="small text-gray">avg tokens · ' +
                round1(val(o, "avgCredits", "AvgCredits")) + " cr · " +
                Number(val(o, "chargedCount", "ChargedCount") || 0) + " ok / " +
                Number(val(o, "failedCount", "FailedCount") || 0) + " fail</div>" +
                "</div></div>"
            );
        }).join("");

        host.innerHTML =
            summaryCards +
            '<div class="col-12"><h3 class="h6 mb-0 mt-1">Credit costs vs observed tokens</h3></div>' +
            costCards +
            '<div class="col-12"><h3 class="h6 mb-0 mt-1">Provider health</h3></div>' +
            (providerCards || '<div class="col-12 text-gray small">No providers configured.</div>') +
            '<div class="col-12"><h3 class="h6 mb-0 mt-1">Per operation (charged)</h3></div>' +
            (opRows || '<div class="col-12 text-gray small">No usage yet.</div>');
    }

    async function loadAiCapacity() {
        var res = await api("/api/Admin/ai-usage/capacity");
        state.aiCapacity = res.data || {};
        renderAiCapacityCards();
    }

    async function loadAi() {
        var tbody = document.getElementById("ai-tbody");
        tbody.innerHTML = '<tr><td colspan="8" class="text-gray">Loading…</td></tr>';
        try {
            var res = await api("/api/Admin/ai-usage", null, {
                userId: document.getElementById("ai-user-id").value || undefined,
                status: document.getElementById("ai-status").value || undefined,
                page: state.aiPage,
                pageSize: 50
            });
            var data = res.data || {};
            var items = val(data, "items", "Items") || [];
            if (!items.length) {
                tbody.innerHTML = '<tr><td colspan="8" class="text-gray">No AI usage yet.</td></tr>';
            } else {
                tbody.innerHTML = items.map(function (e, index) {
                    var names = resolveProviderModel(e);
                    var status = val(e, "status", "Status") || "—";
                    var detailsRaw = val(e, "detailsJson", "DetailsJson");
                    var detailsObj = parseDetailsObject(detailsRaw);
                    var errorMessage = val(e, "errorMessage", "ErrorMessage")
                        || (detailsObj && (detailsObj.errorMessage || detailsObj.ErrorMessage))
                        || val(e, "errorCode", "ErrorCode")
                        || "";
                    var cacheKey = cacheDetails(
                        "ai-" + (val(e, "aiUsageEventId", "AIUsageEventId") || index),
                        detailsRaw || e
                    );
                    return (
                        "<tr>" +
                        "<td>" + fmtDate(val(e, "createdAt", "CreatedAt")) + "</td>" +
                        "<td>" + escapeHtml(val(e, "userEmail", "UserEmail") || ("#" + val(e, "userId", "UserId"))) + "</td>" +
                        "<td>" + escapeHtml(val(e, "operationType", "OperationType") || "—") + "</td>" +
                        "<td><div><strong>" + escapeHtml(names.provider) + "</strong></div>" +
                        '<div class="small text-gray">' + escapeHtml(names.model) + "</div>" +
                        (String(status).toLowerCase() === "failed" && errorMessage
                            ? '<div class="small text-danger mt-1">' + escapeHtml(String(errorMessage).slice(0, 120)) + (String(errorMessage).length > 120 ? "…" : "") + "</div>"
                            : "") +
                        "</td>" +
                        "<td>" + (val(e, "totalTokens", "TotalTokens") == null ? "—" : val(e, "totalTokens", "TotalTokens")) + "</td>" +
                        "<td>" + (val(e, "creditsCharged", "CreditsCharged") == null ? "—" : val(e, "creditsCharged", "CreditsCharged")) + "</td>" +
                        "<td>" + escapeHtml(status) + "</td>" +
                        '<td><button type="button" class="btn btn-outline-card btn-sm" style="width:auto;" data-details="' +
                        escapeHtml(cacheKey) + '" data-details-title="AI usage details">View</button></td>' +
                        "</tr>"
                    );
                }).join("");
            }
            renderPager(
                document.getElementById("ai-pager"),
                val(data, "page", "Page") || state.aiPage,
                val(data, "pageSize", "PageSize") || 50,
                val(data, "totalCount", "TotalCount") || 0,
                function (p) { state.aiPage = p; loadAi().catch(onError); }
            );
        } catch (err) {
            tbody.innerHTML = '<tr><td colspan="8" class="text-danger">Failed to load AI usage.</td></tr>';
            throw err;
        }
    }

    async function loadAudit() {
        var tbody = document.getElementById("audit-tbody");
        tbody.innerHTML = '<tr><td colspan="6" class="text-gray">Loading…</td></tr>';
        try {
            var res = await api("/api/Admin/audit-logs", null, {
                userId: document.getElementById("audit-user-id").value || undefined,
                action: document.getElementById("audit-action").value.trim() || undefined,
                page: state.auditPage,
                pageSize: 50
            });
            var data = res.data || {};
            var items = val(data, "items", "Items") || [];
            if (!items.length) {
                tbody.innerHTML = '<tr><td colspan="6" class="text-gray">No audit events.</td></tr>';
            } else {
                tbody.innerHTML = items.map(function (e, index) {
                    var preview = val(e, "newValues", "NewValues") || val(e, "oldValues", "OldValues") || "";
                    if (preview && preview.length > 100) preview = preview.slice(0, 100) + "…";
                    var cacheKey = cacheDetails(
                        "audit-" + (val(e, "auditLogId", "AuditLogId") || index),
                        val(e, "detailsJson", "DetailsJson") || e
                    );
                    return (
                        "<tr>" +
                        "<td>" + fmtDate(val(e, "createdAt", "CreatedAt")) + "</td>" +
                        "<td><code>" + escapeHtml(val(e, "action", "Action")) + "</code></td>" +
                        "<td>" + escapeHtml(val(e, "userEmail", "UserEmail") || (val(e, "userId", "UserId") || "—")) + "</td>" +
                        "<td>" + escapeHtml(val(e, "entityName", "EntityName")) + " #" + escapeHtml(val(e, "entityId", "EntityId")) + "</td>" +
                        '<td class="small text-gray admin-json-preview" role="button" tabindex="0" title="Open full JSON" data-details="' +
                        escapeHtml(cacheKey) + '" data-details-title="Audit details">' + escapeHtml(preview) + "</td>" +
                        '<td><button type="button" class="btn btn-outline-card btn-sm" style="width:auto;" data-details="' +
                        escapeHtml(cacheKey) + '" data-details-title="Audit details">View</button></td>' +
                        "</tr>"
                    );
                }).join("");
            }
            renderPager(
                document.getElementById("audit-pager"),
                val(data, "page", "Page") || state.auditPage,
                val(data, "pageSize", "PageSize") || 50,
                val(data, "totalCount", "TotalCount") || 0,
                function (p) { state.auditPage = p; loadAudit().catch(onError); }
            );
        } catch (err) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-danger">Failed to load audit log.</td></tr>';
            throw err;
        }
    }

    async function loadErrors() {
        var tbody = document.getElementById("errors-tbody");
        tbody.innerHTML = '<tr><td colspan="7" class="text-gray">Loading…</td></tr>';
        try {
            var res = await api("/api/Admin/system-errors", null, {
                userId: document.getElementById("errors-user-id").value || undefined,
                pathContains: document.getElementById("errors-path").value.trim() || undefined,
                errorCode: document.getElementById("errors-code").value.trim() || undefined,
                minStatusCode: document.getElementById("errors-min-status").value || undefined,
                page: state.errorsPage,
                pageSize: 50
            });
            var data = res.data || {};
            var items = val(data, "items", "Items") || [];
            if (!items.length) {
                tbody.innerHTML = '<tr><td colspan="7" class="text-gray">No system errors logged yet. Clear filters, then run <code>deployment/AddSystemErrorLogs.sql</code> on the DB if this stays empty after real API failures.</td></tr>';
            } else {
                tbody.innerHTML = items.map(function (e, index) {
                    var path = val(e, "path", "Path") || "—";
                    var method = val(e, "httpMethod", "HttpMethod") || "";
                    var action = val(e, "action", "Action") || "—";
                    var message = val(e, "message", "Message") || "";
                    var cacheKey = cacheDetails(
                        "err-" + (val(e, "systemErrorLogId", "SystemErrorLogId") || index),
                        val(e, "detailsJson", "DetailsJson") || e
                    );
                    return (
                        "<tr>" +
                        "<td>" + fmtDate(val(e, "createdAt", "CreatedAt")) + "</td>" +
                        "<td><strong>" + escapeHtml(val(e, "statusCode", "StatusCode")) + "</strong></td>" +
                        "<td><code>" + escapeHtml(method + " " + path) + "</code></td>" +
                        "<td class='small'>" + escapeHtml(action) + "</td>" +
                        "<td><div><code>" + escapeHtml(val(e, "errorCode", "ErrorCode") || "—") + "</code></div>" +
                        '<div class="small text-gray">' + escapeHtml(String(message).slice(0, 100)) + (String(message).length > 100 ? "…" : "") + "</div></td>" +
                        "<td>" + escapeHtml(val(e, "userEmail", "UserEmail") || (val(e, "userId", "UserId") || "—")) + "</td>" +
                        '<td><button type="button" class="btn btn-outline-card btn-sm" style="width:auto;" data-details="' +
                        escapeHtml(cacheKey) + '" data-details-title="System error details">View</button></td>' +
                        "</tr>"
                    );
                }).join("");
            }
            renderPager(
                document.getElementById("errors-pager"),
                val(data, "page", "Page") || state.errorsPage,
                val(data, "pageSize", "PageSize") || 50,
                val(data, "totalCount", "TotalCount") || 0,
                function (p) { state.errorsPage = p; loadErrors().catch(onError); }
            );
        } catch (err) {
            tbody.innerHTML = '<tr><td colspan="7" class="text-danger">Failed to load system errors (API unavailable or SystemErrorLogs table missing).</td></tr>';
            throw err;
        }
    }

    function onError(err) {
        showAlert(err && err.message ? err.message : "Something went wrong.", "danger");
    }

    document.querySelectorAll("#admin-tabs .nav-link").forEach(function (btn) {
        btn.addEventListener("click", function () {
            switchTab(btn.getAttribute("data-tab"));
        });
    });

    document.getElementById("users-refresh").addEventListener("click", function () {
        state.usersPage = 1;
        loadUsers().catch(onError);
    });
    document.getElementById("users-add").addEventListener("click", function () {
        openCreateUserModal().catch(onError);
    });
    document.getElementById("articles-refresh").addEventListener("click", function () {
        state.articlesPage = 1;
        loadArticles().catch(onError);
    });
    document.getElementById("ai-refresh").addEventListener("click", function () {
        state.aiPage = 1;
        loadAiCapacity().catch(onError);
        loadAi().catch(onError);
    });
    document.getElementById("ai-export").addEventListener("click", function () {
        clearAlert();
        downloadExcel("/api/Admin/ai-usage/export", {
            userId: document.getElementById("ai-user-id").value || undefined,
            status: document.getElementById("ai-status").value || undefined
        }, "ai-usage.xlsx").catch(onError);
    });
    document.getElementById("audit-refresh").addEventListener("click", function () {
        state.auditPage = 1;
        loadAudit().catch(onError);
    });
    document.getElementById("audit-export").addEventListener("click", function () {
        clearAlert();
        downloadExcel("/api/Admin/audit-logs/export", {
            userId: document.getElementById("audit-user-id").value || undefined,
            action: document.getElementById("audit-action").value.trim() || undefined
        }, "audit-logs.xlsx").catch(onError);
    });
    document.getElementById("errors-refresh").addEventListener("click", function () {
        state.errorsPage = 1;
        loadErrors().catch(onError);
    });
    document.getElementById("errors-export").addEventListener("click", function () {
        clearAlert();
        downloadExcel("/api/Admin/system-errors/export", {
            userId: document.getElementById("errors-user-id").value || undefined,
            pathContains: document.getElementById("errors-path").value.trim() || undefined,
            errorCode: document.getElementById("errors-code").value.trim() || undefined,
            minStatusCode: document.getElementById("errors-min-status").value || undefined
        }, "system-errors.xlsx").catch(onError);
    });

    function closeDetailsPopup() {
        var modalEl = document.getElementById("admin-details-modal");
        if (!modalEl) return;
        try {
            if (window.bootstrap && bootstrap.Modal) {
                var inst = bootstrap.Modal.getInstance(modalEl);
                if (inst) inst.hide();
            }
        } catch (err) { /* fall through */ }
        modalEl.classList.remove("show");
        modalEl.style.display = "none";
        modalEl.setAttribute("aria-hidden", "true");
        modalEl.removeAttribute("aria-modal");
        document.body.classList.remove("modal-open");
        var backdrop = document.getElementById("admin-details-backdrop");
        if (backdrop) backdrop.remove();
    }

    document.body.addEventListener("click", function (e) {
        if (e.target.closest("[data-admin-details-close]") || e.target.id === "admin-details-backdrop") {
            closeDetailsPopup();
            return;
        }
        var btn = e.target.closest("[data-details]");
        if (!btn) return;
        e.preventDefault();
        var key = btn.getAttribute("data-details");
        openDetailsPopup(btn.getAttribute("data-details-title") || "Details", state.detailsCache[key] || "{}");
    });

    document.body.addEventListener("keydown", function (e) {
        if (e.key !== "Enter" && e.key !== " ") return;
        var el = e.target.closest("[data-details].admin-json-preview");
        if (!el) return;
        e.preventDefault();
        openDetailsPopup(el.getAttribute("data-details-title") || "Details", state.detailsCache[el.getAttribute("data-details")] || "{}");
    });

    var copyBtn = document.getElementById("admin-details-copy");
    if (copyBtn) {
        copyBtn.addEventListener("click", async function () {
            var text = document.getElementById("admin-details-json").textContent || "";
            try {
                await navigator.clipboard.writeText(text);
                copyBtn.textContent = "Copied";
                setTimeout(function () { copyBtn.textContent = "Copy JSON"; }, 1200);
            } catch (err) {
                showAlert("Could not copy JSON.", "warning");
            }
        });
    }

    document.getElementById("users-tbody").addEventListener("click", async function (e) {
        var btn = e.target.closest("button[data-act]");
        if (!btn) return;
        var id = btn.getAttribute("data-id");
        var act = btn.getAttribute("data-act");
        clearAlert();
        try {
            if (act === "edit") {
                await openEditUserModal(id);
            } else if (act === "deactivate") {
                if (!window.confirm("Deactivate this account? They will be signed out.")) return;
                await api("/api/Admin/users/" + id + "/deactivate", { method: "POST" });
                showAlert("User deactivated.", "success");
                await loadUsers();
            } else if (act === "restore") {
                await api("/api/Admin/users/" + id + "/restore", { method: "POST" });
                showAlert("User restored.", "success");
                await loadUsers();
            } else if (act === "unlimited") {
                var makeUnlimited = btn.getAttribute("data-unlimited") === "1";
                await api("/api/Admin/users/" + id + "/unlimited-credits", {
                    method: "POST",
                    body: JSON.stringify({ hasUnlimitedCredits: makeUnlimited })
                });
                showAlert(makeUnlimited ? "Unlimited credits granted." : "Unlimited credits revoked.", "success");
                await loadUsers();
            } else if (act === "admin") {
                var makeAdmin = btn.getAttribute("data-admin") === "1";
                await api("/api/Admin/users/" + id + "/admin-role", {
                    method: "POST",
                    body: JSON.stringify({ isAdmin: makeAdmin })
                });
                showAlert(makeAdmin ? "Admin role granted." : "Admin role revoked.", "success");
                await loadUsers();
            } else if (act === "plan") {
                await ensurePlans();
                document.getElementById("plan-modal-user-id").value = id;
                document.getElementById("plan-modal-user-label").textContent = btn.getAttribute("data-email") || ("User #" + id);
                var select = document.getElementById("plan-modal-code");
                select.innerHTML = state.plans.map(function (p) {
                    return '<option value="' + escapeHtml(val(p, "code", "Code")) + '">' +
                        escapeHtml(val(p, "name", "Name")) + " (" + escapeHtml(val(p, "code", "Code")) + ")</option>";
                }).join("");
                bootstrap.Modal.getOrCreateInstance(document.getElementById("user-plan-modal")).show();
            }
        } catch (err) {
            onError(err);
        }
    });

    document.getElementById("plan-modal-save").addEventListener("click", async function () {
        clearAlert();
        try {
            var userId = document.getElementById("plan-modal-user-id").value;
            var planCode = document.getElementById("plan-modal-code").value;
            await api("/api/Admin/users/" + userId + "/plan", {
                method: "POST",
                body: JSON.stringify({ planCode: planCode })
            });
            bootstrap.Modal.getOrCreateInstance(document.getElementById("user-plan-modal")).hide();
            showAlert("Plan assigned.", "success");
            await loadUsers();
        } catch (err) {
            onError(err);
        }
    });

    document.getElementById("user-form").addEventListener("submit", async function (e) {
        e.preventDefault();
        clearAlert();
        try {
            var id = document.getElementById("user-form-id").value;
            var name = document.getElementById("user-form-name").value.trim();
            var email = document.getElementById("user-form-email").value.trim();
            var password = document.getElementById("user-form-password").value;
            var verified = document.getElementById("user-form-verified").checked;

            if (id) {
                var updateBody = {
                    name: name,
                    email: email,
                    markEmailVerified: verified
                };
                if (password) updateBody.newPassword = password;
                await api("/api/Admin/users/" + id, { method: "PUT", body: JSON.stringify(updateBody) });
                showAlert("User updated.", "success");
            } else {
                if (!password) throw new Error("Password is required for new users.");
                await api("/api/Admin/users", {
                    method: "POST",
                    body: JSON.stringify({
                        name: name,
                        email: email,
                        password: password,
                        planCode: document.getElementById("user-form-plan").value || null,
                        isAdmin: document.getElementById("user-form-admin").checked,
                        markEmailVerified: verified
                    })
                });
                showAlert("User created.", "success");
            }
            bootstrap.Modal.getOrCreateInstance(document.getElementById("user-form-modal")).hide();
            state.usersPage = 1;
            await loadUsers();
        } catch (err) {
            onError(err);
        }
    });

    document.getElementById("plan-create-form").addEventListener("submit", async function (e) {
        e.preventDefault();
        clearAlert();
        try {
            await api("/api/Admin/plans", {
                method: "POST",
                body: JSON.stringify({
                    code: document.getElementById("new-plan-code").value.trim(),
                    name: document.getElementById("new-plan-name").value.trim(),
                    description: document.getElementById("new-plan-description").value.trim(),
                    status: "Active",
                    isPublic: document.getElementById("new-plan-public").checked,
                    isDefault: document.getElementById("new-plan-default").checked,
                    creditsPerPeriod: Number(document.getElementById("new-plan-credits").value),
                    price: document.getElementById("new-plan-price").value === "" ? null : Number(document.getElementById("new-plan-price").value),
                    currency: document.getElementById("new-plan-currency").value.trim() || "USD",
                    enableCodeReview: document.getElementById("new-plan-review").checked
                })
            });
            document.getElementById("plan-create-form").reset();
            document.getElementById("new-plan-public").checked = true;
            document.getElementById("new-plan-credits").value = "200";
            document.getElementById("new-plan-currency").value = "USD";
            showAlert("Plan created.", "success");
            await loadPlans();
        } catch (err) {
            onError(err);
        }
    });

    document.getElementById("plans-list").addEventListener("submit", async function (e) {
        var form = e.target.closest(".plan-edit-form");
        if (!form) return;
        e.preventDefault();
        clearAlert();
        try {
            var id = form.getAttribute("data-id");
            var body = {
                name: form.name.value.trim(),
                description: form.description.value.trim(),
                status: form.status.value,
                isPublic: form.isPublic.checked,
                isDefault: form.isDefault.checked,
                creditsPerPeriod: Number(form.credits.value),
                price: form.price.value === "" ? null : Number(form.price.value),
                currency: form.currency.value.trim(),
                enableCodeReview: form.enableCodeReview.checked,
                saleEnabled: form.saleEnabled.checked,
                saleDiscountPercent: Number(form.saleDiscountPercent.value || 0)
            };
            await api("/api/Admin/plans/" + id, { method: "PUT", body: JSON.stringify(body) });
            state.plans = [];
            showAlert("Plan updated.", "success");
            await loadPlans();
        } catch (err) {
            onError(err);
        }
    });

    document.getElementById("articles-tbody").addEventListener("click", async function (e) {
        var btn = e.target.closest("button[data-act]");
        if (!btn) return;
        var id = btn.getAttribute("data-id");
        var act = btn.getAttribute("data-act");
        clearAlert();
        try {
            if (act === "edit") {
                await openArticle(id);
            } else if (act === "publish") {
                await api("/api/Admin/articles/" + id + "/publish", { method: "POST" });
                showAlert("Article published.", "success");
                await loadArticles();
            } else if (act === "unpublish") {
                await api("/api/Admin/articles/" + id + "/unpublish", { method: "POST" });
                showAlert("Article disabled.", "success");
                await loadArticles();
            } else if (act === "delete") {
                if (!window.confirm("Soft-delete this article?")) return;
                await api("/api/Admin/articles/" + id, { method: "DELETE" });
                showAlert("Article deleted.", "success");
                await loadArticles();
            } else if (act === "restore") {
                await api("/api/Admin/articles/" + id + "/restore", { method: "POST" });
                showAlert("Article restored.", "success");
                await loadArticles();
            }
        } catch (err) {
            onError(err);
        }
    });

    document.getElementById("article-form").addEventListener("submit", async function (e) {
        e.preventDefault();
        clearAlert();
        try {
            var id = document.getElementById("article-id").value;
            await api("/api/Admin/articles/" + id, {
                method: "PUT",
                body: JSON.stringify({
                    title: document.getElementById("article-title").value.trim(),
                    summary: document.getElementById("article-summary").value.trim(),
                    contentMarkdown: document.getElementById("article-content").value,
                    changeReason: document.getElementById("article-reason").value.trim() || "Admin edit"
                })
            });
            showAlert("Article saved.", "success");
            await loadArticles();
        } catch (err) {
            onError(err);
        }
    });

    document.getElementById("credits-save").addEventListener("click", async function () {
        clearAlert();
        try {
            var cards = document.querySelectorAll("#credits-list .surface-card");
            var items = Array.prototype.map.call(cards, function (card) {
                return {
                    actionKey: card.querySelector(".credit-action-key").value,
                    credits: Number(card.querySelector(".credit-action-credits").value),
                    showOnPlansPage: card.querySelector(".credit-action-visible").checked
                };
            });
            await api("/api/Admin/credit-costs", {
                method: "PUT",
                body: JSON.stringify({ items: items })
            });
            showAlert("Credit costs updated.", "success");
            await loadCreditCosts();
        } catch (err) {
            onError(err);
        }
    });

    var ticketsState = {
        activeId: null,
        hub: null,
        joinedId: null
    };

    function ticketStatusBadge(status) {
        var s = String(status || "").toLowerCase();
        if (s === "assigned") return '<span class="badge-soft status-progress">Assigned</span>';
        if (s === "resolved") return '<span class="badge-soft solved">Resolved</span>';
        return '<span class="badge-soft open">Open</span>';
    }

    function formatTicketDate(value) {
        if (!value) return "";
        try { return new Date(value).toLocaleString(); } catch (e) { return String(value); }
    }

    function myAdminUserId() {
        var claims = window.DevPilotApi.getTokenClaims() || {};
        return claims.nameid || claims.sub || claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] || "";
    }

    async function ensureTicketsHub() {
        if (ticketsState.hub && ticketsState.hub.state === signalR.HubConnectionState.Connected) {
            return ticketsState.hub;
        }
        if (ticketsState.hub) {
            try { await ticketsState.hub.stop(); } catch (e) { /* ignore */ }
            ticketsState.hub = null;
            ticketsState.joinedId = null;
        }
        if (!window.signalR) throw new Error("SignalR client failed to load.");

        var hubOptions = {
            accessTokenFactory: function () { return window.DevPilotApi.getAccessToken(); }
        };
        if (window.DEVPILOT_SIGNALR_LONG_POLLING) {
            // On Vercel use LongPolling; WebSockets often fail behind rewrites.
            hubOptions.transport = signalR.HttpTransportType.LongPolling;
        }

        ticketsState.hub = new signalR.HubConnectionBuilder()
            .withUrl(new URL("/hubs/support-tickets", API_BASE).href, hubOptions)
            .withAutomaticReconnect()
            .build();

        ticketsState.hub.on("adminNotification", function (note) {
            var ticketId = note && (note.supportTicketId || note.SupportTicketId);
            if (ticketsState.activeId && ticketId && Number(ticketId) === Number(ticketsState.activeId)) {
                markAdminTicketNotificationsRead(ticketsState.activeId);
                return;
            }
            refreshTicketNotifications().catch(function () { /* ignore */ });
        });
        ticketsState.hub.on("messageReceived", function (msg) {
            var ticketId = msg.supportTicketId || msg.SupportTicketId;
            if (Number(ticketId) !== Number(ticketsState.activeId)) return;
            appendAdminChatMessage(msg);
            markAdminTicketNotificationsRead(ticketId);
        });
        ticketsState.hub.on("ticketUpdated", function () {
            if (ticketsState.activeId) openAdminTicket(ticketsState.activeId, false);
            loadAdminTicketsList().catch(function () { /* ignore */ });
        });
        ticketsState.hub.on("ticketResolved", function () {
            if (ticketsState.activeId) openAdminTicket(ticketsState.activeId, false);
            loadAdminTicketsList().catch(function () { /* ignore */ });
        });

        try {
            await ticketsState.hub.start();
        } catch (err) {
            ticketsState.hub = null;
            throw err;
        }
        return ticketsState.hub;
    }

    async function joinAdminTicketRoom(ticketId) {
        var conn = await ensureTicketsHub();
        if (conn.state !== signalR.HubConnectionState.Connected) {
            throw new Error("Live chat is not connected yet. Retry in a moment.");
        }
        if (ticketsState.joinedId && ticketsState.joinedId !== ticketId) {
            try { await conn.invoke("LeaveTicket", ticketsState.joinedId); } catch (e) { /* ignore */ }
        }
        await conn.invoke("JoinTicket", ticketId);
        ticketsState.joinedId = ticketId;
    }

    function appendAdminChatMessage(msg) {
        var box = document.getElementById("admin-chat-messages");
        if (!box) return;
        var empty = box.querySelector(".empty-state");
        if (empty) empty.remove();
        // Bubble side from role, not sender id (same-account tests).
        var fromAdmin = !!(msg.isFromAdmin || msg.IsFromAdmin);
        var row = document.createElement("div");
        row.className = "support-chat-bubble " + (fromAdmin ? "is-admin is-mine" : "is-user");
        var role = fromAdmin ? "Admin" : "User";
        row.innerHTML =
            '<div class="support-chat-meta"><span><span class="role-tag">' + role + "</span><strong>" +
            escapeHtml(msg.senderName || msg.SenderName || role) + "</strong></span>" +
            "<span>" + escapeHtml(formatTicketDate(msg.createdAt || msg.CreatedAt)) + "</span></div>" +
            "<p>" + escapeHtml(msg.body || msg.Body || "") + "</p>";
        box.appendChild(row);
        box.scrollTop = box.scrollHeight;
    }

    function setTicketsUnreadBadge(count) {
        var badge = document.getElementById("tickets-unread-badge");
        if (!badge) return;
        var n = Number(count) || 0;
        badge.textContent = String(n);
        badge.classList.toggle("d-none", n < 1);
    }

    async function refreshTicketNotifications() {
        var res = await api("/api/support-tickets/admin/notifications");
        var data = res.data || {};
        var unread = data.unread ?? data.Unread ?? 0;
        setTicketsUnreadBadge(unread);
        if (window.DevPilotNotifications && typeof window.DevPilotNotifications.refresh === "function") {
            window.DevPilotNotifications.refresh();
        }
    }

    async function loadAdminTicketsList() {
        var status = document.getElementById("tickets-status").value;
        var res = await api("/api/support-tickets", null, status ? { status: status } : null);
        var items = res.data || [];
        // Show open-ticket count even if notifications are late.
        var openCount = items.filter(function (t) {
            return String(t.status || t.Status || "").toLowerCase() === "open";
        }).length;
        if (!status || String(status).toLowerCase() === "open" || String(status).toLowerCase() === "all") {
            var badge = document.getElementById("tickets-unread-badge");
            var current = badge && !badge.classList.contains("d-none") ? Number(badge.textContent) || 0 : 0;
            if (openCount > current) setTicketsUnreadBadge(openCount);
        }
        var host = document.getElementById("admin-tickets-list");
        if (!items.length) {
            host.innerHTML = '<div class="empty-state">No tickets for this filter.</div>';
            return;
        }
        host.innerHTML = items.map(function (t) {
            var id = t.supportTicketId || t.SupportTicketId;
            return (
                '<button type="button" class="support-ticket-item' + (Number(id) === Number(ticketsState.activeId) ? " is-active" : "") + '" data-id="' + id + '">' +
                '<div class="d-flex justify-content-between gap-2"><strong>' + escapeHtml(t.ticketCode || t.TicketCode || "") + "</strong>" +
                ticketStatusBadge(t.status || t.Status) + "</div>" +
                '<div class="text-gray small mt-1">' + escapeHtml(t.title || t.Title || "") + "</div>" +
                '<div class="text-gray small">By ' + escapeHtml(t.createdByName || t.CreatedByName || "") +
                (t.assignedAdminName || t.AssignedAdminName ? (" · " + escapeHtml(t.assignedAdminName || t.AssignedAdminName)) : "") +
                "</div></button>"
            );
        }).join("");

        host.querySelectorAll("[data-id]").forEach(function (btn) {
            btn.addEventListener("click", function () {
                openAdminTicket(Number(btn.getAttribute("data-id")), true);
            });
        });
    }

    function renderAdminTicketDetail(ticket, messages) {
        var id = ticket.supportTicketId || ticket.SupportTicketId;
        var status = ticket.status || ticket.Status || "";
        var statusLower = String(status).toLowerCase();
        // Allow accept when status is open even if canAccept lags.
        var canAccept = !!(ticket.canAccept || ticket.CanAccept) || statusLower === "open";
        var canResolve = !!(ticket.canResolve || ticket.CanResolve);
        var canChat = !!(ticket.canChat || ticket.CanChat);
        var resolution = ticket.resolutionSummary || ticket.ResolutionSummary || "";
        var userName = ticket.createdByName || ticket.CreatedByName || "—";
        var adminName = ticket.assignedAdminName || ticket.AssignedAdminName || "—";

        var actions = "";
        if (canAccept && statusLower === "open") {
            actions += '<button type="button" class="btn btn-primary btn-sm" id="admin-accept-ticket">Accept ticket</button>';
        }
        if (canResolve || statusLower === "assigned" || statusLower === "open") {
            actions +=
                '<form id="admin-resolve-form" class="support-resolve-box w-100">' +
                '<label class="form-label" for="admin-resolution">How was it resolved?</label>' +
                '<textarea class="form-control mb-2" id="admin-resolution" rows="3" minlength="2" maxlength="4000" required placeholder="e.g. Fixed CORS config or plan activated"></textarea>' +
                '<button type="submit" class="btn btn-success btn-sm" id="admin-resolve-btn">Resolve ticket</button>' +
                "</form>";
        }

        var chatShell =
            '<div class="support-chat" id="admin-chat-panel">' +
            '<div class="support-chat-label"><span>Conversation</span>' +
            '<div class="support-chat-legend"><span class="leg-user">User left</span><span class="leg-admin">Admin right</span></div></div>' +
            '<div class="support-chat-messages" id="admin-chat-messages"></div>';

        var chatHtml = "";
        if (statusLower === "resolved") {
            chatHtml =
                '<div class="alert alert-success-soft mt-3 mb-3"><strong>Resolved</strong><br>' +
                escapeHtml(resolution || "No notes.") +
                "</div>" + chatShell + "</div>";
        } else if (canChat) {
            chatHtml =
                chatShell +
                '<form id="admin-chat-form" class="support-chat-form">' +
                '<input type="text" class="form-control" id="admin-chat-input" maxlength="4000" placeholder="Reply to the user…" required>' +
                '<button type="submit" class="btn btn-primary">Send</button>' +
                "</form></div>";
        } else if (statusLower === "assigned") {
            chatHtml = '<div class="alert alert-warning mt-3 mb-0">This ticket is assigned to another admin. Chat is locked to the assignee only.</div>';
        } else {
            chatHtml = '<div class="text-gray small mt-3">Accept the ticket to unlock chat with the user.</div>';
        }

        var detail = document.getElementById("admin-ticket-detail");
        detail.innerHTML =
            '<div class="support-ticket-head">' +
            "<div><p class=\"ticket-code\">" + escapeHtml(ticket.ticketCode || ticket.TicketCode || "") + "</p>" +
            '<p class="text-gray small mb-0">' + escapeHtml(formatTicketDate(ticket.createdAt || ticket.CreatedAt)) + "</p></div>" +
            ticketStatusBadge(status) + "</div>" +
            "<h3 class=\"h6 mb-1\">" + escapeHtml(ticket.title || ticket.Title || "") + "</h3>" +
            '<p class="text-gray mb-0">' + escapeHtml(ticket.description || ticket.Description || "") + "</p>" +
            '<ul class="support-ticket-meta-row">' +
            "<li><strong>User</strong> " + escapeHtml(userName) + "</li>" +
            "<li><strong>Admin</strong> " + escapeHtml(adminName) + "</li>" +
            "</ul>" +
            '<div class="d-flex flex-wrap gap-2 mt-3">' + actions + "</div>" + chatHtml;

        var box = document.getElementById("admin-chat-messages");
        if (box) {
            if (!messages.length) box.innerHTML = '<div class="empty-state">No messages yet.</div>';
            else messages.forEach(appendAdminChatMessage);
        }

        var acceptBtn = document.getElementById("admin-accept-ticket");
        if (acceptBtn) {
            acceptBtn.addEventListener("click", async function () {
                try {
                    await api("/api/support-tickets/" + id + "/accept", { method: "POST" });
                    showAlert("Ticket accepted. You can chat now.", "success");
                    await openAdminTicket(id, true);
                    await loadAdminTicketsList();
                } catch (err) { onError(err); }
            });
        }

        var resolveForm = document.getElementById("admin-resolve-form");
        if (resolveForm) {
            resolveForm.addEventListener("submit", async function (e) {
                e.preventDefault();
                e.stopPropagation();
                var summary = (document.getElementById("admin-resolution").value || "").trim();
                if (summary.length < 2) {
                    showAlert("Write at least a short resolution note before closing.", "warning");
                    return;
                }
                var btn = document.getElementById("admin-resolve-btn");
                if (btn) btn.disabled = true;
                try {
                    await api("/api/support-tickets/" + id + "/resolve", {
                        method: "POST",
                        body: JSON.stringify({ resolutionSummary: summary })
                    });
                    showAlert("Ticket resolved.", "success");
                    await openAdminTicket(id, false);
                    await loadAdminTicketsList();
                } catch (err) {
                    onError(err);
                    if (btn) btn.disabled = false;
                }
            });
        }

        var chatForm = document.getElementById("admin-chat-form");
        if (chatForm) {
            chatForm.addEventListener("submit", async function (e) {
                e.preventDefault();
                var input = document.getElementById("admin-chat-input");
                var body = (input.value || "").trim();
                if (!body) return;
                chatForm.querySelector("button").disabled = true;
                try {
                    var conn = null;
                    try { conn = await ensureTicketsHub(); } catch (hubErr) { conn = null; }
                    if (conn && conn.state === signalR.HubConnectionState.Connected) {
                        if (Number(ticketsState.joinedId) !== Number(id)) {
                            await joinAdminTicketRoom(id);
                        }
                        await conn.invoke("SendMessage", id, body, true);
                    } else {
                        var sent = await api("/api/support-tickets/" + id + "/messages", {
                            method: "POST",
                            body: JSON.stringify({ body: body, asAdmin: true })
                        });
                        if (sent.data) appendAdminChatMessage(sent.data);
                    }
                    input.value = "";
                } catch (err) {
                    onError(err);
                } finally {
                    chatForm.querySelector("button").disabled = false;
                }
            });
        }
    }

    async function markAdminTicketNotificationsRead(ticketId) {
        if (!ticketId) return;
        try {
            await api("/api/support-tickets/admin/notifications/read?ticketId=" + encodeURIComponent(ticketId), {
                method: "POST"
            });
            await refreshTicketNotifications();
        } catch (e) { /* ignore */ }
    }

    async function openAdminTicket(ticketId, joinChat) {
        ticketsState.activeId = ticketId;
        var detail = document.getElementById("admin-ticket-detail");
        detail.innerHTML = '<div class="empty-state">Loading ticket…</div>';
        try {
            var detailRes = await api("/api/support-tickets/" + ticketId);
            var ticket = detailRes.data;
            var messages = [];
            var canChat = !!(ticket.canChat || ticket.CanChat);
            var statusLower = String(ticket.status || ticket.Status || "").toLowerCase();
            if (canChat || statusLower === "resolved") {
                var msgRes = await api("/api/support-tickets/" + ticketId + "/messages");
                messages = msgRes.data || [];
            }
            renderAdminTicketDetail(ticket, messages);
            await loadAdminTicketsList();
            markAdminTicketNotificationsRead(ticketId);
            // SignalR join is optional; don't clear the ticket UI on failure.
            if (canChat && joinChat) {
                joinAdminTicketRoom(ticketId).catch(function () { /* REST chat still works */ });
            }
        } catch (err) {
            detail.innerHTML = '<div class="empty-state text-danger">' + escapeHtml(err.message || "Failed") + "</div>";
        }
    }

    async function loadTicketsTab() {
        await Promise.all([
            refreshTicketNotifications(),
            loadAdminTicketsList(),
            ensureTicketsHub().catch(function () { /* notifications still work via refresh */ })
        ]);
    }

    async function loadPaymentsAdmin() {
        var tbody = document.getElementById("payments-tbody");
        if (!tbody) return;
        var res = await api("/api/Payments");
        var items = res.data || [];
        if (!items.length) {
            tbody.innerHTML = '<tr><td colspan="7" class="text-gray">No payment orders.</td></tr>';
            return;
        }
        tbody.innerHTML = items.map(function (p) {
            var code = escapeHtml(val(p, "merchantOrderId", "MerchantOrderId"));
            var plan = escapeHtml(val(p, "planCode", "PlanCode"));
            var status = String(val(p, "status", "Status") || "");
            var paid = val(p, "paidAt", "PaidAt");
            var userId = val(p, "userId", "UserId");
            var email = escapeHtml(val(p, "userEmail", "UserEmail") || ("#" + userId));
            var activate = "<button type='button' class='btn btn-sm btn-primary' data-act='activate-plan' data-plan='" + plan +
                "' data-user='" + userId + "'>Activate " + plan + "</button>";
            return "<tr><td><code>" + code + "</code><div class='small text-gray'>" + email + "</div></td><td>" + plan +
                "</td><td>" + escapeHtml(status) + "</td>" +
                "<td>$" + Number(val(p, "amountUsd", "AmountUsd") || 0).toFixed(2) + "</td>" +
                "<td class='small'>" + new Date(val(p, "createdAt", "CreatedAt")).toLocaleString() + "</td>" +
                "<td class='small'>" + (paid ? new Date(paid).toLocaleString() : "—") + "</td>" +
                "<td>" + activate + "</td></tr>";
        }).join("");
    }

    async function loadFeedbackAdmin() {
        var host = document.getElementById("feedback-list");
        if (!host) return;
        host.innerHTML = '<div class="text-gray">Loading…</div>';
        var res;
        try {
            res = await api("/api/Feedback");
        } catch (err) {
            host.innerHTML = '<div class="empty-state text-danger">' +
                escapeHtml(err.message || "Could not load feedback.") + "</div>";
            throw err;
        }
        var items = res.data || [];
        if (!items.length) {
            host.innerHTML = '<div class="empty-state">No feedback yet.</div>';
            return;
        }
        host.innerHTML = items.map(function (f) {
            var id = val(f, "userFeedbackId", "UserFeedbackId");
            var status = val(f, "status", "Status");
            var rating = Number(val(f, "rating", "Rating") || 0);
            var stars = rating > 0
                ? "<span class='feedback-stars-read' aria-label='" + rating + " of 5 stars'>" +
                    "★★★★★".slice(0, rating) + "<span class='feedback-stars-empty'>" + "★★★★★".slice(rating) + "</span></span>"
                : "<span class='small text-gray'>No rating</span>";
            return '<div class="border-bottom py-3">' +
                "<div class='d-flex justify-content-between gap-2'><strong>" +
                escapeHtml(val(f, "category", "Category") || "General") +
                "</strong><span class='badge-soft'>" + escapeHtml(status) + "</span></div>" +
                "<div class='small text-gray mb-1'>" + escapeHtml(val(f, "userEmail", "UserEmail") || "Anonymous") +
                " · " + escapeHtml(val(f, "source", "Source")) +
                " · " + new Date(val(f, "createdAt", "CreatedAt")).toLocaleString() +
                " · " + stars + "</div>" +
                "<p class='mb-2'>" + escapeHtml(val(f, "message", "Message")) + "</p>" +
                (String(status).toLowerCase() === "open"
                    ? "<button type='button' class='btn btn-sm btn-outline-card' data-act='review-feedback' data-id='" + id + "'>Mark reviewed</button>"
                    : "") +
                "</div>";
        }).join("");
    }

    var ticketsRefresh = document.getElementById("tickets-refresh");
    if (ticketsRefresh) {
        ticketsRefresh.addEventListener("click", function () {
            loadTicketsTab().catch(onError);
        });
    }
    var ticketsStatus = document.getElementById("tickets-status");
    if (ticketsStatus) {
        ticketsStatus.addEventListener("change", function () {
            loadAdminTicketsList().catch(onError);
        });
    }
    var paymentsRefresh = document.getElementById("payments-refresh");
    if (paymentsRefresh) {
        paymentsRefresh.addEventListener("click", function () {
            loadPaymentsAdmin().catch(onError);
        });
    }
    var paymentsTbody = document.getElementById("payments-tbody");
    if (paymentsTbody) {
        paymentsTbody.addEventListener("click", async function (e) {
            var btn = e.target.closest("button[data-act='activate-plan']");
            if (!btn) return;
            try {
                var userId = btn.getAttribute("data-user");
                var planCode = btn.getAttribute("data-plan");
                await api("/api/Admin/users/" + userId + "/plan", {
                    method: "POST",
                    body: JSON.stringify({ planCode: planCode })
                });
                showAlert("Plan " + planCode + " assigned to user #" + userId + ".", "success");
            } catch (err) { onError(err); }
        });
    }

    var feedbackRefresh = document.getElementById("feedback-refresh");
    if (feedbackRefresh) {
        feedbackRefresh.addEventListener("click", function () {
            loadFeedbackAdmin().catch(onError);
        });
    }
    var feedbackList = document.getElementById("feedback-list");
    if (feedbackList) {
        feedbackList.addEventListener("click", async function (e) {
            var btn = e.target.closest("button[data-act='review-feedback']");
            if (!btn) return;
            try {
                await api("/api/Feedback/" + btn.getAttribute("data-id") + "/reviewed", { method: "POST" });
                showAlert("Feedback marked reviewed.", "success");
                await loadFeedbackAdmin();
            } catch (err) { onError(err); }
        });
    }

    ensureAdminAccess().then(function (ok) {
        if (!ok) {
            window.location.href = "dashboard.html";
            return;
        }
        loadOverview().catch(onError);
        refreshTicketNotifications().catch(function () { /* ignore */ });
        ensureTicketsHub().catch(function () { /* ignore until tickets tab */ });

        var params = new URLSearchParams(window.location.search);
        var tab = (params.get("tab") || "").toLowerCase();
        var ticketParam = params.get("ticket");
        if (tab === "tickets" || ticketParam) {
            switchTab("tickets");
            loadTicketsTab().then(function () {
                if (ticketParam) {
                    return openAdminTicket(Number(ticketParam), true);
                }
            }).catch(onError);
        }
    }).catch(function () {
        window.location.href = "dashboard.html";
    });
})();
