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

    function renderNavbar(isAdmin) {
        const authButtonsHtml = isLoggedIn
            ? `<div class="nav-auth-actions d-flex flex-wrap gap-2">
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
    }

    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape") closeSidebar();
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
