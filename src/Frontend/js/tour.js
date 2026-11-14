// DevPilot AI — guided page tours (Tour / Next / Skip / Close)
(function (window, document) {
    "use strict";

    if (window.DevPilotTour) return;

    function pageName() {
        const parts = (window.location.pathname || "").split("/");
        return (parts[parts.length - 1] || "index.html").toLowerCase() || "index.html";
    }

    /** Control-focused tours only (fields, buttons, filters) — no navbar/titles. */
    /** @type {Record<string, Array<{selector:string,title:string,body:string}>>} */
    const PAGE_TOURS = {
        "index.html": [
            {
                selector: ".hero-section a.btn-primary",
                title: "Get Started",
                body: "Creates your account so you can debug with AI, save articles, and keep credits."
            },
            {
                selector: ".hero-section a[href='submit-problem.html']",
                title: "Debug a Problem",
                body: "Opens the debugger: paste code + error and run AI analysis."
            },
            {
                selector: ".hero-section a[href='articles.html']",
                title: "Explore Articles",
                body: "Browse published fixes from real bugs — search, save, like, and comment."
            }
        ],
        "dashboard.html": [
            {
                selector: "a.quick-action[href='submit-problem.html']",
                title: "Debug with AI",
                body: "Opens the debugger to paste code and an error, then run AI analysis."
            },
            {
                selector: "a.quick-action[href='articles.html']",
                title: "Browse Knowledge",
                body: "Opens the articles list to search published fixes by keyword or technology."
            },
            {
                selector: "a.quick-action[href='#saved-articles']",
                title: "Saved Articles",
                body: "Jumps to your bookmarked articles on this dashboard."
            },
            {
                selector: "a.quick-action[href='challenges.html']",
                title: "Take a Challenge",
                body: "Opens practice challenges graded by AI so the fix sticks."
            },
            {
                selector: "a.quick-action[href='github.html']",
                title: "GitHub Review & Debug",
                body: "Connect a repo, pick a file, then run Code Review or Debug without copy/paste."
            },
            {
                selector: "a.quick-action[href='settings.html']",
                title: "Account settings",
                body: "Change your display name or password, or deactivate the account."
            },
            {
                selector: "#recent-problems",
                title: "Recent Problems list",
                body: "Each row opens the ticket. Use Continue to resume debugging, or Article when it is solved."
            },
            {
                selector: "#saved-articles-list",
                title: "Saved Articles list",
                body: "Articles you tapped Save on. Click a title to reopen it."
            },
            {
                selector: "a[href='plans.html']",
                title: "Manage plan",
                body: "Opens Plans to see credits left and upgrade (Pro unlocks GitHub Code Review)."
            }
        ],
        "submit-problem.html": [
            {
                selector: "#sourceCode",
                title: "Your code",
                body: "Required. Paste the code involved in the bug — keep it focused on the failing snippet."
            },
            {
                selector: "#errorMessage",
                title: "Error message",
                body: "Required. Paste the exact error text (exception, HTTP status, stack snippet)."
            },
            {
                selector: "#description",
                title: "Description (optional)",
                body: "Extra context only — expected behavior, when it started, etc. Not required."
            },
            {
                selector: "#submit-btn",
                title: "Analyze with AI",
                body: "Sends the bug to AI, spends credits from your plan, then shows root cause, steps, and corrected code."
            },
            {
                selector: "#feedback-actions",
                title: "Solved / Still stuck",
                body: "After analysis: Problem Solved publishes knowledge + challenge; Still stuck keeps the ticket open for follow-up."
            }
        ],
        "articles.html": [
            {
                selector: "#view-all",
                title: "All articles",
                body: "Shows the full public knowledge base."
            },
            {
                selector: "#view-saved",
                title: "Saved",
                body: "Shows only articles you bookmarked. Requires login."
            },
            {
                selector: "#search-q",
                title: "Search box",
                body: "Type a title, error name, technology, article code, or keyword."
            },
            {
                selector: "#tech-toggle",
                title: "Technology filter",
                body: "Open this to multi-select frameworks/languages, then search."
            },
            {
                selector: "#search-form button[type='submit']",
                title: "Search button",
                body: "Runs the search with your keyword and selected technologies."
            },
            {
                selector: "#articles-grid .article-tile, #articles-grid a",
                title: "Article card",
                body: "Opens the full article where you can Like, Save, Share, and Comment."
            }
        ],
        "article-details.html": [
            {
                selector: "#btn-like",
                title: "Like",
                body: "Toggles your like on this article. Requires login."
            },
            {
                selector: "#btn-save",
                title: "Save",
                body: "Bookmarks the article to your Saved list (dashboard + Articles → Saved). Requires login."
            },
            {
                selector: "#btn-share",
                title: "Share",
                body: "Copies the article link (or opens the device share sheet)."
            },
            {
                selector: ".ide-copy-btn",
                title: "Copy code",
                body: "Copies the corrected code block so you can paste it into your editor."
            },
            {
                selector: "#comment-body",
                title: "Comment box",
                body: "Write a tip or question about this fix. Requires login."
            },
            {
                selector: "#comment-form button[type='submit']",
                title: "Post comment",
                body: "Publishes your comment under the article."
            },
            {
                selector: ".col-lg-4 .btn-primary",
                title: "Solve this challenge",
                body: "Opens the practice challenge built from this fix (if one exists)."
            }
        ],
        "challenges.html": [
            {
                selector: "#challenges-grid a, #challenges-grid .challenge-card, #challenges-grid button",
                title: "Challenge item",
                body: "Opens that challenge so you can read requirements and submit a solution."
            },
            {
                selector: "#leaderboard",
                title: "Leaderboard",
                body: "Shows top scorers. Points come from AI-graded challenge submissions."
            }
        ],
        "challenge-details.html": [
            {
                selector: "#solution-code",
                title: "Solution editor",
                body: "Write (or paste) your fix here before submitting."
            },
            {
                selector: "#btn-submit",
                title: "Submit solution",
                body: "Sends your code for AI grading and awards points if you pass."
            },
            {
                selector: "#result-card",
                title: "AI Feedback",
                body: "After submit, shows pass/fail, advice, and points awarded."
            }
        ],
        "github.html": [
            {
                selector: "#btn-connect",
                title: "Connect GitHub",
                body: "Authorizes DevPilot to list your repos and open files for Review/Debug."
            },
            {
                selector: "#btn-disconnect",
                title: "Disconnect",
                body: "Removes the GitHub link. Saved code reviews stay available."
            },
            {
                selector: "#btn-refresh-reviews",
                title: "Refresh reviews",
                body: "Reloads your saved Code Review history."
            },
            {
                selector: "#reviews-history-list",
                title: "Saved code reviews",
                body: "Click a past review to reopen it anytime."
            },
            {
                selector: "#repos-list",
                title: "Repositories",
                body: "Pick a repo after connecting. Then choose a file on the right."
            },
            {
                selector: "#files-list",
                title: "Files",
                body: "Select a file to load into Debug or Code Review."
            },
            {
                selector: "#tab-debug",
                title: "Debug tab",
                body: "Debug this file with an error message — same flow as Submit Problem."
            },
            {
                selector: "#tab-review",
                title: "Code Review tab",
                body: "Runs an AI code review on the selected file (Pro plan)."
            },
            {
                selector: "#error-message",
                title: "Error message (Debug)",
                body: "Paste the error related to the selected file, then run Debug."
            },
            {
                selector: "#debug-notes",
                title: "Notes (optional)",
                body: "Optional context for the GitHub debug run."
            },
            {
                selector: "#btn-debug",
                title: "Run Debug",
                body: "Analyzes the selected file + error with AI."
            },
            {
                selector: "#btn-review",
                title: "Run Code Review",
                body: "Reviews the selected file and saves the result in your history."
            }
        ],
        "plans.html": [
            {
                selector: "#my-usage-card",
                title: "Your usage",
                body: "Shows your current plan and how many credits you have left this month."
            },
            {
                selector: "#plans-grid .btn, #plans-grid button, #plans-grid a.btn",
                title: "Choose / upgrade plan",
                body: "Select a plan. Pro includes GitHub Code Review; credits power AI debug actions."
            },
            {
                selector: "#credit-costs",
                title: "Credit costs",
                body: "Exact credit deduction for each AI action (debug, follow-up, review, etc.)."
            }
        ],
        "settings.html": [
            {
                selector: "#profile-name",
                title: "Display name",
                body: "Shown on the dashboard greeting and challenge leaderboard."
            },
            {
                selector: "#profile-email-display",
                title: "Email",
                body: "Read-only login email for this account."
            },
            {
                selector: "#btn-save-profile",
                title: "Save name",
                body: "Saves your new display name."
            },
            {
                selector: "#current-password",
                title: "Current password",
                body: "Required to prove it is you before changing the password."
            },
            {
                selector: "#new-password",
                title: "New password",
                body: "At least 8 characters, with upper, lower, and a number."
            },
            {
                selector: "#confirm-password",
                title: "Confirm new password",
                body: "Must match the new password exactly."
            },
            {
                selector: "#btn-change-password",
                title: "Update password",
                body: "Applies the new password to your account."
            },
            {
                selector: "#btn-delete-account",
                title: "Deactivate account",
                body: "Soft-deletes the account and signs you out. Use carefully."
            }
        ],
        "admin.html": [
            {
                selector: "#admin-tabs button[data-tab='overview']",
                title: "Overview tab",
                body: "Platform stats snapshot for admins."
            },
            {
                selector: "#admin-tabs button[data-tab='users']",
                title: "Users tab",
                body: "Search users, add users, assign roles/plans, and manage accounts."
            },
            {
                selector: "#admin-tabs button[data-tab='plans']",
                title: "Plans tab",
                body: "Create and edit subscription plans and credit allocations."
            },
            {
                selector: "#admin-tabs button[data-tab='credits']",
                title: "Credit costs tab",
                body: "Set how many credits each AI action deducts."
            },
            {
                selector: "#admin-tabs button[data-tab='articles']",
                title: "Articles tab",
                body: "Review and manage published knowledge articles."
            },
            {
                selector: "#admin-tabs button[data-tab='ai']",
                title: "AI usage tab",
                body: "Inspect provider/model calls, tokens, and failures. Use View for full JSON."
            },
            {
                selector: "#admin-tabs button[data-tab='audit']",
                title: "Audit log tab",
                body: "Security and admin action history with detail JSON."
            },
            {
                selector: "#admin-tabs button[data-tab='errors']",
                title: "System errors tab",
                body: "Non-AI failures with path, status, and context JSON."
            },
            {
                selector: "#users-search",
                title: "Users search",
                body: "Filter the users table by name, email, or id (on the Users tab)."
            },
            {
                selector: "#users-add",
                title: "Add user",
                body: "Creates a user from Admin without going through public register."
            }
        ],
        "login.html": [
            {
                selector: "#email",
                title: "Email Address",
                body: "The email you registered with."
            },
            {
                selector: "#password",
                title: "Password",
                body: "Your account password."
            },
            {
                selector: "#toggle-password",
                title: "Show / hide password",
                body: "Toggles password visibility in this field."
            },
            {
                selector: "#remember",
                title: "Keep me signed in",
                body: "Keeps the session so you stay logged in on this device."
            },
            {
                selector: "a[href='forgot-password.html']",
                title: "Forgot password",
                body: "Opens the reset flow to email a recovery link."
            },
            {
                selector: "#turnstile-group",
                title: "Security check",
                body: "Complete the bot check before logging in."
            },
            {
                selector: "#login-btn",
                title: "Log In",
                body: "Signs you in and sends you to the dashboard (or the page you came from)."
            },
            {
                selector: "a[href='register.html']",
                title: "Create account",
                body: "Opens registration if you do not have an account yet."
            }
        ],
        "register.html": [
            {
                selector: "#name",
                title: "Full name",
                body: "Your display name on the dashboard and leaderboard."
            },
            {
                selector: "#email",
                title: "Email Address",
                body: "Used to log in and verify the account."
            },
            {
                selector: "#password",
                title: "Password",
                body: "Must meet the rules listed under the field (length + character mix)."
            },
            {
                selector: "#confirm",
                title: "Confirm password",
                body: "Re-enter the same password to avoid typos."
            },
            {
                selector: "#turnstile-group",
                title: "Security check",
                body: "Complete the bot check before creating the account."
            },
            {
                selector: "#register-btn",
                title: "Create account",
                body: "Registers you and starts email verification."
            }
        ],
        "problem-details.html": [
            {
                selector: "a.btn-primary[href*='submit-problem']",
                title: "Continue debugging",
                body: "Reopens this ticket in the debugger with prior context."
            },
            {
                selector: "a.btn-primary[href*='article-details']",
                title: "Open article",
                body: "Opens the published knowledge article for a solved ticket."
            },
            {
                selector: "a.btn[href='challenges.html']",
                title: "Practice challenges",
                body: "Go practice related fixes in the challenge arena."
            }
        ],
        "forgot-password.html": [
            {
                selector: "#email, input[type='email']",
                title: "Email",
                body: "Enter the account email to receive a reset link."
            },
            {
                selector: "button[type='submit']",
                title: "Send reset link",
                body: "Emails a password reset link if the account exists."
            }
        ],
        "reset-password.html": [
            {
                selector: "#password, input[type='password']",
                title: "New password",
                body: "Choose a strong new password for the account."
            },
            {
                selector: "button[type='submit']",
                title: "Update password",
                body: "Saves the new password using the reset token from your email."
            }
        ],
        "verify-email.html": [
            {
                selector: "#code, input[type='text'], input[name='code']",
                title: "Verification code",
                body: "Enter the code from your email to activate the account."
            },
            {
                selector: "button[type='submit']",
                title: "Verify",
                body: "Confirms the email and unlocks full access."
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

    function isTourVisible(el) {
        if (!el || !el.getClientRects || !el.getClientRects().length) return false;
        if (el.closest && el.closest(".d-none, [hidden], .tour-root, .tour-launch-btn")) return false;
        const style = window.getComputedStyle(el);
        if (style.display === "none" || style.visibility === "hidden" || style.opacity === "0") return false;
        return true;
    }

    function resolveSteps(defs) {
        const resolved = [];
        (defs || []).forEach(function (def) {
            const selectors = String(def.selector || "").split(",").map(function (s) { return s.trim(); }).filter(Boolean);
            let el = null;
            for (let i = 0; i < selectors.length; i++) {
                const found = document.querySelector(selectors[i]);
                if (found && (def.includeHidden || isTourVisible(found))) {
                    el = found;
                    break;
                }
            }
            if (!el) return;
            // Prefer a visible ancestor if the target itself is hidden but includeHidden was set
            if (!isTourVisible(el) && def.includeHidden) {
                const host = el.closest(".surface-card, .mb-3, .form-group, .d-flex") || el.parentElement;
                if (host && isTourVisible(host)) el = host;
                else return;
            }
            resolved.push({
                el: el,
                title: def.title || "Control",
                body: def.body || ""
            });
        });

        document.querySelectorAll("[data-tour]").forEach(function (el) {
            if (!isTourVisible(el)) return;
            const already = resolved.some(function (s) { return s.el === el; });
            if (already) return;
            const order = Number(el.getAttribute("data-tour-order") || resolved.length + 1);
            resolved.push({
                el: el,
                title: el.getAttribute("data-tour-title") || "Control",
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

    function beginWithSteps(steps) {
        if (!steps.length) {
            ui.btn.classList.add("is-empty");
            ui.btn.title = "No interactive controls to tour on this page";
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

        // Dynamic pages (article / challenge details) may still be rendering — retry once.
        if (defs.length && steps.length < Math.min(2, defs.length)) {
            setTimeout(function () {
                if (state.active) return;
                beginWithSteps(resolveSteps(defs));
            }, 500);
            return;
        }

        beginWithSteps(steps);
    }

    function init() {
        buildUi();
        const name = pageName();
        const hasTour = Array.isArray(PAGE_TOURS[name]) && PAGE_TOURS[name].length > 0;
        ui.btn.classList.toggle("d-none", !hasTour);
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
