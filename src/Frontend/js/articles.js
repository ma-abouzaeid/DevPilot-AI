(function () {
    "use strict";

    const grid = document.getElementById("articles-grid");
    const form = document.getElementById("search-form");
    const techToggle = document.getElementById("tech-toggle");
    const techToggleLabel = document.getElementById("tech-toggle-label");
    const techMenu = document.getElementById("tech-menu");
    const selectedTechs = new Set();

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function updateTechLabel() {
        if (!selectedTechs.size) {
            techToggleLabel.textContent = "All technologies";
            return;
        }
        const values = Array.from(selectedTechs);
        if (values.length <= 2) {
            techToggleLabel.textContent = values.join(", ");
            return;
        }
        techToggleLabel.textContent = values.length + " technologies";
    }

    function renderTechOptions(items) {
        if (!items.length) {
            techMenu.innerHTML = '<div class="tech-multiselect-empty">No technologies yet</div>';
            return;
        }

        techMenu.innerHTML = items.map(function (tech, index) {
            const id = "tech-opt-" + index;
            return (
                '<label class="tech-multiselect-option" for="' + id + '">' +
                '<input type="checkbox" id="' + id + '" value="' + escapeHtml(tech) + '">' +
                "<span>" + escapeHtml(tech) + "</span>" +
                "</label>"
            );
        }).join("") +
            '<div class="tech-multiselect-actions">' +
            '<button type="button" class="btn btn-outline-card btn-sm" id="tech-clear" style="width:auto;">Clear</button>' +
            "</div>";

        techMenu.querySelectorAll('input[type="checkbox"]').forEach(function (input) {
            input.addEventListener("change", function () {
                if (input.checked) selectedTechs.add(input.value);
                else selectedTechs.delete(input.value);
                updateTechLabel();
            });
        });

        const clearBtn = document.getElementById("tech-clear");
        if (clearBtn) {
            clearBtn.addEventListener("click", function () {
                selectedTechs.clear();
                techMenu.querySelectorAll('input[type="checkbox"]').forEach(function (input) {
                    input.checked = false;
                });
                updateTechLabel();
            });
        }
    }

    async function loadTechnologies() {
        try {
            const url = new URL("/api/Problems/article-technologies", API_BASE).href;
            const res = await window.DevPilotApi.request(url, { skipAuth: true });
            const items = Array.isArray(res.data) ? res.data : [];
            renderTechOptions(items);
        } catch (e) {
            techMenu.innerHTML = '<div class="tech-multiselect-empty">Could not load technologies</div>';
        }
    }

    function setTechMenuOpen(open) {
        techMenu.classList.toggle("d-none", !open);
        techToggle.setAttribute("aria-expanded", open ? "true" : "false");
        techToggle.classList.toggle("is-open", open);
    }

    async function loadArticles(q, technologies) {
        grid.innerHTML = '<div class="col-12"><div class="empty-state">Searching knowledge base…</div></div>';
        try {
            const url = new URL("/api/Problems/articles", API_BASE);
            url.searchParams.set("page", "1");
            url.searchParams.set("pageSize", "12");
            if (q) url.searchParams.set("q", q);
            (technologies || []).forEach(function (tech) {
                url.searchParams.append("technologies", tech);
            });

            const res = await window.DevPilotApi.request(url.href, { skipAuth: true });
            const items = (res.data && (res.data.items || res.data.Items)) || [];

            if (!items.length) {
                grid.innerHTML =
                    '<div class="col-12"><div class="empty-state surface-card">' +
                    "<p>No articles matched your search. Try another keyword, error name, or technology.</p>" +
                    '<a class="btn btn-primary mt-2" href="submit-problem.html">Submit a Problem</a>' +
                    "</div></div>";
                return;
            }

            grid.innerHTML = items.map(function (a) {
                const articleId = a.knowledgeArticleId || a.KnowledgeArticleId;
                const code = a.articleCode || a.ArticleCode || a.problemCode || a.ProblemCode || "";
                const title = a.title || a.Title || "Untitled";
                const summary = a.summary || a.Summary || "Structured debugging knowledge from a real developer issue.";
                const framework = a.framework || a.Framework || "";
                return (
                    '<div class="col-md-6 col-lg-4">' +
                    '<a class="article-tile" href="article-details.html?id=' + encodeURIComponent(articleId) + '">' +
                    '<div class="d-flex justify-content-between align-items-center">' +
                    '<span class="badge-soft ai">' + escapeHtml(code) + "</span>" +
                    '<span class="badge-soft solved">Published</span>' +
                    "</div>" +
                    "<h3>" + escapeHtml(title) + "</h3>" +
                    '<p class="text-gray mb-2">' + escapeHtml(summary) + "</p>" +
                    (framework ? '<div class="text-gray small">' + escapeHtml(framework) + "</div>" : "") +
                    "</a></div>"
                );
            }).join("");
        } catch (e) {
            grid.innerHTML = '<div class="col-12"><div class="empty-state">Could not load articles. Check API connection.</div></div>';
        }
    }

    techToggle.addEventListener("click", function (e) {
        e.preventDefault();
        setTechMenuOpen(techMenu.classList.contains("d-none"));
    });

    document.addEventListener("click", function (e) {
        if (!e.target.closest("#tech-multiselect")) setTechMenuOpen(false);
    });

    form.addEventListener("submit", function (e) {
        e.preventDefault();
        setTechMenuOpen(false);
        loadArticles(
            document.getElementById("search-q").value.trim(),
            Array.from(selectedTechs)
        );
    });

    loadTechnologies();
    loadArticles("", []);
})();
