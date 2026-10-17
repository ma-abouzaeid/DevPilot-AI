/* ============================================================
   DevPilot AI - Motion Controller
   Lightweight, dependency-free.

   Responsibilities:
   1. Scroll-reveal system (IntersectionObserver, staggered)
   2. "AI network" canvas backdrops (nodes, light-flow pulses,
      node pulses, pointer parallax; pause off-screen)
   3. Code window typing animation (.code-box)
   4. Testimonial word-by-word reveal (.quote-box)
   5. Statistic count-up (.hero-stats)
   6. Off-screen pausing for decorative CSS motion
   7. Teardown / instant finish when prefers-reduced-motion is
      enabled (at load or mid-session)

   Decorative motion never blocks interaction and is fully
   disabled under prefers-reduced-motion.
   ============================================================ */
(function () {
    "use strict";

    const motionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
    const prefersReduced = function () { return motionQuery.matches; };

    /* Elements that start hidden and animate in on scroll.
       Kept in CSS + JS sync; loaded only on content pages. */
    const REVEAL_SELECTOR =
        ".workflow-col, .feature-col, .article-col, .why-item, .code-box, " +
        ".section-badge, .section-title, .section-text, " +
        ".cta-section h3, .cta-section p, .cta-section .btn";

    const pickMotion = function (fn) {
        if (typeof motionQuery.addEventListener === "function") {
            motionQuery.addEventListener("change", fn);
        } else if (typeof motionQuery.addListener === "function") {
            motionQuery.addListener(fn); // Safari < 14
        }
    };

    const onPageHide = function (fn) {
        window.addEventListener("pagehide", fn, { once: true });
    };

    /* ------------------------------------------------------------
       1. Scroll reveal
       ------------------------------------------------------------ */
    function initReveal() {
        if (prefersReduced() || !("IntersectionObserver" in window)) return;

        // Hero and auth sections run their own load-time sequences.
        const hero = document.querySelector(".hero-section");
        const auth = document.querySelector(".login-section");

        const targets = Array.prototype.filter.call(
            document.querySelectorAll(REVEAL_SELECTOR),
            function (el) {
                return (!hero || !hero.contains(el)) &&
                       (!auth || !auth.contains(el));
            }
        );

        if (!targets.length) return;

        targets.forEach(function (el) { el.classList.add("reveal"); });

        // One coordinated stagger per section (not per element globally).
        const sections = [];
        targets.forEach(function (el) {
            const section = el.closest("section") || document.body;
            if (sections.indexOf(section) === -1) sections.push(section);
        });

        sections.forEach(function (section) {
            const group = section.querySelectorAll(".reveal");
            group.forEach(function (el, i) {
                el.style.setProperty(
                    "--reveal-delay",
                    Math.min(i, 8) * 80 + "ms"
                );
            });
        });

        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        }, {
            threshold: 0.15,
            rootMargin: "0px 0px -8% 0px"
        });

        targets.forEach(function (el) { observer.observe(el); });

        // Safety net: never leave content permanently hidden, even if the
        // observer is interrupted (bfcache restore, unusual embedding...).
        const fallback = setTimeout(function () {
            document.querySelectorAll(".reveal:not(.is-visible)")
                .forEach(function (el) { el.classList.add("is-visible"); });
        }, 3000);

        pickMotion(function () {
            clearTimeout(fallback);
        });

        onPageHide(function () {
            clearTimeout(fallback);
            observer.disconnect();
        });
    }

    /* ------------------------------------------------------------
       2. Network backdrop
       A restrained mesh of drifting nodes connected by faint lines,
       with slow light-flow pulses and occasional node rings.
       Pauses when off-screen or the tab is hidden; never runs under
       prefers-reduced-motion.
       ------------------------------------------------------------ */
    function initNetworkBackdrop(target, options) {
        if (!target || prefersReduced()) return null;

        const opts = options || {};
        const canvasClass = opts.canvasClass || "net-canvas";
        const primaryClass = canvasClass.split(/\s+/)[0];
        if (target.querySelector("." + primaryClass)) return null;

        const canvas = document.createElement("canvas");
        canvas.className = canvasClass;
        canvas.setAttribute("aria-hidden", "true");
        target.prepend(canvas);

        const ctx = canvas.getContext("2d");
        const dpr = Math.min(window.devicePixelRatio || 1, 2);

        const LINK_DIST = opts.linkDist || 140;
        const DENSITY = opts.density || 34000;
        const MIN_NODES = opts.minNodes || 16;
        const MAX_NODES = opts.maxNodes || 42;
        const NODE_SPEED = opts.speed || 0.22;
        const NODE_ALPHA = opts.nodeAlpha || 0.6;
        const CYAN_ALPHA = opts.cyanAlpha || 0.75;
        const LINK_ALPHA = opts.linkAlpha || 0.35;
        const PARALLAX = opts.parallax || 0;      // max px offset (desktop)
        const LIGHT_FLOW = opts.lightFlow !== false;
        const MAX_PULSES = 3;

        let width = 0;
        let height = 0;
        let nodes = [];
        let pulses = [];
        let raf = 0;
        let running = false;
        let inView = !("IntersectionObserver" in window);

        function seed() {
            const count = Math.max(
                MIN_NODES,
                Math.min(MAX_NODES, Math.round((width * height) / DENSITY))
            );
            nodes = [];
            pulses = [];
            for (let i = 0; i < count; i++) {
                nodes.push({
                    x: Math.random() * width,
                    y: Math.random() * height,
                    vx: (Math.random() - 0.5) * NODE_SPEED,
                    vy: (Math.random() - 0.5) * NODE_SPEED,
                    r: 0.9 + Math.random() * 1.6,
                    cyan: Math.random() < 0.28,
                    pulse: null                       // 0..1 while ringing
                });
            }
        }

        function resize() {
            const rect = target.getBoundingClientRect();
            width = Math.max(1, rect.width);
            height = Math.max(1, rect.height);
            canvas.width = Math.round(width * dpr);
            canvas.height = Math.round(height * dpr);
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            if (!nodes.length) seed();
        }

        /* pointer parallax (desktop / fine pointers only) */
        const finePointer = window.matchMedia("(pointer: fine)");
        let pointerX = 0;
        let pointerY = 0;
        let offX = 0;
        let offY = 0;

        function onPointerMove(e) {
            if (!PARALLAX || !finePointer.matches) return;
            const rect = target.getBoundingClientRect();
            if (!rect.width || !rect.height) return;
            pointerX = -((e.clientX - rect.left) / rect.width - 0.5) * PARALLAX;
            pointerY = -((e.clientY - rect.top) / rect.height - 0.5) * PARALLAX;
        }

        function onPointerLeave() {
            pointerX = 0;
            pointerY = 0;
        }

        function spawnPulse(dt) {
            if (!LIGHT_FLOW || pulses.length >= MAX_PULSES) return;
            if (Math.random() > dt * 0.7) return;     // ~1 pulse / 1.4 s
            for (let attempt = 0; attempt < 10; attempt++) {
                const a = (Math.random() * nodes.length) | 0;
                const b = (Math.random() * nodes.length) | 0;
                if (a === b || !nodes[a] || !nodes[b]) continue;
                const dx = nodes[a].x - nodes[b].x;
                const dy = nodes[a].y - nodes[b].y;
                if (Math.sqrt(dx * dx + dy * dy) <= LINK_DIST) {
                    pulses.push({ a: a, b: b, t: 0 });
                    return;
                }
            }
        }

        function drawPulses(dt) {
            for (let i = pulses.length - 1; i >= 0; i--) {
                const p = pulses[i];
                if (!nodes[p.a] || !nodes[p.b]) { pulses.splice(i, 1); continue; }
                p.t += dt / 1.2;
                if (p.t >= 1) { pulses.splice(i, 1); continue; }

                const a = nodes[p.a];
                const b = nodes[p.b];
                const x = a.x + (b.x - a.x) * p.t;
                const y = a.y + (b.y - a.y) * p.t;
                const env = Math.sin(Math.PI * p.t);  // fade in / fade out

                ctx.fillStyle = "rgba(0, 102, 255, " + (env * 0.16).toFixed(3) + ")";
                ctx.beginPath();
                ctx.arc(x, y, 7, 0, Math.PI * 2);
                ctx.fill();

                ctx.fillStyle = "rgba(0, 212, 255, " + (env * 0.55).toFixed(3) + ")";
                ctx.beginPath();
                ctx.arc(x, y, 1.8, 0, Math.PI * 2);
                ctx.fill();
            }
        }

        function step(dt) {
            offX += (pointerX - offX) * Math.min(1, dt * 3);
            offY += (pointerY - offY) * Math.min(1, dt * 3);

            ctx.clearRect(0, 0, width, height);
            ctx.save();
            ctx.translate(offX, offY);

            spawnPulse(dt);

            /* links */
            for (let i = 0; i < nodes.length; i++) {
                const n = nodes[i];
                n.x += n.vx * dt;
                n.y += n.vy * dt;
                if (n.x < -20) n.x = width + 20;
                if (n.x > width + 20) n.x = -20;
                if (n.y < -20) n.y = height + 20;
                if (n.y > height + 20) n.y = -20;

                for (let j = i + 1; j < nodes.length; j++) {
                    const m = nodes[j];
                    const dx = n.x - m.x;
                    const dy = n.y - m.y;
                    const dist = Math.sqrt(dx * dx + dy * dy);
                    if (dist > LINK_DIST) continue;
                    const alpha = (1 - dist / LINK_DIST) * LINK_ALPHA;
                    ctx.strokeStyle = "rgba(0, 102, 255, " + alpha.toFixed(3) + ")";
                    ctx.lineWidth = 1;
                    ctx.beginPath();
                    ctx.moveTo(n.x, n.y);
                    ctx.lineTo(m.x, m.y);
                    ctx.stroke();
                }
            }

            drawPulses(dt);

            /* nodes + soft rings */
            for (let i = 0; i < nodes.length; i++) {
                const n = nodes[i];

                if (n.pulse === null) {
                    if (Math.random() < dt * 0.1) n.pulse = 0;   // ~1 ring / 10 s
                } else {
                    n.pulse += dt / 1.8;
                    if (n.pulse >= 1) {
                        n.pulse = null;
                    } else {
                        const ringAlpha = (1 - n.pulse) * 0.3;
                        ctx.strokeStyle =
                            "rgba(0, 212, 255, " + ringAlpha.toFixed(3) + ")";
                        ctx.lineWidth = 1;
                        ctx.beginPath();
                        ctx.arc(n.x, n.y, n.r + 2 + n.pulse * 16, 0, Math.PI * 2);
                        ctx.stroke();
                    }
                }

                ctx.fillStyle = n.cyan
                    ? "rgba(0, 212, 255, " + CYAN_ALPHA + ")"
                    : "rgba(125, 180, 255, " + NODE_ALPHA + ")";
                ctx.beginPath();
                ctx.arc(n.x, n.y, n.r, 0, Math.PI * 2);
                ctx.fill();
            }

            ctx.restore();
        }

        let last = 0;
        function frame(now) {
            if (!running) return;
            const dt = Math.min((now - last) / 1000, 0.05); // clamp tab-switch jumps
            last = now;
            step(dt || 0);
            raf = requestAnimationFrame(frame);
        }

        function sync() {
            const shouldRun = inView && !document.hidden && !prefersReduced();
            if (shouldRun === running) return;
            running = shouldRun;
            if (running) {
                last = 0;
                raf = requestAnimationFrame(frame);
            } else if (raf) {
                cancelAnimationFrame(raf);
                raf = 0;
            }
        }

        resize();
        canvas.classList.add("is-active");

        let io = null;
        if ("IntersectionObserver" in window) {
            io = new IntersectionObserver(function (entries) {
                inView = entries[0].isIntersecting;
                sync();
            });
            io.observe(target);
            inView = target.getBoundingClientRect().bottom > 0;
        }

        document.addEventListener("visibilitychange", sync);
        if (PARALLAX) {
            target.addEventListener("pointermove", onPointerMove, { passive: true });
            target.addEventListener("pointerleave", onPointerLeave, { passive: true });
        }

        let resizeRaf = 0;
        function onResize() {
            if (resizeRaf) return;
            resizeRaf = requestAnimationFrame(function () {
                resizeRaf = 0;
                resize();
            });
        }
        window.addEventListener("resize", onResize);

        function destroy() {
            running = false;
            if (raf) cancelAnimationFrame(raf);
            if (resizeRaf) cancelAnimationFrame(resizeRaf);
            if (io) io.disconnect();
            document.removeEventListener("visibilitychange", sync);
            window.removeEventListener("resize", onResize);
            target.removeEventListener("pointermove", onPointerMove);
            target.removeEventListener("pointerleave", onPointerLeave);
            canvas.remove();
        }

        pickMotion(function () { if (prefersReduced()) destroy(); });
        onPageHide(destroy);

        sync();
        return destroy;
    }

    function initBackdrops() {
        initNetworkBackdrop(document.querySelector(".hero-section"), {
            canvasClass: "net-canvas net-canvas-hero",
            density: 34000,
            minNodes: 16,
            maxNodes: 42,
            nodeAlpha: 0.6,
            cyanAlpha: 0.75,
            linkAlpha: 0.35,
            parallax: 7
        });

        initNetworkBackdrop(document.querySelector(".why-section"), {
            canvasClass: "net-canvas net-canvas-soft",
            density: 42000,
            minNodes: 14,
            maxNodes: 30,
            nodeAlpha: 0.4,
            cyanAlpha: 0.5,
            linkAlpha: 0.22,
            speed: 0.18,
            parallax: 4
        });
    }

    /* ------------------------------------------------------------
       3. Code window typing
       Types the existing, author-written code character by
       character, preserving markup, classes and indentation.
       The full text stays available to assistive technologies in a
       visually hidden copy; the animated box is aria-hidden so no
       partial code is announced character by character.
       ------------------------------------------------------------ */
    function initCodeTyping() {
        const codeBox = document.querySelector(".code-box");
        if (!codeBox || codeBox.dataset.typed === "true") return;
        if (prefersReduced()) return;
        codeBox.dataset.typed = "true";

        const COLLAPSIBLE = /[ \t\r\n\f\v]/;

        const sourceLines = Array.prototype.slice.call(
            codeBox.querySelectorAll("p.code-comment, p.code-line")
        );
        if (!sourceLines.length) return;

        /* --- parse the authored markup into a char stream per block --- */
        function parseBlock(p) {
            const raw = [];

            function pushText(value, cls) {
                for (let i = 0; i < value.length; i++) {
                    raw.push({ ch: value.charAt(i), cls: cls || null });
                }
            }

            Array.prototype.forEach.call(p.childNodes, function (node) {
                if (node.nodeType === 3) {
                    pushText(node.nodeValue, null);
                } else if (node.nodeType === 1) {
                    if (node.tagName === "BR") {
                        raw.push({ br: true });
                    } else {
                        pushText(node.textContent, node.className || null);
                    }
                }
            });

            /* normalize whitespace exactly like the browser renders it
               (collapse runs, then trim line-leading/trailing spaces) */
            const collapsed = [];
            for (let i = 0; i < raw.length; i++) {
                const item = raw[i];
                if (item.br) { collapsed.push(item); continue; }
                if (COLLAPSIBLE.test(item.ch)) {
                    while (i + 1 < raw.length && !raw[i + 1].br &&
                           COLLAPSIBLE.test(raw[i + 1].ch)) {
                        i++;
                    }
                    collapsed.push({ ch: " ", cls: item.cls });
                } else {
                    collapsed.push(item);
                }
            }

            const chars = [];
            let lineStart = 0;
            for (let i = 0; i <= collapsed.length; i++) {
                const isBreak = i === collapsed.length || collapsed[i].br;
                if (!isBreak) continue;
                let end = i;                       // exclusive
                while (end > lineStart && collapsed[end - 1].ch === " ") end--;
                let start = lineStart;
                while (start < end && collapsed[start].ch === " ") start++;
                for (let k = start; k < end; k++) chars.push(collapsed[k]);
                if (i < collapsed.length) chars.push({ br: true });
                lineStart = i + 1;
            }
            return { cls: p.className, chars: chars };
        }

        const blocks = sourceLines.map(parseBlock);

        const cursor = document.createElement("span");
        cursor.className = "type-cursor";
        cursor.setAttribute("aria-hidden", "true");

        const rendered = [];        // typed <p> elements, created on start
        let started = false;
        let finished = false;
        let blockIndex = 0;
        let charIndex = 0;
        let segment = null;         // current styled span
        let segmentCls = "__none__";
        let timer = 0;
        let startTimer = 0;
        let guard = 0;
        let lastProgressAt = 0;     // used by the stall watchdog

        function appendChar(item) {
            const p = rendered[blockIndex];
            if (item.cls !== segmentCls) {
                if (item.cls) {
                    segment = document.createElement("span");
                    segment.className = item.cls;
                    p.appendChild(segment);
                } else {
                    segment = null;
                }
                segmentCls = item.cls;
            }
            if (segment) {
                segment.appendChild(document.createTextNode(item.ch));
            } else {
                p.appendChild(document.createTextNode(item.ch));
            }
            p.appendChild(cursor);          // keep the caret at the tip
        }

        function delayFor(ch, isBreak) {
            if (isBreak) return 220 + Math.random() * 170;
            let d = 22 + Math.random() * 18;            // 22-40 ms/char
            if (ch === "{") d += 90;
            else if (ch === "}") d += 110;
            else if (ch === ";") d += 40;
            else if (Math.random() < 0.05) d += 90 + Math.random() * 130;
            return d;
        }

        function typeStep() {
            if (finished || !started) return;
            lastProgressAt = Date.now();

            if (blockIndex >= blocks.length) { finish(); return; }
            const chars = blocks[blockIndex].chars;
            if (charIndex >= chars.length) {
                blockIndex++;
                charIndex = 0;
                typeStep();
                return;
            }

            const item = chars[charIndex++];
            if (item.br) {
                rendered[blockIndex].appendChild(document.createElement("br"));
                rendered[blockIndex].appendChild(cursor);
                segment = null;
                segmentCls = "__none__";
            } else {
                appendChar(item);
            }

            timer = setTimeout(typeStep, delayFor(item.ch, !!item.br));
        }

        /* Swap the authored markup for the typed blocks. Runs at trigger
           time so the untouched code stays rendered (and visible to
           assistive tech) until the animation actually starts. */
        function beginTyping() {
            if (started) return;
            started = true;

            // reserve the exact height of the untouched rendering so the
            // surrounding layout never moves while characters appear
            codeBox.style.minHeight =
                Math.ceil(codeBox.getBoundingClientRect().height) + "px";

            // complete code for assistive technologies (the animated box is
            // aria-hidden, so partial code is never announced)
            const seeded = [];
            blocks.forEach(function (block, bi) {
                if (bi > 0) seeded.push("\n");
                block.chars.forEach(function (c) {
                    seeded.push(c.br ? "\n" : c.ch);
                });
            });
            const sr = document.createElement("div");
            sr.className = "visually-hidden";
            sr.setAttribute("data-code-sr", "");
            sr.textContent = seeded.join("");
            codeBox.parentNode.insertBefore(sr, codeBox);
            codeBox.setAttribute("aria-hidden", "true");

            sourceLines.forEach(function (p) { p.remove(); });
            blocks.forEach(function (block) {
                const p = document.createElement("p");
                p.className = block.cls + " code-typed";
                codeBox.appendChild(p);
                rendered.push(p);
            });

            startTimer = setTimeout(typeStep, 380);   // short initial pause
            lastProgressAt = Date.now();
            guard = setTimeout(guardCheck, 6000);
        }

        function finish() {
            if (!started || finished) return;
            finished = true;
            clearTimeout(timer);
            clearTimeout(startTimer);
            clearTimeout(guard);

            // render any remaining characters immediately
            for (; blockIndex < blocks.length; blockIndex++) {
                const chars = blocks[blockIndex].chars;
                for (; charIndex < chars.length; charIndex++) {
                    const item = chars[charIndex];
                    if (item.br) {
                        rendered[blockIndex].appendChild(document.createElement("br"));
                        segment = null;
                        segmentCls = "__none__";
                    } else {
                        appendChar(item);
                    }
                }
                charIndex = 0;
            }

            setTimeout(function () { cursor.remove(); }, 500);
        }

        // watchdog: only force the final state if the typing has genuinely
        // stalled (throttled timers, interrupted frame) and the window is on
        // screen - never to cut a healthy animation short
        function guardCheck() {
            if (finished) return;
            const stalled = Date.now() - lastProgressAt > 4000;
            const rect = codeBox.getBoundingClientRect();
            const onScreen = rect.bottom > 0 && rect.top < window.innerHeight;
            if (stalled && onScreen) {
                observer.disconnect();
                finish();
                return;
            }
            guard = setTimeout(guardCheck, 2000);
        }

        /* --- trigger at ~30% visibility, once --- */
        let observer = null;

        if (!("IntersectionObserver" in window)) {
            beginTyping();
            finish();
            return;
        }

        observer = new IntersectionObserver(function (entries, obs) {
            if (!entries[0].isIntersecting) return;
            obs.disconnect();
            beginTyping();
        }, { threshold: 0.3 });

        observer.observe(codeBox);

        // keep the final state safe if the page is hidden mid-animation
        onPageHide(function () {
            if (observer) observer.disconnect();
            finish();
        });
        pickMotion(function () {
            if (!prefersReduced()) return;
            if (observer) observer.disconnect();
            finish();          // no-op if typing never started
        });
    }

    /* ------------------------------------------------------------
       4. Testimonial reveal
       Quote mark first, then word-by-word text, then attribution.
       Words keep their inline flow so wrapping is untouched, and
       only opacity/transform change, so heights never jump.
       ------------------------------------------------------------ */
    function initTestimonials() {
        if (prefersReduced() || !("IntersectionObserver" in window)) return;

        const boxes = document.querySelectorAll(".quote-box");
        if (!boxes.length) return;

        const WORD_STEP = 42;
        const BASE = 220;
        const AUTH_BASE = 1150;   // waits for the auth card entrance

        Array.prototype.forEach.call(boxes, function (box, boxIndex) {
            const icon = box.querySelector("i");
            const text = box.querySelector("p");
            const author = box.querySelector("small");
            if (!text) return;

            const groupDelay = boxIndex * 150;
            const startDelay =
                (box.closest(".login-section") ? AUTH_BASE : BASE) + groupDelay;

            /* wrap words in fading spans; keep the text untouched */
            let wordCount = 0;
            const frag = document.createDocumentFragment();
            Array.prototype.forEach.call(text.childNodes, function (node) {
                if (node.nodeType !== 3) {
                    frag.appendChild(node);
                    return;
                }
                node.nodeValue.split(/(\s+)/).forEach(function (part) {
                    if (!part) return;
                    if (/^\s+$/.test(part)) {
                        frag.appendChild(document.createTextNode(part));
                        return;
                    }
                    const span = document.createElement("span");
                    span.className = "t-word";
                    span.textContent = part;
                    span.style.setProperty(
                        "--word-delay",
                        (wordCount * WORD_STEP) + "ms"
                    );
                    wordCount++;
                    frag.appendChild(span);
                });
            });
            text.textContent = "";
            text.appendChild(frag);

            if (icon) icon.classList.add("q-icon");
            if (author) {
                author.classList.add("q-author");
                author.style.setProperty(
                    "--author-delay",
                    (startDelay + wordCount * WORD_STEP + 160) + "ms"
                );
            }

            box.style.setProperty("--quote-base", startDelay + "ms");
            box.classList.add("quote-armed");

            const observer = new IntersectionObserver(function (entries, obs) {
                if (!entries[0].isIntersecting) return;
                obs.disconnect();
                clearTimeout(guard);
                box.classList.add("quote-in");
            }, { threshold: 0.25 });

            observer.observe(box);

            // safety net: text must never stay hidden, but only force the
            // reveal once the card really is on screen
            function guardCheck() {
                const rect = box.getBoundingClientRect();
                const onScreen = rect.width > 0 &&
                    rect.bottom > 0 && rect.top < window.innerHeight;
                if (onScreen) {
                    observer.disconnect();
                    box.classList.add("quote-in");
                    return;
                }
                guard = setTimeout(guardCheck, 2000);
            }
            let guard = setTimeout(guardCheck, 6000);

            // never leave text hidden if motion is turned off later
            pickMotion(function () {
                if (!prefersReduced()) return;
                observer.disconnect();
                clearTimeout(guard);
                box.classList.remove("quote-armed");
            });
            onPageHide(function () { observer.disconnect(); clearTimeout(guard); });
        });
    }

    /* ------------------------------------------------------------
       5. Statistic count-up
       Animates the existing presentation targets (10K+, 5K+, 2K+,
       1K+) from zero once per visit. These are display values, not
       live data. The final string is always written verbatim.
       ------------------------------------------------------------ */
    function initCounters() {
        const stats = document.querySelector(".hero-stats");
        if (!stats || prefersReduced() || !("IntersectionObserver" in window)) return;

        const targets = [];
        Array.prototype.forEach.call(stats.querySelectorAll("h4"), function (h4) {
            const match = /^\s*(\d+)([^\d]*)\s*$/.exec(h4.textContent);
            if (!match) return;
            targets.push({
                el: h4,
                value: parseInt(match[1], 10),
                suffix: match[2],
                finalText: h4.textContent
            });
        });
        if (!targets.length) return;

        const STAGGER = 150;
        const DURATION = 1700;
        const START_DELAY = 450;      // sync with the hero entrance

        let raf = 0;
        let observer = null;
        let started = false;
        let cancelled = false;

        function writeFinal() {
            targets.forEach(function (t) { t.el.textContent = t.finalText; });
        }

        function run(startTime) {
            function tick(now) {
                if (cancelled) return;
                const elapsed = now - startTime;
                let allDone = true;

                for (let i = 0; i < targets.length; i++) {
                    const t = targets[i];
                    const p = (elapsed - i * STAGGER) / DURATION;
                    if (p <= 0) {
                        t.el.textContent = "0" + t.suffix;
                        allDone = false;
                        continue;
                    }
                    if (p >= 1) {
                        t.el.textContent = t.finalText;
                        continue;
                    }
                    allDone = false;
                    const eased = 1 - Math.pow(1 - p, 3);   // ease-out cubic
                    t.el.textContent = Math.round(t.value * eased) + t.suffix;
                }

                if (allDone) {
                    writeFinal();
                    return;
                }
                raf = requestAnimationFrame(tick);
            }

            raf = requestAnimationFrame(tick);
        }

        function start() {
            if (started) return;
            started = true;
            if (observer) observer.disconnect();
            const first = performance.now() + START_DELAY;
            targets.forEach(function (t, i) {
                if (i === 0) t.el.textContent = "0" + t.suffix;
            });
            run(first);
        }

        function cancel() {
            cancelled = true;
            if (raf) cancelAnimationFrame(raf);
            raf = 0;
        }

        observer = new IntersectionObserver(function (entries, obs) {
            if (!entries[0].isIntersecting) return;
            obs.disconnect();
            start();
        }, { threshold: 0.3 });

        observer.observe(stats);

        pickMotion(function () {
            if (!prefersReduced()) return;
            cancel();
            writeFinal();
        });
        onPageHide(function () {
            cancel();
            if (observer) observer.disconnect();
        });
    }

    /* ------------------------------------------------------------
       6. Pause decorative CSS motion for off-screen sections
       ------------------------------------------------------------ */
    function initOffscreenPause() {
        if (prefersReduced() || !("IntersectionObserver" in window)) return;

        const sections = document.querySelectorAll(
            ".why-section, .cta-section, .login-section"
        );
        if (!sections.length) return;

        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                entry.target.classList.toggle(
                    "motion-paused",
                    !entry.isIntersecting
                );
            });
        }, { rootMargin: "160px 0px" });

        Array.prototype.forEach.call(sections, function (s) { observer.observe(s); });
        onPageHide(function () { observer.disconnect(); });
    }

    /* ------------------------------------------------------------
       Boot + live reduced-motion handling
       ------------------------------------------------------------ */
    function finishAllMotion() {
        document.querySelectorAll(".reveal")
            .forEach(function (el) { el.classList.add("is-visible"); });
        document.querySelectorAll(".quote-armed")
            .forEach(function (el) { el.classList.remove("quote-armed"); });
    }

    pickMotion(function () {
        if (prefersReduced()) finishAllMotion();
    });

    function boot() {
        initReveal();
        initBackdrops();
        initCodeTyping();
        initTestimonials();
        initCounters();
        initOffscreenPause();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})();
