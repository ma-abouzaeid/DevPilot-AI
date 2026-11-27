// navbar and app sidebar
const navbarBox = document.querySelector("#navbar");

if (navbarBox) {
    const isLoggedIn = (typeof window.DevPilotApi !== "undefined" && typeof window.DevPilotApi.isAuthenticated === "function")
        ? window.DevPilotApi.isAuthenticated()
        : Boolean(localStorage.getItem("accessToken"));

    const activePage = navbarBox.dataset.active || "";
    function linkClass(page) {
        return ' class="nav-link' + (activePage === page ? " active" : "") + '" data-page="' + page + '"' +
            (activePage === page ? ' aria-current="page"' : "");
    }

    function sideLink(page, href, label, icon) {
        const active = activePage === page ? " is-active" : "";
        return (
            '<a class="app-sidebar-link' + active + '" data-page="' + page + '" href="' + href + '">' +
            '<i class="' + icon + '" aria-hidden="true"></i><span>' + label + "</span></a>"
        );
    }

    function closeSidebar() {
        document.body.classList.remove("app-sidebar-open");
        const toggle = document.getElementById("app-sidebar-toggle");
        if (toggle) toggle.setAttribute("aria-expanded", "false");
        const panel = document.getElementById("app-sidebar");
        if (panel) panel.setAttribute("aria-hidden", "true");
    }

    function openSidebar() {
        document.body.classList.add("app-sidebar-open");
        const toggle = document.getElementById("app-sidebar-toggle");
        if (toggle) toggle.setAttribute("aria-expanded", "true");
        const panel = document.getElementById("app-sidebar");
        if (panel) panel.setAttribute("aria-hidden", "false");
    }

    function toggleSidebar() {
        if (document.body.classList.contains("app-sidebar-open")) closeSidebar();
        else openSidebar();
    }

    function escapeHtml(value) {
        return String(value == null ? "" : value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function isAdminAccount() {
        return !!(window.DevPilotApi && typeof window.DevPilotApi.isAdmin === "function" && window.DevPilotApi.isAdmin());
    }

    function notificationsListUrl() {
        return isAdminAccount()
            ? "/api/support-tickets/admin/notifications"
            : "/api/support-tickets/notifications";
    }

    function notificationsReadUrl(id) {
        var path = isAdminAccount()
            ? "/api/support-tickets/admin/notifications/read"
            : "/api/support-tickets/notifications/read";
        if (id != null && id !== "") path += "?id=" + encodeURIComponent(id);
        return path;
    }

    function fmtNoteDate(value) {
        if (!value) return "";
        try { return new Date(value).toLocaleString(); } catch (e) { return String(value); }
    }

    function setBellUnread(count) {
        var badge = document.getElementById("nav-notif-badge");
        if (!badge) return;
        var n = Number(count) || 0;
        badge.textContent = n > 99 ? "99+" : String(n);
        badge.classList.toggle("d-none", n < 1);
        badge.setAttribute("aria-hidden", n < 1 ? "true" : "false");
    }

    function closeNotifDropdown() {
        var menu = document.getElementById("nav-notif-menu");
        var btn = document.getElementById("nav-notif-btn");
        if (menu) menu.classList.add("d-none");
        if (btn) btn.setAttribute("aria-expanded", "false");
    }

    function toggleNotifDropdown() {
        var menu = document.getElementById("nav-notif-menu");
        var btn = document.getElementById("nav-notif-btn");
        if (!menu || !btn) return;
        var open = menu.classList.contains("d-none");
        menu.classList.toggle("d-none", !open);
        btn.setAttribute("aria-expanded", open ? "true" : "false");
        if (open) refreshNavNotifications();
    }

    function ticketTargetUrl(ticketId) {
        if (isAdminAccount()) {
            return "admin.html?tab=tickets&ticket=" + encodeURIComponent(ticketId);
        }
        return "support.html?ticket=" + encodeURIComponent(ticketId);
    }

    async function openNotification(noteId, ticketId) {
        try {
            var base = (typeof window !== "undefined" && window.API_BASE) ? window.API_BASE : "";
            var url = new URL(notificationsReadUrl(noteId), base || window.location.origin).href;
            await window.DevPilotApi.request(url, { method: "POST" });
        } catch (e) { /* still navigate */ }

        closeNotifDropdown();
        refreshNavNotifications();

        if (!ticketId) return;

        var path = (window.location.pathname || "").toLowerCase();
        if (isAdminAccount() && path.indexOf("admin") >= 0) {
            window.location.href = ticketTargetUrl(ticketId);
            return;
        }
        if (!isAdminAccount() && path.indexOf("support") >= 0) {
            window.location.href = ticketTargetUrl(ticketId);
            return;
        }
        window.location.href = ticketTargetUrl(ticketId);
    }

    async function refreshNavNotifications() {
        var list = document.getElementById("nav-notif-list");
        if (!list || !window.DevPilotApi) return;
        try {
            var base = (typeof window !== "undefined" && window.API_BASE) ? window.API_BASE : "";
            var url = new URL(notificationsListUrl(), base || window.location.origin).href;
            var res = await window.DevPilotApi.request(url);
            if (!res.ok) throw new Error(res.message || "Failed");
            var data = res.data || {};
            var items = data.items || data.Items || [];
            var unread = data.unread ?? data.Unread ?? 0;
            setBellUnread(unread);

            if (!items.length) {
                list.innerHTML = '<div class="nav-notif-empty">No notifications yet.</div>';
                return;
            }

            list.innerHTML = items.slice(0, 20).map(function (n) {
                var id = n.adminNotificationId || n.AdminNotificationId;
                var ticketId = n.supportTicketId || n.SupportTicketId || "";
                var read = !!(n.isRead || n.IsRead);
                var title = n.title || n.Title || "Notification";
                var body = n.body || n.Body || "";
                var when = fmtNoteDate(n.createdAt || n.CreatedAt);
                return (
                    '<button type="button" class="nav-notif-item' + (read ? "" : " is-unread") + '" data-note-id="' +
                    escapeHtml(String(id)) + '" data-ticket-id="' + escapeHtml(String(ticketId)) + '">' +
                    '<div class="nav-notif-title">' + escapeHtml(title) + "</div>" +
                    '<div class="nav-notif-body">' + escapeHtml(body) + "</div>" +
                    (when ? '<div class="nav-notif-time">' + escapeHtml(when) + "</div>" : "") +
                    "</button>"
                );
            }).join("");
        } catch (err) {
            list.innerHTML = '<div class="nav-notif-empty">Could not load notifications.</div>';
        }
    }

    function bindNotificationsUi() {
        var btn = document.getElementById("nav-notif-btn");
        var menu = document.getElementById("nav-notif-menu");
        var list = document.getElementById("nav-notif-list");
        var markAll = document.getElementById("nav-notif-mark-all");
        if (!btn || !menu || !list) return;

        btn.addEventListener("click", function (e) {
            e.stopPropagation();
            toggleNotifDropdown();
        });

        menu.addEventListener("click", function (e) {
            e.stopPropagation();
        });

        list.addEventListener("click", function (e) {
            var item = e.target.closest("[data-note-id]");
            if (!item) return;
            openNotification(item.getAttribute("data-note-id"), item.getAttribute("data-ticket-id"));
        });

        if (markAll) {
            markAll.addEventListener("click", async function () {
                try {
                    var base = (typeof window !== "undefined" && window.API_BASE) ? window.API_BASE : "";
                    var url = new URL(notificationsReadUrl(), base || window.location.origin).href;
                    await window.DevPilotApi.request(url, { method: "POST" });
                    await refreshNavNotifications();
                } catch (err) { /* ignore */ }
            });
        }

        if (!window.__navNotifDocBound) {
            document.addEventListener("click", function () {
                closeNotifDropdown();
            });
            window.__navNotifDocBound = true;
        }

        window.DevPilotNotifications = {
            refresh: function () { return refreshNavNotifications(); }
        };

        refreshNavNotifications();
    }

    function renderNavbar(isAdmin) {
        const notifHtml = isLoggedIn
            ? `<div class="nav-notif dropdown">
                 <button type="button" class="nav-notif-btn" id="nav-notif-btn" aria-label="Notifications" aria-expanded="false" aria-haspopup="true">
                   <i class="fa-solid fa-bell" aria-hidden="true"></i>
                   <span class="nav-notif-badge d-none" id="nav-notif-badge" aria-hidden="true">0</span>
                 </button>
                 <div class="nav-notif-menu d-none" id="nav-notif-menu" role="menu">
                   <div class="nav-notif-head">
                     <strong>Notifications</strong>
                     <button type="button" class="btn btn-outline-card btn-sm" id="nav-notif-mark-all" style="width:auto;">Mark all read</button>
                   </div>
                   <div class="nav-notif-list" id="nav-notif-list">
                     <div class="nav-notif-empty">Loading…</div>
                   </div>
                 </div>
               </div>`
            : "";

        const authButtonsHtml = isLoggedIn
            ? `<div class="nav-auth-actions d-flex flex-wrap align-items-center gap-2">
                 ${notifHtml}
                 <a class="btn btn-outline-light" href="dashboard.html">Dashboard</a>
                 <a class="btn btn-outline-light" href="settings.html">Settings</a>
                 <button type="button" class="btn btn-danger" id="nav-logout-btn">Log Out</button>
               </div>`
            : `<div class="nav-auth-actions d-flex gap-2">
                 <a class="btn btn-outline-light" href="login.html">Login</a>
                 <a class="btn btn-primary" href="register.html">Get Started</a>
               </div>`;

        const supportSide = isLoggedIn
            ? sideLink("support", "support.html", "Support", "fa-solid fa-headset")
            : "";
        const adminSide = isAdmin
            ? sideLink("admin", "admin.html", "Admin", "fa-solid fa-shield-halved")
            : "";
        const accountSide = isLoggedIn
            ? (
                '<div class="app-sidebar-section">' +
                '<p class="app-sidebar-heading">Account</p>' +
                sideLink("dashboard", "dashboard.html", "Dashboard", "fa-solid fa-gauge-high") +
                sideLink("settings", "settings.html", "Settings", "fa-solid fa-gear") +
                adminSide +
                "</div>"
            )
            : (
                '<div class="app-sidebar-section">' +
                '<p class="app-sidebar-heading">Account</p>' +
                sideLink("login", "login.html", "Login", "fa-solid fa-right-to-bracket") +
                sideLink("register", "register.html", "Get Started", "fa-solid fa-user-plus") +
                "</div>"
            );

        navbarBox.innerHTML = `
        <nav class="navbar navbar-expand-lg nav-brand" data-bs-theme="light">
            <div class="container nav-shell">
                <div class="nav-start">
                    <button type="button" class="app-sidebar-toggle" id="app-sidebar-toggle"
                        aria-controls="app-sidebar" aria-expanded="false" aria-label="Open menu">
                        <span class="app-sidebar-toggle-bars" aria-hidden="true"></span>
                    </button>
                    <a class="navbar-brand logo" href="index.html" aria-label="DevPilot AI home">
                        <img src="images/logo.svg" alt="DevPilot AI" class="logo-img logo-svg">
                    </a>
                </div>
                <ul class="navbar-nav nav-primary mb-0">
                    <li class="nav-item"><a${linkClass("home")} href="index.html">Home</a></li>
                    <li class="nav-item"><a${linkClass("debug")} href="submit-problem.html">Debug</a></li>
                    <li class="nav-item"><a${linkClass("github")} href="github.html">GitHub</a></li>
                </ul>
                ${authButtonsHtml}
            </div>
        </nav>
        <div class="app-sidebar-backdrop" id="app-sidebar-backdrop" hidden></div>
        <aside class="app-sidebar" id="app-sidebar" aria-hidden="true" aria-label="Site menu">
            <div class="app-sidebar-top">
                <div>
                    <p class="app-sidebar-kicker">DevPilot AI</p>
                    <h2 class="app-sidebar-title">Menu</h2>
                </div>
                <button type="button" class="app-sidebar-close" id="app-sidebar-close" aria-label="Close menu">
                    <i class="fa-solid fa-xmark" aria-hidden="true"></i>
                </button>
            </div>
            <nav class="app-sidebar-nav">
                <div class="app-sidebar-section">
                    <p class="app-sidebar-heading">Workspace</p>
                    ${sideLink("debug", "submit-problem.html", "Debug", "fa-solid fa-bug")}
                    ${sideLink("github", "github.html", "GitHub", "fa-brands fa-github")}
                    ${sideLink("articles", "articles.html", "Articles", "fa-solid fa-book-open")}
                    ${sideLink("challenges", "challenges.html", "Challenges", "fa-solid fa-trophy")}
                </div>
                <div class="app-sidebar-section">
                    <p class="app-sidebar-heading">Billing & help</p>
                    ${sideLink("plans", "plans.html", "Plans", "fa-solid fa-crown")}
                    ${supportSide}
                    ${sideLink("about", "about.html", "About", "fa-solid fa-circle-info")}
                    ${sideLink("community", "community.html", "Community", "fa-solid fa-users")}
                </div>
                ${accountSide}
            </nav>
        </aside>`;

        const toggle = document.getElementById("app-sidebar-toggle");
        const closeBtn = document.getElementById("app-sidebar-close");
        const backdrop = document.getElementById("app-sidebar-backdrop");
        if (toggle) toggle.addEventListener("click", toggleSidebar);
        if (closeBtn) closeBtn.addEventListener("click", closeSidebar);
        if (backdrop) {
            backdrop.hidden = false;
            backdrop.addEventListener("click", closeSidebar);
        }

        document.querySelectorAll(".app-sidebar-link").forEach(function (link) {
            link.addEventListener("click", closeSidebar);
        });

        const logoutBtn = navbarBox.querySelector("#nav-logout-btn");
        if (logoutBtn) {
            logoutBtn.addEventListener("click", function () {
                if (window.DevPilotApi && typeof window.DevPilotApi.logout === "function") {
                    window.DevPilotApi.logout("login.html");
                } else {
                    localStorage.removeItem("accessToken");
                    localStorage.removeItem("refreshToken");
                    localStorage.removeItem("userRoles");
                    window.location.href = "login.html";
                }
            });
        }

        if (isLoggedIn) {
            bindNotificationsUi();
        }
    }

    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape") {
            closeSidebar();
            closeNotifDropdown();
        }
    });

    const initialAdmin = isLoggedIn
        && typeof window.DevPilotApi !== "undefined"
        && typeof window.DevPilotApi.isAdmin === "function"
        && window.DevPilotApi.isAdmin();

    renderNavbar(initialAdmin);

    // JWT may be stale after Admin grant; refresh roles for sidebar.
    if (isLoggedIn && window.DevPilotApi && typeof window.DevPilotApi.refreshRolesFromProfile === "function") {
        window.DevPilotApi.refreshRolesFromProfile().then(function () {
            if (window.DevPilotApi.isAdmin() && !initialAdmin) {
                renderNavbar(true);
            }
        }).catch(function () { /* keep current navbar */ });
    }
}
