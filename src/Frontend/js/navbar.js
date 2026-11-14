// navbar
const navbarBox = document.querySelector("#navbar");

if (navbarBox) {
    const isLoggedIn = (typeof window.DevPilotApi !== "undefined" && typeof window.DevPilotApi.isAuthenticated === "function")
        ? window.DevPilotApi.isAuthenticated()
        : Boolean(localStorage.getItem("accessToken"));

    function renderNavbar(isAdmin) {
        const authButtonsHtml = isLoggedIn
            ? `<div class="d-flex flex-wrap gap-2">
                 <a class="btn btn-outline-light" href="dashboard.html">Dashboard</a>
                 ${isAdmin ? `<a class="btn btn-outline-light" href="admin.html">Admin</a>` : ""}
                 <a class="btn btn-outline-light" href="settings.html">Settings</a>
                 <button type="button" class="btn btn-danger" id="nav-logout-btn">Log Out</button>
               </div>`
            : `<div class="d-flex gap-2">
                 <a class="btn btn-outline-light" href="login.html">Login</a>
                 <a class="btn btn-primary" href="register.html">Get Started</a>
               </div>`;

        const adminNavItem = isAdmin
            ? `<li class="nav-item"><a class="nav-link" data-page="admin" href="admin.html">Admin</a></li>`
            : "";

        navbarBox.innerHTML = `
        <nav class="navbar navbar-expand-lg nav-brand" data-bs-theme="light">
            <div class="container">
                <a class="navbar-brand logo" href="index.html" aria-label="DevPilot AI home">
                    <img src="images/logo.svg" alt="DevPilot AI" class="logo-img logo-svg">
                </a>
                <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarContent"
                    aria-controls="navbarContent" aria-expanded="false" aria-label="Toggle navigation">
                    <span class="navbar-toggler-icon"></span>
                </button>
                <div class="collapse navbar-collapse" id="navbarContent">
                    <ul class="navbar-nav mx-auto mb-2 mb-lg-0">
                        <li class="nav-item"><a class="nav-link" data-page="home" href="index.html">Home</a></li>
                        <li class="nav-item"><a class="nav-link" data-page="debug" href="submit-problem.html">Debug</a></li>
                        <li class="nav-item"><a class="nav-link" data-page="github" href="github.html">GitHub</a></li>
                        <li class="nav-item"><a class="nav-link" data-page="articles" href="articles.html">Articles</a></li>
                        <li class="nav-item"><a class="nav-link" data-page="challenges" href="challenges.html">Challenges</a></li>
                        <li class="nav-item"><a class="nav-link" data-page="plans" href="plans.html">Plans</a></li>
                        ${adminNavItem}
                        <li class="nav-item"><a class="nav-link" data-page="about" href="about.html">About</a></li>
                        <li class="nav-item"><a class="nav-link" data-page="community" href="community.html">Community</a></li>
                    </ul>
                    ${authButtonsHtml}
                </div>
            </div>
        </nav>`;

        const activeLink = navbarBox.querySelector('[data-page="' + navbarBox.dataset.active + '"]');
        if (activeLink) {
            activeLink.classList.add("active");
            activeLink.setAttribute("aria-current", "page");
        }

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

    const initialAdmin = isLoggedIn
        && typeof window.DevPilotApi !== "undefined"
        && typeof window.DevPilotApi.isAdmin === "function"
        && window.DevPilotApi.isAdmin();

    renderNavbar(initialAdmin);

    // After Admin role is assigned in DB, the current JWT may still be old.
    // Refresh roles from profile so the Admin tab appears without guessing claim names.
    if (isLoggedIn && window.DevPilotApi && typeof window.DevPilotApi.refreshRolesFromProfile === "function") {
        window.DevPilotApi.refreshRolesFromProfile().then(function () {
            if (window.DevPilotApi.isAdmin() && !initialAdmin) {
                renderNavbar(true);
            }
        }).catch(function () { /* keep current navbar */ });
    }
}
