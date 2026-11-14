(function () {
    "use strict";

    const root = document.getElementById("article-root");
    const params = new URLSearchParams(window.location.search);
    const id = params.get("id");
    const problemId = params.get("problemId");
    const code = params.get("code");

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function formatDate(value) {
        if (!value) return "";
        try { return new Date(value).toLocaleString(); } catch { return String(value); }
    }

    function formatSourceCode(raw) {
        if (!raw) return "";
        let text = String(raw).replace(/\r\n/g, "\n").trim();
        const newlineCount = (text.match(/\n/g) || []).length;
        if (newlineCount >= 3) return text;

        let out = "";
        let indent = 0;
        let inString = false;
        let stringQuote = "";
        let escaped = false;

        function newline() {
            out += "\n" + "    ".repeat(Math.max(indent, 0));
        }

        for (let i = 0; i < text.length; i++) {
            const ch = text[i];
            if (inString) {
                out += ch;
                if (escaped) escaped = false;
                else if (ch === "\\") escaped = true;
                else if (ch === stringQuote) inString = false;
                continue;
            }
            if (ch === "\"" || ch === "'") {
                inString = true;
                stringQuote = ch;
                out += ch;
                continue;
            }
            if (ch === "{") {
                out = out.replace(/[ \t]+$/, "");
                out += " {";
                indent += 1;
                newline();
                continue;
            }
            if (ch === "}") {
                indent = Math.max(indent - 1, 0);
                out = out.replace(/[ \t]+$/, "");
                newline();
                out += "}";
                const next = text[i + 1];
                if (next && next !== ";" && next !== ")" && next !== "," && next !== "}") newline();
                continue;
            }
            if (ch === ";") {
                out += ";";
                const next = text[i + 1];
                if (next && next !== "}" && next !== ")") newline();
                continue;
            }
            out += ch;
        }

        return out
            .split("\n")
            .map(function (line) { return line.replace(/[ \t]+$/g, ""); })
            .join("\n")
            .replace(/\n{3,}/g, "\n\n")
            .trim();
    }

    function buildIdeHtml(code, filename) {
        const formatted = formatSourceCode(code);
        const lines = formatted.length ? formatted.split("\n") : [" "];
        let gutter = "";
        for (let i = 1; i <= lines.length; i++) gutter += i + (i < lines.length ? "\n" : "");

        return (
            '<div class="ide-editor ide-readonly article-ide">' +
            '<div class="ide-toolbar">' +
            '<div class="ide-dots" aria-hidden="true"><span></span><span></span><span></span></div>' +
            '<span class="ide-filename">' + escapeHtml(filename || "snippet.cs") + "</span>" +
            '<button type="button" class="ide-copy-btn" data-copy="' + encodeURIComponent(formatted) + '">' +
            '<i class="fa-regular fa-copy"></i> Copy</button>' +
            "</div>" +
            '<div class="ide-body ide-body-readonly">' +
            '<pre class="ide-gutter" aria-hidden="true">' + gutter + "</pre>" +
            '<pre class="ide-code"><code>' + escapeHtml(formatted) + "</code></pre>" +
            "</div></div>"
        );
    }

    function renderRichMarkdown(markdown) {
        const source = String(markdown || "").replace(/\r\n/g, "\n").trim();
        if (!source) return "<p class=\"text-gray\">No article content yet.</p>";

        const codeBlocks = [];
        const withPlaceholders = source.replace(/```[\w+-]*\n?([\s\S]*?)```/g, function (_, code) {
            const token = "@@CODEBLOCK" + codeBlocks.length + "@@";
            codeBlocks.push(String(code || "").replace(/^\n+|\n+$/g, ""));
            return token;
        });

        const lines = withPlaceholders.split("\n");
        const html = [];
        let paragraph = [];
        let listItems = [];

        function flushParagraph() {
            if (!paragraph.length) return;
            const text = paragraph.join(" ").trim();
            paragraph = [];
            if (!text) return;
            html.push("<p>" + inlineFormat(text) + "</p>");
        }

        function flushList() {
            if (!listItems.length) return;
            html.push("<ol class=\"article-steps\">" + listItems.join("") + "</ol>");
            listItems = [];
        }

        function inlineFormat(text) {
            return escapeHtml(text)
                .replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>")
                .replace(/`([^`]+)`/g, "<code class=\"inline-code\">$1</code>");
        }

        lines.forEach(function (rawLine) {
            const line = rawLine.trimEnd();
            const trimmed = line.trim();

            if (!trimmed) {
                flushParagraph();
                flushList();
                return;
            }

            const codeMatch = trimmed.match(/^@@CODEBLOCK(\d+)@@$/);
            if (codeMatch) {
                flushParagraph();
                flushList();
                const idx = Number(codeMatch[1]);
                html.push(buildIdeHtml(codeBlocks[idx] || "", "fixed.cs"));
                return;
            }

            if (/^###\s+/.test(trimmed)) {
                flushParagraph();
                flushList();
                html.push("<h3>" + inlineFormat(trimmed.replace(/^###\s+/, "")) + "</h3>");
                return;
            }
            if (/^##\s+/.test(trimmed)) {
                flushParagraph();
                flushList();
                html.push("<h2>" + inlineFormat(trimmed.replace(/^##\s+/, "")) + "</h2>");
                return;
            }
            if (/^#\s+/.test(trimmed)) {
                flushParagraph();
                flushList();
                // Skip page-level duplicate titles inside the body.
                return;
            }

            const ordered = trimmed.match(/^(\d+)\.\s+(.*)$/);
            if (ordered) {
                flushParagraph();
                listItems.push("<li>" + inlineFormat(ordered[2]) + "</li>");
                return;
            }

            if (/^[-*]\s+/.test(trimmed)) {
                flushParagraph();
                listItems.push("<li>" + inlineFormat(trimmed.replace(/^[-*]\s+/, "")) + "</li>");
                return;
            }

            flushList();
            paragraph.push(trimmed);
        });

        flushParagraph();
        flushList();
        return html.join("\n");
    }

    function wireCopyButtons(scope) {
        scope.querySelectorAll(".ide-copy-btn").forEach(function (btn) {
            btn.addEventListener("click", function () {
                const text = decodeURIComponent(btn.getAttribute("data-copy") || "");
                const done = function () {
                    btn.classList.add("is-copied");
                    btn.innerHTML = '<i class="fa-solid fa-check"></i> Copied';
                    setTimeout(function () {
                        btn.classList.remove("is-copied");
                        btn.innerHTML = '<i class="fa-regular fa-copy"></i> Copy';
                    }, 1500);
                };
                if (navigator.clipboard && navigator.clipboard.writeText) {
                    navigator.clipboard.writeText(text).then(done).catch(done);
                } else {
                    done();
                }
            });
        });

        scope.querySelectorAll(".ide-code").forEach(function (codeEl) {
            const gutter = codeEl.parentElement && codeEl.parentElement.querySelector(".ide-gutter");
            if (!gutter) return;
            codeEl.addEventListener("scroll", function () {
                gutter.scrollTop = codeEl.scrollTop;
            });
        });
    }

    async function load() {
        const key = id || problemId || code;
        if (!key) {
            root.innerHTML = '<div class="surface-card empty-state">Missing article id.</div>';
            return;
        }

        try {
            const url = new URL("/api/Problems/articles/" + encodeURIComponent(key), API_BASE).href;
            const res = await window.DevPilotApi.request(url, { skipAuth: true });
            if (!res.ok || !res.data) {
                throw new Error(res.message || "Article not found");
            }

            const data = res.data;
            const title = data.title || data.Title || "Knowledge article";
            const articleCode = data.articleCode || data.ArticleCode || "";
            const problemCode = data.problemCode || data.ProblemCode || "";
            const status = data.status || data.Status || "Solved";
            const framework = data.framework || data.Framework || "";
            const summary = data.summary || data.Summary || "";
            const content = data.contentMarkdown || data.ContentMarkdown || summary;
            const challengeId = data.challengeId ?? data.ChallengeId;
            const challengeHref = challengeId
                ? ("challenge-details.html?id=" + encodeURIComponent(challengeId))
                : "challenges.html";
            const challengeLabel = challengeId ? "Solve this challenge" : "Browse challenges";

            root.innerHTML =
                '<div class="surface-card mb-4">' +
                '<div class="d-flex flex-wrap gap-2 mb-3">' +
                '<span class="badge-soft ai">' + escapeHtml(articleCode || problemCode) + "</span>" +
                '<span class="badge-soft solved">' + escapeHtml(status) + "</span>" +
                (framework ? '<span class="badge-soft status-progress">' + escapeHtml(framework) + "</span>" : "") +
                "</div>" +
                '<h1 class="page-title mb-2">' + escapeHtml(title) + "</h1>" +
                (summary ? '<p class="article-lead mb-2">' + escapeHtml(summary) + "</p>" : "") +
                '<p class="text-gray mb-0">Updated ' + escapeHtml(formatDate(data.resolvedAt || data.ResolvedAt || data.createdAt || data.CreatedAt)) + "</p>" +
                "</div>" +

                '<div class="row g-4">' +
                '<div class="col-lg-8">' +
                '<article class="surface-card content-prose article-body">' +
                renderRichMarkdown(content) +
                "</article>" +
                "</div>" +
                '<div class="col-lg-4">' +
                '<div class="surface-card mb-3">' +
                "<h3 class=\"h5\">Practice this fix</h3>" +
                '<p class="text-gray">Take a short challenge to apply the solution yourself.</p>' +
                '<a class="btn btn-primary w-100 mb-2" href="' + challengeHref + '">' + challengeLabel + "</a>" +
                '<a class="btn btn-outline-card w-100" href="submit-problem.html">Debug your own issue</a>' +
                "</div>" +
                '<div class="alert alert-success-soft">' +
                '<i class="fa-solid fa-circle-check text-success-brand me-2"></i>' +
                "Every solved problem becomes searchable knowledge for the next developer." +
                "</div>" +
                "</div>" +
                "</div>";

            wireCopyButtons(root);
        } catch (e) {
            root.innerHTML = '<div class="surface-card empty-state">' + escapeHtml(e.message || "Failed to load article") + "</div>";
        }
    }

    load();
})();
