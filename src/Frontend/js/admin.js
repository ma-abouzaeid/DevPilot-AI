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
        plans: []
    };

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
        if (tab === "ai") loadAi();
        if (tab === "audit") loadAudit();
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
                return (
                    "<tr>" +
                    "<td><strong>" + name + "</strong><div class='text-gray small'>" + email + " · #" + id + "</div></td>" +
                    "<td>" + plan + "</td>" +
                    "<td>" + (isAdmin ? "Unlimited" : (credits == null ? "—" : credits)) + "</td>" +
                    "<td>" + roles + "</td>" +
                    "<td>" + (deleted ? '<span class="badge text-bg-danger">Deactivated</span>' : '<span class="badge text-bg-success">Active</span>') + "</td>" +
                    "<td class='text-end admin-actions'>" +
                    "<button type='button' class='btn btn-sm btn-outline-card' data-act='edit' data-id='" + id + "'>Edit</button> " +
                    "<button type='button' class='btn btn-sm btn-outline-card' data-act='plan' data-id='" + id + "' data-email='" + email + "'>Plan</button> " +
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
                '<div class="col-12"><label class="form-label">Description</label><textarea class="form-control" name="description" rows="2">' + escapeHtml(val(p, "description", "Description") || "") + "</textarea></div>" +
                '<div class="col-12"><small class="text-gray">Features: ' + escapeHtml(features.join(", ") || "—") + "</small></div>" +
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

    async function loadAi() {
        var res = await api("/api/Admin/ai-usage", null, {
            userId: document.getElementById("ai-user-id").value || undefined,
            status: document.getElementById("ai-status").value || undefined,
            page: state.aiPage,
            pageSize: 50
        });
        var data = res.data || {};
        var items = val(data, "items", "Items") || [];
        var tbody = document.getElementById("ai-tbody");
        if (!items.length) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-gray">No AI usage yet.</td></tr>';
        } else {
            tbody.innerHTML = items.map(function (e) {
                return (
                    "<tr>" +
                    "<td>" + fmtDate(val(e, "createdAt", "CreatedAt")) + "</td>" +
                    "<td>" + escapeHtml(val(e, "userEmail", "UserEmail") || ("#" + val(e, "userId", "UserId"))) + "</td>" +
                    "<td>" + escapeHtml(val(e, "operationType", "OperationType") || "—") + "</td>" +
                    "<td>" + (val(e, "totalTokens", "TotalTokens") == null ? "—" : val(e, "totalTokens", "TotalTokens")) + "</td>" +
                    "<td>" + (val(e, "creditsCharged", "CreditsCharged") == null ? "—" : val(e, "creditsCharged", "CreditsCharged")) + "</td>" +
                    "<td>" + escapeHtml(val(e, "status", "Status") || "—") + "</td>" +
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
    }

    async function loadAudit() {
        var res = await api("/api/Admin/audit-logs", null, {
            userId: document.getElementById("audit-user-id").value || undefined,
            action: document.getElementById("audit-action").value.trim() || undefined,
            page: state.auditPage,
            pageSize: 50
        });
        var data = res.data || {};
        var items = val(data, "items", "Items") || [];
        var tbody = document.getElementById("audit-tbody");
        if (!items.length) {
            tbody.innerHTML = '<tr><td colspan="5" class="text-gray">No audit events.</td></tr>';
        } else {
            tbody.innerHTML = items.map(function (e) {
                var details = val(e, "newValues", "NewValues") || val(e, "oldValues", "OldValues") || "";
                if (details && details.length > 120) details = details.slice(0, 120) + "…";
                return (
                    "<tr>" +
                    "<td>" + fmtDate(val(e, "createdAt", "CreatedAt")) + "</td>" +
                    "<td><code>" + escapeHtml(val(e, "action", "Action")) + "</code></td>" +
                    "<td>" + escapeHtml(val(e, "userEmail", "UserEmail") || (val(e, "userId", "UserId") || "—")) + "</td>" +
                    "<td>" + escapeHtml(val(e, "entityName", "EntityName")) + " #" + escapeHtml(val(e, "entityId", "EntityId")) + "</td>" +
                    "<td class='small text-gray'>" + escapeHtml(details) + "</td>" +
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
        loadAi().catch(onError);
    });
    document.getElementById("audit-refresh").addEventListener("click", function () {
        state.auditPage = 1;
        loadAudit().catch(onError);
    });

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
                enableCodeReview: form.enableCodeReview.checked
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

    ensureAdminAccess().then(function (ok) {
        if (!ok) {
            window.location.href = "dashboard.html";
            return;
        }
        loadOverview().catch(onError);
    }).catch(function () {
        window.location.href = "dashboard.html";
    });
})();
