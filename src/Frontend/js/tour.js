// DevPilot AI — guided page tours (Tour / Next / Skip / Close)
(function (window, document) {
    "use strict";

    if (window.DevPilotTour) return;

    function pageName() {
        const parts = (window.location.pathname || "").split("/");
        return (parts[parts.length - 1] || "index.html").toLowerCase() || "index.html";
    }

    /** @type {Record<string, Array<{selector:string,title:string,body:string}>>} */
    const PAGE_TOURS = {
        "index.html": [
            {
                selector: ".navbar-brand, .logo",
                title: "DevPilot AI",
                body: "Your home base. Click the logo anytime to return to the landing page."
            },
            {
                selector: ".navbar-nav",
                title: "Main navigation",
                body: "Jump between Debug, GitHub, Articles, Challenges, Plans, and more from here."
            },
            {
                selector: ".hero-title",
                title: "What DevPilot does",
                body: "Debug with AI, learn from real fixes, and turn every solution into reusable knowledge."
            },
            {
                selector: ".hero-section .btn-primary",
                title: "Get started",
                body: "Create an account to save progress, run AI debugs, and keep credits on your plan."
            },
            {
                selector: ".hero-section a[href='submit-problem.html']",
                title: "Debug a problem",
                body: "Paste code + the error message and let AI explain the root cause and fix."
            },
            {
                selector: ".hero-stats",
                title: "Live platform stats",
                body: "See how many developers, solved problems, articles, and challenges are on the platform."
            }
        ],
        "dashboard.html": [
            {
                selector: ".page-title",
                title: "Your workspace",
                body: "This is your developer dashboard — open issues, saved knowledge, and quick actions live here."
            },
            {
                selector: ".stat-card",
                title: "Progress at a glance",
                body: "Track open vs solved problems, your challenge rank, and challenge points."
            },
            {
                selector: ".quick-action",
                title: "Quick actions",
                body: "Start a debug, browse articles, open saved bookmarks, take a challenge, or connect GitHub."
            },
            {
                selector: "#recent-problems",
                title: "Recent problems",
                body: "Reopen tickets you already submitted. Use Continue to resume debugging or open the article when solved."
            },
            {
                selector: "#saved-articles",
                title: "Saved articles",
                body: "Articles you bookmarked appear here so you can jump back without searching again."
            },
            {
                selector: "#plan-credits, .surface-card .fa-coins",
                title: "Plan & credits",
                body: "See remaining AI credits and manage your plan when you need more capacity."
            }
        ],
        "submit-problem.html": [
            {
                selector: ".title-style, .page-title",
                title: "AI Debugging",
                body: "Submit a bug with only code and the error — DevPilot fills title and framework for you."
            },
            {
                selector: "#ide-editor, #sourceCode",
                title: "Code editor",
                body: "Paste the code involved in the bug. Keep it focused on the failing snippet when possible."
            },
            {
                selector: "#errorMessage",
                title: "Error message",
                body: "Paste the exact error text (exception, HTTP status, stack snippet). Better input = better analysis."
            },
            {
                selector: "#description",
                title: "Optional context",
                body: "Add expected behavior or when it started. Helpful, but not required."
            },
            {
                selector: "#submit-btn",
                title: "Analyze with AI",
                body: "Runs the analysis, uses credits from your plan, then shows root cause, steps, and corrected code."
            },
            {
                selector: "#result-panel, .debug-result",
                title: "Results area",
                body: "After analysis, the structured solution appears here. Mark solved to publish an article and challenge."
            }
        ],
        "articles.html": [
            {
                selector: ".page-title",
                title: "Knowledge base",
                body: "Browse published fixes from real developer problems — root cause, steps, and corrected code."
            },
            {
                selector: "#articles-view-toggle, #view-saved",
                title: "All vs Saved",
                body: "Switch to Saved to see only articles you bookmarked (requires login)."
            },
            {
                selector: "#search-form, #search-q",
                title: "Smart search",
                body: "Search by title, error name, technology, article code, or keyword."
            },
            {
                selector: "#tech-multiselect, #tech-toggle",
                title: "Technology filter",
                body: "Pick one or more frameworks/languages to narrow the list."
            },
            {
                selector: "#articles-grid",
                title: "Article cards",
                body: "Open any card to read the full article, then Like, Save, Share, or Comment."
            }
        ],
        "article-details.html": [
            {
                selector: ".page-title",
                title: "Article details",
                body: "A structured write-up of a real fix: summary, steps, and corrected code."
            },
            {
                selector: "#engage-bar, .article-engage-bar",
                title: "Engage",
                body: "Like, Save for later, Share the link, or jump to comments. Login is required for like/save/comment."
            },
            {
                selector: ".article-body, #article-root article",
                title: "Article body",
                body: "Read the explanation and use Copy on IDE-style code blocks to try the fix locally."
            },
            {
                selector: "#comments-section, .article-comments",
                title: "Comments",
                body: "Ask a follow-up or share what worked for you. You can delete your own comments."
            }
        ],
        "challenges.html": [
            {
                selector: ".page-title",
                title: "Practice arena",
                body: "Challenges are mini versions of real bugs so the fix sticks — and you earn points."
            },
            {
                selector: "#challenges-grid",
                title: "Available challenges",
                body: "Pick a challenge to open requirements, write a solution, and get AI-graded feedback."
            },
            {
                selector: "#leaderboard",
                title: "Leaderboard",
                body: "See top scorers and your rank as you solve more challenges."
            }
        ],
        "challenge-details.html": [
            {
                selector: "#challenge-root, .page-title, .page-shell .container",
                title: "Challenge workspace",
                body: "Read the requirements, submit your code, and review AI feedback with points awarded."
            }
        ],
        "github.html": [
            {
                selector: ".page-title",
                title: "GitHub workspace",
                body: "Review or debug files from your repos without copy/paste."
            },
            {
                selector: "#connection-card",
                title: "Connect GitHub",
                body: "Authorize once. You can disconnect anytime; saved reviews stay available."
            },
            {
                selector: "#reviews-history-card",
                title: "Saved code reviews",
                body: "Reopen past reviews even after disconnecting GitHub."
            },
            {
                selector: "#workspace, #repos-list",
                title: "Repos & files",
                body: "After connecting, pick a repository and file, then run Code Review or Debug."
            }
        ],
        "plans.html": [
            {
                selector: ".page-title, .page-shell .container > .page-title",
                title: "Plans",
                body: "Compare plans and credits for AI debug, articles, and GitHub code review."
            },
            {
                selector: ".plan-card, .plans-grid, #plans-root, .row.g-3, .surface-card",
                title: "Plan options",
                body: "Choose the plan that matches how often you debug. Pro unlocks GitHub Code Review."
            }
        ],
        "settings.html": [
            {
                selector: ".page-title, .page-shell h1",
                title: "Account settings",
                body: "Update your display name and manage account status from this page."
            },
            {
                selector: "form, #settings-form, .surface-card",
                title: "Profile form",
                body: "Save changes here. Sensitive actions like deactivation ask for confirmation."
            }
        ],
        "admin.html": [
            {
                selector: ".page-title, .page-shell h1, .admin-shell h1",
                title: "Admin console",
                body: "Manage users, plans, credit costs, AI usage, audit logs, and system errors."
            },
            {
                selector: ".nav-tabs, #admin-tabs, [role='tablist']",
                title: "Admin tabs",
                body: "Switch sections to inspect usage, errors, and configuration without leaving Admin."
            }
        ],
        "login.html": [
            {
                selector: "#email, input[type='email']",
                title: "Email",
                body: "Use the email you registered with."
            },
            {
                selector: "#password, input[type='password']",
                title: "Password",
                body: "Enter your password. Use Forgot password if you need a reset link."
            },
            {
                selector: "button[type='submit'], #login-btn",
                title: "Sign in",
                body: "After login you land on the dashboard (or the page you were sent from)."
            }
        ],
        "register.html": [
            {
                selector: "form, #register-form",
                title: "Create account",
                body: "Fill name, email, and password. You will verify email before full access."
            },
            {
                selector: "button[type='submit']",
                title: "Register",
                body: "Creates your account and starts email verification."
            }
        ],
        "about.html": [
            {
                selector: ".page-title, .hero-title, h1",
                title: "About DevPilot",
                body: "Learn the product story: debug smarter, publish knowledge, practice with challenges."
            }
        ],
        "community.html": [
            {
                selector: ".page-title, h1",
                title: "Community",
                body: "See how developers share fixes through articles, challenges, and discussion."
            }
        ],
        "problem-details.html": [
            {
                selector: "#problem-root, .page-shell .container",
                title: "Problem ticket",
                body: "Review the history of a submitted problem, continue debugging, or open the published article."
            }
        ]
    };

    const state = {
        active: false,
        index: 0,
        steps: [],
        resizeTimer: null
    };

    let ui = {
        btn: null,
        root: null,
        spotlight: null,
        popover: null,
        title: null,
        body: null,
        progress: null,
        btnNext: null,
        btnSkip: null,
        btnClose: null
    };

    function resolveSteps(defs) {
        const resolved = [];
        (defs || []).forEach(function (def) {
            const selectors = String(def.selector || "").split(",").map(function (s) { return s.trim(); }).filter(Boolean);
            let el = null;
            for (let i = 0; i < selectors.length; i++) {
                el = document.querySelector(selectors[i]);
                if (el && el.getClientRects().length) break;
                el = null;
            }
            if (!el) return;
            resolved.push({
                el: el,
                title: def.title || "Tip",
                body: def.body || ""
            });
        });

        // Merge any explicit data-tour nodes not already covered
        document.querySelectorAll("[data-tour]").forEach(function (el) {
            if (!el.getClientRects().length) return;
            const already = resolved.some(function (s) { return s.el === el; });
            if (already) return;
            const order = Number(el.getAttribute("data-tour-order") || resolved.length + 1);
            resolved.push({
                el: el,
                title: el.getAttribute("data-tour-title") || "Tip",
                body: el.getAttribute("data-tour-text") || el.getAttribute("data-tour") || "",
                order: order
            });
        });

        resolved.sort(function (a, b) {
            return (a.order || 0) - (b.order || 0);
        });
        return resolved;
    }

    function buildUi() {
        if (ui.root) return;

        ui.btn = document.createElement("button");
        ui.btn.type = "button";
        ui.btn.className = "tour-launch-btn";
        ui.btn.id = "tour-launch-btn";
        ui.btn.setAttribute("aria-label", "Start page tour");
        ui.btn.innerHTML = '<i class="fa-solid fa-route" aria-hidden="true"></i><span>Tour</span>';
        ui.btn.addEventListener("click", function () {
            if (state.active) stopTour();
            else startTour();
        });
        document.body.appendChild(ui.btn);

        ui.root = document.createElement("div");
        ui.root.className = "tour-root d-none";
        ui.root.setAttribute("aria-live", "polite");
        ui.root.innerHTML =
            '<div class="tour-backdrop" id="tour-backdrop"></div>' +
            '<div class="tour-spotlight" id="tour-spotlight"></div>' +
            '<div class="tour-popover" id="tour-popover" role="dialog" aria-modal="true" aria-labelledby="tour-title">' +
            '<button type="button" class="tour-close" id="tour-close" aria-label="Close tour"><i class="fa-solid fa-xmark"></i></button>' +
            '<div class="tour-progress" id="tour-progress"></div>' +
            '<h3 class="tour-title" id="tour-title"></h3>' +
            '<p class="tour-body" id="tour-body"></p>' +
            '<div class="tour-actions">' +
            '<button type="button" class="btn btn-outline-card btn-sm" id="tour-skip">Skip</button>' +
            '<button type="button" class="btn btn-primary btn-sm" id="tour-next">Next</button>' +
            "</div></div>";
        document.body.appendChild(ui.root);

        ui.spotlight = ui.root.querySelector("#tour-spotlight");
        ui.popover = ui.root.querySelector("#tour-popover");
        ui.title = ui.root.querySelector("#tour-title");
        ui.body = ui.root.querySelector("#tour-body");
        ui.progress = ui.root.querySelector("#tour-progress");
        ui.btnNext = ui.root.querySelector("#tour-next");
        ui.btnSkip = ui.root.querySelector("#tour-skip");
        ui.btnClose = ui.root.querySelector("#tour-close");

        ui.btnClose.addEventListener("click", stopTour);
        ui.btnSkip.addEventListener("click", function () { advance(true); });
        ui.btnNext.addEventListener("click", function () { advance(false); });
        ui.root.querySelector("#tour-backdrop").addEventListener("click", stopTour);

        document.addEventListener("keydown", function (e) {
            if (!state.active) return;
            if (e.key === "Escape") {
                e.preventDefault();
                stopTour();
            } else if (e.key === "ArrowRight" || e.key === "Enter") {
                e.preventDefault();
                advance(false);
            }
        });

        window.addEventListener("resize", function () {
            if (!state.active) return;
            clearTimeout(state.resizeTimer);
            state.resizeTimer = setTimeout(function () { showStep(state.index); }, 120);
        });

        window.addEventListener("scroll", function () {
            if (!state.active) return;
            clearTimeout(state.resizeTimer);
            state.resizeTimer = setTimeout(function () { positionStep(); }, 60);
        }, true);
    }

    function clearHighlight() {
        document.querySelectorAll(".tour-target-active").forEach(function (el) {
            el.classList.remove("tour-target-active");
        });
    }

    function stopTour() {
        state.active = false;
        state.index = 0;
        state.steps = [];
        clearHighlight();
        if (ui.root) ui.root.classList.add("d-none");
        document.body.classList.remove("tour-open");
        if (ui.btn) {
            ui.btn.classList.remove("is-active");
            ui.btn.setAttribute("aria-pressed", "false");
        }
    }

    function positionStep() {
        const step = state.steps[state.index];
        if (!step || !step.el) return;

        const pad = 8;
        const rect = step.el.getBoundingClientRect();
        const top = Math.max(8, rect.top - pad);
        const left = Math.max(8, rect.left - pad);
        const width = Math.min(window.innerWidth - left - 8, rect.width + pad * 2);
        const height = Math.min(window.innerHeight - top - 8, rect.height + pad * 2);

        ui.spotlight.style.top = top + "px";
        ui.spotlight.style.left = left + "px";
        ui.spotlight.style.width = Math.max(width, 40) + "px";
        ui.spotlight.style.height = Math.max(height, 40) + "px";

        const pop = ui.popover;
        pop.style.visibility = "hidden";
        pop.classList.remove("d-none");
        const popRect = pop.getBoundingClientRect();
        const spaceBelow = window.innerHeight - (top + height);
        const placeBelow = spaceBelow >= popRect.height + 16 || top < popRect.height + 24;

        let popTop = placeBelow ? (top + height + 12) : (top - popRect.height - 12);
        let popLeft = left;
        popTop = Math.min(Math.max(12, popTop), window.innerHeight - popRect.height - 12);
        popLeft = Math.min(Math.max(12, popLeft), window.innerWidth - popRect.width - 12);

        pop.style.top = popTop + "px";
        pop.style.left = popLeft + "px";
        pop.style.visibility = "visible";
    }

    function showStep(index) {
        if (!state.steps.length) {
            stopTour();
            return;
        }

        if (index < 0) index = 0;
        if (index >= state.steps.length) {
            stopTour();
            return;
        }

        state.index = index;
        const step = state.steps[index];
        clearHighlight();
        step.el.classList.add("tour-target-active");

        try {
            step.el.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" });
        } catch (e) {
            step.el.scrollIntoView(true);
        }

        ui.title.textContent = step.title;
        ui.body.textContent = step.body;
        ui.progress.textContent = (index + 1) + " / " + state.steps.length;
        ui.btnNext.textContent = index >= state.steps.length - 1 ? "Done" : "Next";
        ui.btnSkip.classList.toggle("d-none", index >= state.steps.length - 1);

        ui.root.classList.remove("d-none");
        document.body.classList.add("tour-open");

        // Allow scroll/layout to settle before measuring
        setTimeout(positionStep, 220);
    }

    function advance(isSkip) {
        void isSkip;
        showStep(state.index + 1);
    }

    function fallbackDefs() {
        return [
            {
                selector: ".navbar-nav, .nav-brand",
                title: "Navigation",
                body: "Use the top menu to move between Debug, Articles, Challenges, and Plans."
            },
            {
                selector: ".page-title, .title-style, h1",
                title: "This page",
                body: "This is the main content for the current screen. Explore the controls below to get work done."
            }
        ];
    }

    function beginWithSteps(steps) {
        if (!steps.length) {
            ui.btn.classList.add("is-empty");
            ui.btn.title = "No tour steps on this page yet";
            return;
        }

        state.steps = steps;
        state.active = true;
        ui.btn.classList.add("is-active");
        ui.btn.setAttribute("aria-pressed", "true");
        showStep(0);
    }

    function startTour() {
        buildUi();
        const name = pageName();
        const defs = PAGE_TOURS[name] || [];
        let steps = resolveSteps(defs);
        if (!steps.length) steps = resolveSteps(fallbackDefs());

        // Dynamic pages (article details, etc.) may still be rendering — retry once.
        if (steps.length < 2 && defs.length > 2) {
            setTimeout(function () {
                if (state.active) return;
                let retry = resolveSteps(defs);
                if (!retry.length) retry = resolveSteps(fallbackDefs());
                beginWithSteps(retry);
            }, 450);
            return;
        }

        beginWithSteps(steps);
    }

    function init() {
        buildUi();
        // Hide launch button on legal-only pages if desired — keep available everywhere
        ui.btn.classList.remove("d-none");
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }

    window.DevPilotTour = {
        start: startTour,
        stop: stopTour,
        isActive: function () { return state.active; }
    };
})(window, document);
