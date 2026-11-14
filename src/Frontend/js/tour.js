// DevPilot AI — guided page tours (Tour / Next / Skip / Close)
// Only on Debug + GitHub, with detailed control explanations.
(function (window, document) {
    "use strict";

    if (window.DevPilotTour) return;

    function pageName() {
        const parts = (window.location.pathname || "").split("/");
        return (parts[parts.length - 1] || "index.html").toLowerCase() || "index.html";
    }

    /**
     * Tours ONLY on Debug and GitHub — every important field/button explained in detail.
     */
    /** @type {Record<string, Array<{selector:string,title:string,body:string,includeHidden?:boolean}>>} */
    const PAGE_TOURS = {
        "submit-problem.html": [
            {
                selector: "#ide-editor",
                title: "Your code editor",
                body: "Required field. Paste only the code involved in the bug (not the whole project). Line numbers help you match stack-trace locations. Max length is about 50,000 characters."
            },
            {
                selector: "#code-meta",
                title: "Cursor position (Ln / Col)",
                body: "Shows the current line and column while you type or paste. Useful when the error mentions a specific line number."
            },
            {
                selector: "#errorMessage",
                title: "Error message",
                body: "Required. Paste the exact error from your console or logs (for example NullReferenceException or 401 Unauthorized). More precise text produces better analysis. Length: 2–4000 characters."
            },
            {
                selector: "#description",
                title: "Description (optional)",
                body: "Not required. Add context if you want: what you were doing, when it started, and expected behavior. Code + error still drive the analysis."
            },
            {
                selector: "#submit-btn",
                title: "Analyze with AI",
                body: "Sends the problem for analysis. Uses credits from your plan, creates a ticket, and returns a structured solution: problem, location, root cause, summary, steps, and corrected code. Watch the live log on the right while it runs."
            },
            {
                selector: "#analysis-rail",
                title: "Analysis log",
                body: "Right-side panel with live steps (validate input, create ticket, run AI). Idle means nothing is running right now."
            },
            {
                selector: "#analysis-status",
                title: "Analysis status",
                body: "Moves through Idle → Running → Done/Error so you know whether analysis is still in progress."
            },
            {
                selector: "#result-panel",
                title: "Structured Solution",
                body: "Appears after analysis finishes and shows the full answer in a fixed order. On follow-up, the previous solution is hidden until the new run completes."
            },
            {
                selector: "#result-problem",
                title: "1. Problem",
                body: "A clear summary of the issue as the AI understood it from your code and error — not a raw copy of your description."
            },
            {
                selector: "#result-location",
                title: "2. Location",
                body: "Where the issue likely sits in the code (file / function / line when possible). Use it with the editor line numbers."
            },
            {
                selector: "#result-rootCause",
                title: "3. Root Cause",
                body: "Why the failure happens underneath — not just the surface error text."
            },
            {
                selector: "#result-summary",
                title: "4. Solution Summary",
                body: "A short overview of the fix before the detailed steps. Read this first for the overall direction."
            },
            {
                selector: "#result-steps",
                title: "5. Steps",
                body: "Ordered actions you apply yourself to fix the bug. Follow them in sequence."
            },
            {
                selector: "#corrected-code-host",
                title: "6. Corrected Code",
                body: "The fixed code in an IDE-style block. Use Copy when available to paste it into your local editor and test."
            },
            {
                selector: "#btn-solved",
                title: "Problem solved",
                body: "Click when the fix works for you. Marks the ticket Solved and can publish a Knowledge Article plus a practice Challenge from the same fix."
            },
            {
                selector: "#btn-unsolved",
                title: "Still have a problem",
                body: "If you are still stuck, starts a follow-up on the same ticket. Update code/error/description and re-analyze without starting over."
            }
        ],
        "github.html": [
            {
                selector: "#connection-card",
                title: "Connection",
                body: "First step on this page. Shows whether GitHub is linked. Without a connection you cannot browse repos or run Debug/Review on your files."
            },
            {
                selector: "#connection-text",
                title: "Connection status",
                body: "Status text: checking, connected as a username, or disconnected. Watch it after Connect or Disconnect."
            },
            {
                selector: "#btn-connect",
                title: "Connect GitHub",
                body: "Starts GitHub OAuth. After you approve, DevPilot can list repositories and files for Debug and Code Review without manual copy/paste. You must be logged into DevPilot."
            },
            {
                selector: "#btn-disconnect",
                title: "Disconnect",
                body: "Unlinks GitHub and clears the stored token. Saved code reviews remain available and can still be reopened."
            },
            {
                selector: "#reviews-history-card",
                title: "Saved code reviews",
                body: "Archive of past code reviews. They stay saved even after Disconnect. Click any review to reopen the full result."
            },
            {
                selector: "#btn-refresh-reviews",
                title: "Refresh",
                body: "Reloads saved reviews from the server (useful if you ran a new review in another tab)."
            },
            {
                selector: "#reviews-history-list",
                title: "Reviews list",
                body: "Each row is a past review (file / date / summary). Open one for details. If empty, run Code Review at least once."
            },
            {
                selector: "#reviews-pager",
                title: "Reviews pager",
                body: "When you have many reviews, use these controls to move between pages (Previous / Next)."
            },
            {
                selector: "#workspace",
                title: "Workspace (after connect)",
                body: "Shown after Connect. Flow: pick a repo → pick a file → use Debug or Code Review → read the result."
            },
            {
                selector: "#repos-list",
                title: "Repositories",
                body: "Your GitHub repositories. Click one to load its files/branches. If empty, re-check permissions after Connect."
            },
            {
                selector: "#branch-label",
                title: "Current branch",
                body: "Branch you are browsing (for example main). The file list below belongs to this branch."
            },
            {
                selector: "#files-list",
                title: "Files",
                body: "After choosing a repo, select the source file to debug or review. Without a file, Debug stays locked; Code Review may auto-pick files depending on your plan."
            },
            {
                selector: "#selected-file-title",
                title: "Selected file",
                body: "Title of the file currently selected. Confirm it before Analyze or Run code review."
            },
            {
                selector: "#tab-debug",
                title: "Debug tab",
                body: "Same idea as the Debug page, but source code is loaded from the GitHub file. You provide the error message (and optional notes); AI analyzes that file."
            },
            {
                selector: "#tab-review",
                title: "Code Review tab",
                body: "Quality review of the code (not only a bug fix). Pro feature; costs credits (about 15). Output is saved under Saved code reviews."
            },
            {
                selector: "#panel-debug",
                title: "Debug panel",
                body: "Enter analysis details for the selected file. If no file is selected you see “Select a file” — pick one from the list on the left first."
            },
            {
                selector: "#debug-file-gate",
                title: "File gate",
                body: "Shown when no file is selected. You cannot enter the error or run analysis until you pick a file from Files."
            },
            {
                selector: "#selected-file-path",
                title: "File path",
                body: "Full path inside the repo for the selected file (for example src/Auth/Login.cs). Confirm it before running."
            },
            {
                selector: "#error-message",
                title: "Error message (GitHub Debug)",
                body: "Paste the exact error for this file. Enabled after a file is selected. This is the key field for GitHub Debug."
            },
            {
                selector: "#debug-notes",
                title: "Notes (optional)",
                body: "Extra context: what were you trying to do? Optional, but helpful when the error is vague."
            },
            {
                selector: "#btn-debug",
                title: "Analyze file with AI",
                body: "Runs Debug on the GitHub file contents plus your error message. Uses credits like normal Debug. Results appear below with Solved / Still have a problem."
            },
            {
                selector: "#panel-review",
                title: "Code Review panel",
                body: "Open the Code Review tab to reach this panel. If your plan is not Pro you will see an upgrade notice. Run code review starts the review."
            },
            {
                selector: "#review-plan-gate",
                title: "Pro plan notice",
                body: "Appears when Code Review is not available on your current plan. Use the link to open Plans and upgrade to Pro."
            },
            {
                selector: "#btn-review",
                title: "Run code review",
                body: "Runs an AI review on the selected file (or auto-selected files). The result is saved under Saved code reviews and remains available after Disconnect."
            },
            {
                selector: "#result-card",
                title: "Result",
                body: "After Debug or Review, output appears here: summary, findings, or structured solution. Check the heading and badge for the result type."
            },
            {
                selector: "#result-heading",
                title: "Result heading",
                body: "Labels the current output type (debug solution or code review)."
            },
            {
                selector: "#result-body",
                title: "Result body",
                body: "AI details: structured solution sections for Debug, or findings/notes for Code Review."
            },
            {
                selector: "#btn-solved",
                title: "Problem solved",
                body: "On GitHub Debug: confirm the fix worked. Like regular Debug, this can publish a knowledge article and a practice challenge."
            },
            {
                selector: "#btn-unsolved",
                title: "Still have a problem",
                body: "If GitHub Debug did not resolve it, continue a follow-up on the same ticket after updating the error/notes and re-running analysis."
            }
        ]
    };

    const REVEAL_FOR_TOUR = {
        "submit-problem.html": ["#result-panel", "#notes-block", "#feedback-actions"],
        "github.html": [
            "#workspace",
            "#btn-disconnect",
            "#debug-fields",
            "#debug-file-gate",
            "#panel-review",
            "#review-plan-gate",
            "#result-card",
            "#feedback-actions"
        ]
    };

    const state = {
        active: false,
        index: 0,
        steps: [],
        resizeTimer: null,
        revealed: []
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
        if (el.closest && el.closest(".tour-root, .tour-launch-btn")) return false;
        if (el.closest && el.closest(".d-none, [hidden]") && !el.closest("[data-tour-revealed='1']")) return false;
        const style = window.getComputedStyle(el);
        if (style.display === "none" || style.visibility === "hidden") return false;
        return true;
    }

    function revealForTour(name) {
        state.revealed = [];
        const list = REVEAL_FOR_TOUR[name] || [];
        list.forEach(function (sel) {
            const el = document.querySelector(sel);
            if (!el) return;
            if (el.classList.contains("d-none")) {
                el.classList.remove("d-none");
                el.setAttribute("data-tour-revealed", "1");
                state.revealed.push(el);
            }
            // Enable disabled fields so the spotlight can focus meaningfully
            if (el.matches && (el.matches("textarea, input, button") || el.querySelector)) {
                el.querySelectorAll("textarea[disabled], input[disabled], button[disabled]").forEach(function (ctrl) {
                    if (!ctrl.hasAttribute("data-tour-was-disabled")) {
                        ctrl.setAttribute("data-tour-was-disabled", "1");
                        ctrl.disabled = false;
                    }
                });
                if ((el.tagName === "TEXTAREA" || el.tagName === "INPUT" || el.tagName === "BUTTON") && el.disabled) {
                    el.setAttribute("data-tour-was-disabled", "1");
                    el.disabled = false;
                }
            }
        });

        // On GitHub: show both panels for the tour so each control can be visited
        if (name === "github.html") {
            ["#panel-debug", "#panel-review"].forEach(function (sel) {
                const panel = document.querySelector(sel);
                if (panel && panel.classList.contains("d-none")) {
                    panel.classList.remove("d-none");
                    panel.setAttribute("data-tour-revealed", "1");
                    state.revealed.push(panel);
                }
            });
        }
    }

    function restoreRevealed() {
        state.revealed.forEach(function (el) {
            if (!el) return;
            el.classList.add("d-none");
            el.removeAttribute("data-tour-revealed");
        });
        state.revealed = [];
        document.querySelectorAll("[data-tour-was-disabled]").forEach(function (ctrl) {
            ctrl.disabled = true;
            ctrl.removeAttribute("data-tour-was-disabled");
        });
    }

    function resolveSteps(defs) {
        const resolved = [];
        (defs || []).forEach(function (def) {
            const selectors = String(def.selector || "").split(",").map(function (s) { return s.trim(); }).filter(Boolean);
            let el = null;
            for (let i = 0; i < selectors.length; i++) {
                const found = document.querySelector(selectors[i]);
                if (!found) continue;
                if (isTourVisible(found) || def.includeHidden || found.getAttribute("data-tour-revealed") === "1") {
                    el = found;
                    break;
                }
            }
            if (!el) return;
            if (!isTourVisible(el) && el.getAttribute("data-tour-revealed") !== "1" && !def.includeHidden) return;
            resolved.push({
                el: el,
                title: def.title || "Control",
                body: def.body || ""
            });
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
        ui.btnSkip.addEventListener("click", function () { advance(); });
        ui.btnNext.addEventListener("click", function () { advance(); });
        ui.root.querySelector("#tour-backdrop").addEventListener("click", stopTour);

        document.addEventListener("keydown", function (e) {
            if (!state.active) return;
            if (e.key === "Escape") {
                e.preventDefault();
                stopTour();
            } else if (e.key === "ArrowRight" || e.key === "Enter") {
                e.preventDefault();
                advance();
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
            state.resizeTimer = setTimeout(positionStep, 60);
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
        restoreRevealed();
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
        setTimeout(positionStep, 220);
    }

    function advance() {
        showStep(state.index + 1);
    }

    function beginWithSteps(steps) {
        if (!steps.length) {
            restoreRevealed();
            ui.btn.classList.add("is-empty");
            ui.btn.title = "No tour steps available";
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
        if (!defs.length) return;

        revealForTour(name);
        // Let layout settle after revealing hidden panels
        setTimeout(function () {
            beginWithSteps(resolveSteps(defs));
        }, 80);
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
