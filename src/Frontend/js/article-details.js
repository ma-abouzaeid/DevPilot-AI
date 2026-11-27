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
        if (newlineCount >= 2) return text;

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
            '<span class="ide-filename">' + escapeHtml(filename || "fixed.cs") + "</span>" +
            '<button type="button" class="ide-copy-btn" data-copy="' + encodeURIComponent(formatted) + '">' +
            '<i class="fa-regular fa-copy"></i> Copy</button>' +
            "</div>" +
            '<div class="ide-body ide-body-readonly">' +
            '<pre class="ide-gutter" aria-hidden="true">' + gutter + "</pre>" +
            '<pre class="ide-code"><code>' + escapeHtml(formatted) + "</code></pre>" +
            "</div></div>"
        );
    }

    function looksLikeSourceCode(text) {
        const t = String(text || "").trim();
        if (t.length < 12) return false;
        return /[{;}]/.test(t) ||
            /\b(using|function|const|class|def|import|public|private|return|var)\b/.test(t);
    }

    function normalizeArticleMarkdown(markdown) {
        let source = String(markdown || "").replace(/\r\n/g, "\n");

        // Normalize "Corrected code:" blocks into heading + fenced code.
        source = source.replace(
            /(^|\n)(?:##\s*)?Corrected code\s*:?\s*\n*(?:```[\w+-]*\n)?([\s\S]*?)(?:\n```[ \t]*)?(?=\n#{1,3}\s|\n*$)/gi,
            function (match, pre, body) {
                let code = String(body || "").trim();
                code = code.replace(/^```[\w+-]*\n?/, "").replace(/\n```$/, "").trim();
                if (!code || !looksLikeSourceCode(code)) return match;
                return pre + "## Corrected code\n\n```\n" + code + "\n```\n";
            }
        );

        return source.trim();
    }

    function renderRichMarkdown(markdown) {
        const source = normalizeArticleMarkdown(markdown);
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

            const correctedInline = text.match(/^Corrected code:\s*([\s\S]+)$/i);
            if (correctedInline && looksLikeSourceCode(correctedInline[1])) {
                html.push('<h2 class="article-code-heading">Corrected code</h2>');
                html.push(buildIdeHtml(correctedInline[1], "fixed.cs"));
                return;
            }

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
                const heading = trimmed.replace(/^##\s+/, "");
                // Special style when heading is only for the IDE block.
                if (/^corrected code$/i.test(heading)) {
                    html.push('<h2 class="article-code-heading">' + inlineFormat(heading) + "</h2>");
                } else {
                    html.push("<h2>" + inlineFormat(heading) + "</h2>");
                }
                return;
            }
            if (/^#\s+/.test(trimmed)) {
                flushParagraph();
                flushList();
                // Skip duplicate H1 in article body.
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

    function fallbackCopy(text) {
        const ta = document.createElement("textarea");
        ta.value = text;
        ta.setAttribute("readonly", "");
        ta.style.position = "absolute";
        ta.style.left = "-9999px";
        document.body.appendChild(ta);
        ta.select();
        try { document.execCommand("copy"); } catch (e) { /* ignore */ }
        document.body.removeChild(ta);
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
                    navigator.clipboard.writeText(text).then(done).catch(function () {
                        fallbackCopy(text);
                        done();
                    });
                } else {
                    fallbackCopy(text);
                    done();
                }
            });
        });

        scope.querySelectorAll(".ide-body-readonly").forEach(function (body) {
            const gutter = body.querySelector(".ide-gutter");
            const codeEl = body.querySelector(".ide-code");
            if (!gutter || !codeEl) return;
            codeEl.addEventListener("scroll", function () {
                gutter.scrollTop = codeEl.scrollTop;
            });
        });
    }

    function requireAuth() {
        if (window.DevPilotApi && window.DevPilotApi.isAuthenticated()) return true;
        const page = (window.location.pathname.split("/").pop() || "article-details.html") + window.location.search;
        window.location.href = "login.html?redirect=" + encodeURIComponent(page);
        return false;
    }

    function renderComments(comments) {
        const list = Array.isArray(comments) ? comments : [];
        if (!list.length) {
            return '<p class="text-gray mb-0" id="comments-empty">No comments yet. Be the first to share a tip.</p>';
        }

        return list.map(function (c) {
            const commentId = c.articleCommentId || c.ArticleCommentId;
            const name = c.userName || c.UserName || "Developer";
            const body = c.body || c.Body || "";
            const created = formatDate(c.createdAt || c.CreatedAt);
            const isMine = !!(c.isMine || c.IsMine);
            return (
                '<div class="article-comment" data-comment-id="' + escapeHtml(commentId) + '">' +
                '<div class="article-comment-meta">' +
                "<strong>" + escapeHtml(name) + "</strong>" +
                '<span class="text-gray">' + escapeHtml(created) + "</span>" +
                (isMine
                    ? '<button type="button" class="article-comment-delete" data-delete-comment="' + escapeHtml(commentId) + '" title="Delete comment"><i class="fa-regular fa-trash-can"></i></button>'
                    : "") +
                "</div>" +
                '<p class="mb-0">' + escapeHtml(body) + "</p>" +
                "</div>"
            );
        }).join("");
    }

    function applyEngagement(state) {
        const likeBtn = document.getElementById("btn-like");
        const saveBtn = document.getElementById("btn-save");
        const likeCount = document.getElementById("like-count");
        const saveCount = document.getElementById("save-count");
        const commentCount = document.getElementById("comment-count");
        const commentsList = document.getElementById("comments-list");

        if (likeCount) likeCount.textContent = String(state.likesCount || 0);
        if (saveCount) saveCount.textContent = String(state.savesCount || 0);
        if (commentCount) commentCount.textContent = String(state.commentsCount || 0);

        if (likeBtn) {
            likeBtn.classList.toggle("is-active", !!state.likedByMe);
            likeBtn.setAttribute("aria-pressed", state.likedByMe ? "true" : "false");
            likeBtn.innerHTML = (state.likedByMe
                ? '<i class="fa-solid fa-heart"></i> Liked'
                : '<i class="fa-regular fa-heart"></i> Like') +
                ' <span class="article-engage-count" id="like-count">' + (state.likesCount || 0) + "</span>";
        }

        if (saveBtn) {
            saveBtn.classList.toggle("is-active", !!state.savedByMe);
            saveBtn.setAttribute("aria-pressed", state.savedByMe ? "true" : "false");
            saveBtn.innerHTML = (state.savedByMe
                ? '<i class="fa-solid fa-bookmark"></i> Saved'
                : '<i class="fa-regular fa-bookmark"></i> Save') +
                ' <span class="article-engage-count" id="save-count">' + (state.savesCount || 0) + "</span>";
        }

        if (commentsList) {
            commentsList.innerHTML = renderComments(state.comments || []);
        }
    }

    async function loadEngagement(articleId) {
        const url = new URL("/api/Problems/articles/" + articleId + "/engagement", API_BASE).href;
        const opts = window.DevPilotApi.isAuthenticated() ? {} : { skipAuth: true };
        const res = await window.DevPilotApi.request(url, opts);
        if (!res.ok || !res.data) {
            throw new Error(res.message || "Could not load engagement");
        }

        const data = res.data;
        return {
            likesCount: data.likesCount ?? data.LikesCount ?? 0,
            commentsCount: data.commentsCount ?? data.CommentsCount ?? 0,
            savesCount: data.savesCount ?? data.SavesCount ?? 0,
            likedByMe: !!(data.likedByMe ?? data.LikedByMe),
            savedByMe: !!(data.savedByMe ?? data.SavedByMe),
            comments: data.comments || data.Comments || []
        };
    }

    function wireEngagement(articleId) {
        const likeBtn = document.getElementById("btn-like");
        const saveBtn = document.getElementById("btn-save");
        const shareBtn = document.getElementById("btn-share");
        const commentForm = document.getElementById("comment-form");
        const commentInput = document.getElementById("comment-body");
        const commentsList = document.getElementById("comments-list");
        const feedback = document.getElementById("engage-feedback");

        function showFeedback(message, isError) {
            if (!feedback) return;
            feedback.textContent = message;
            feedback.classList.toggle("is-error", !!isError);
            feedback.classList.remove("d-none");
            setTimeout(function () { feedback.classList.add("d-none"); }, 2500);
        }

        if (likeBtn) {
            likeBtn.addEventListener("click", async function () {
                if (!requireAuth()) return;
                likeBtn.disabled = true;
                try {
                    const url = new URL("/api/Problems/articles/" + articleId + "/like", API_BASE).href;
                    const res = await window.DevPilotApi.request(url, { method: "POST" });
                    if (!res.ok || !res.data) throw new Error(res.message || "Like failed");
                    const active = !!(res.data.active ?? res.data.Active);
                    const count = res.data.count ?? res.data.Count ?? 0;
                    likeBtn.classList.toggle("is-active", active);
                    likeBtn.setAttribute("aria-pressed", active ? "true" : "false");
                    likeBtn.innerHTML = (active
                        ? '<i class="fa-solid fa-heart"></i> Liked'
                        : '<i class="fa-regular fa-heart"></i> Like') +
                        ' <span class="article-engage-count" id="like-count">' + count + "</span>";
                } catch (e) {
                    showFeedback(e.message || "Like failed", true);
                } finally {
                    likeBtn.disabled = false;
                }
            });
        }

        if (saveBtn) {
            saveBtn.addEventListener("click", async function () {
                if (!requireAuth()) return;
                saveBtn.disabled = true;
                try {
                    const url = new URL("/api/Problems/articles/" + articleId + "/save", API_BASE).href;
                    const res = await window.DevPilotApi.request(url, { method: "POST" });
                    if (!res.ok || !res.data) throw new Error(res.message || "Save failed");
                    const active = !!(res.data.active ?? res.data.Active);
                    const count = res.data.count ?? res.data.Count ?? 0;
                    saveBtn.classList.toggle("is-active", active);
                    saveBtn.setAttribute("aria-pressed", active ? "true" : "false");
                    saveBtn.innerHTML = (active
                        ? '<i class="fa-solid fa-bookmark"></i> Saved'
                        : '<i class="fa-regular fa-bookmark"></i> Save') +
                        ' <span class="article-engage-count" id="save-count">' + count + "</span>";
                    showFeedback(active ? "Article saved." : "Removed from saved.");
                } catch (e) {
                    showFeedback(e.message || "Save failed", true);
                } finally {
                    saveBtn.disabled = false;
                }
            });
        }

        if (shareBtn) {
            shareBtn.addEventListener("click", async function () {
                const shareUrl = window.location.href;
                const title = document.querySelector(".page-title")?.textContent || "DevPilot article";
                try {
                    if (navigator.share) {
                        await navigator.share({ title: title, url: shareUrl });
                        showFeedback("Shared.");
                        return;
                    }
                } catch (e) {
                    if (e && e.name === "AbortError") return;
                }

                if (navigator.clipboard && navigator.clipboard.writeText) {
                    try {
                        await navigator.clipboard.writeText(shareUrl);
                        showFeedback("Link copied.");
                        return;
                    } catch (e) { /* fall through */ }
                }
                fallbackCopy(shareUrl);
                showFeedback("Link copied.");
            });
        }

        if (commentForm && commentInput) {
            commentForm.addEventListener("submit", async function (e) {
                e.preventDefault();
                if (!requireAuth()) return;
                const body = commentInput.value.trim();
                if (!body) {
                    showFeedback("Write a comment first.", true);
                    return;
                }

                const submitBtn = commentForm.querySelector('button[type="submit"]');
                if (submitBtn) submitBtn.disabled = true;
                try {
                    const url = new URL("/api/Problems/articles/" + articleId + "/comments", API_BASE).href;
                    const res = await window.DevPilotApi.request(url, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ body: body })
                    });
                    if (!res.ok || !res.data) throw new Error(res.message || "Comment failed");
                    commentInput.value = "";
                    const state = await loadEngagement(articleId);
                    applyEngagement(state);
                    showFeedback("Comment posted.");
                } catch (err) {
                    showFeedback(err.message || "Comment failed", true);
                } finally {
                    if (submitBtn) submitBtn.disabled = false;
                }
            });
        }

        if (commentsList) {
            commentsList.addEventListener("click", async function (e) {
                const btn = e.target.closest("[data-delete-comment]");
                if (!btn) return;
                if (!requireAuth()) return;
                const commentId = btn.getAttribute("data-delete-comment");
                if (!commentId) return;
                btn.disabled = true;
                try {
                    const url = new URL(
                        "/api/Problems/articles/" + articleId + "/comments/" + encodeURIComponent(commentId),
                        API_BASE
                    ).href;
                    const res = await window.DevPilotApi.request(url, { method: "DELETE" });
                    if (!res.ok) throw new Error(res.message || "Delete failed");
                    const state = await loadEngagement(articleId);
                    applyEngagement(state);
                    showFeedback("Comment deleted.");
                } catch (err) {
                    showFeedback(err.message || "Delete failed", true);
                    btn.disabled = false;
                }
            });
        }
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
            const articleId = data.knowledgeArticleId || data.KnowledgeArticleId;
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
            const loggedIn = window.DevPilotApi && window.DevPilotApi.isAuthenticated();

            root.innerHTML =
                '<div class="surface-card mb-4">' +
                '<div class="d-flex flex-wrap gap-2 mb-3">' +
                '<span class="badge-soft ai">' + escapeHtml(articleCode || problemCode) + "</span>" +
                '<span class="badge-soft solved">' + escapeHtml(status) + "</span>" +
                (framework ? '<span class="badge-soft status-progress">' + escapeHtml(framework) + "</span>" : "") +
                "</div>" +
                '<h1 class="page-title mb-2">' + escapeHtml(title) + "</h1>" +
                (summary ? '<p class="article-lead mb-2">' + escapeHtml(summary) + "</p>" : "") +
                '<p class="text-gray mb-3">Updated ' + escapeHtml(formatDate(data.resolvedAt || data.ResolvedAt || data.createdAt || data.CreatedAt)) + "</p>" +
                '<div class="article-engage-bar" id="engage-bar">' +
                '<button type="button" class="article-engage-btn" id="btn-like" aria-pressed="false">' +
                '<i class="fa-regular fa-heart"></i> Like <span class="article-engage-count" id="like-count">0</span></button>' +
                '<button type="button" class="article-engage-btn" id="btn-save" aria-pressed="false">' +
                '<i class="fa-regular fa-bookmark"></i> Save <span class="article-engage-count" id="save-count">0</span></button>' +
                '<button type="button" class="article-engage-btn" id="btn-share">' +
                '<i class="fa-solid fa-share-nodes"></i> Share</button>' +
                '<span class="article-engage-meta"><i class="fa-regular fa-comment"></i> <span id="comment-count">0</span> comments</span>' +
                "</div>" +
                '<p class="article-engage-feedback d-none" id="engage-feedback" role="status"></p>' +
                "</div>" +

                '<div class="row g-4">' +
                '<div class="col-lg-8">' +
                '<article class="surface-card content-prose article-body mb-4">' +
                renderRichMarkdown(content) +
                "</article>" +
                '<section class="surface-card article-comments" id="comments-section">' +
                '<h2 class="h5 mb-3">Comments</h2>' +
                (loggedIn
                    ? ('<form id="comment-form" class="article-comment-form mb-3">' +
                        '<label class="form-label" for="comment-body">Add a comment</label>' +
                        '<textarea id="comment-body" class="form-control" rows="3" maxlength="2000" placeholder="Share a tip, ask a follow-up, or note what worked for you…" required></textarea>' +
                        '<div class="d-flex justify-content-end mt-2">' +
                        '<button type="submit" class="btn btn-primary btn-sm">Post comment</button>' +
                        "</div></form>")
                    : '<p class="text-gray mb-3">Log in to leave a comment. <a class="link-blue" href="login.html?redirect=' +
                        encodeURIComponent((window.location.pathname.split("/").pop() || "article-details.html") + window.location.search) + '">Sign in</a></p>') +
                '<div id="comments-list" class="article-comments-list"></div>' +
                "</section>" +
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
            if (articleId) {
                wireEngagement(articleId);
                try {
                    const state = await loadEngagement(articleId);
                    applyEngagement(state);
                } catch (engageErr) {
                    const feedback = document.getElementById("engage-feedback");
                    if (feedback) {
                        feedback.textContent = engageErr.message || "Engagement unavailable.";
                        feedback.classList.add("is-error");
                        feedback.classList.remove("d-none");
                    }
                }
            }
        } catch (e) {
            root.innerHTML = '<div class="surface-card empty-state">' + escapeHtml(e.message || "Failed to load article") + "</div>";
        }
    }

    load();
})();
