(function () {
    "use strict";

    function formatStat(value) {
        const n = Number(value) || 0;
        if (n >= 10000) return Math.floor(n / 1000) + "K+";
        if (n >= 1000) return (Math.round(n / 100) / 10) + "K+";
        return String(n);
    }

    async function load() {
        if (typeof API_BASE === "undefined" || !window.DevPilotApi) return;
        const nodes = {
            developers: document.getElementById("stat-developers"),
            solved: document.getElementById("stat-problems-solved"),
            articles: document.getElementById("stat-articles"),
            challenges: document.getElementById("stat-challenges")
        };
        if (!nodes.developers) return;

        try {
            const url = new URL("/api/Problems/stats", API_BASE).href;
            const res = await window.DevPilotApi.request(url, { skipAuth: true });
            if (!res.ok || !res.data) return;
            const d = res.data;
            nodes.developers.textContent = formatStat(d.developers ?? d.Developers);
            nodes.solved.textContent = formatStat(d.problemsSolved ?? d.ProblemsSolved);
            nodes.articles.textContent = formatStat(d.knowledgeArticles ?? d.KnowledgeArticles);
            nodes.challenges.textContent = formatStat(d.codingChallenges ?? d.CodingChallenges);
        } catch (e) {
            // Keep placeholder values if the public stats call fails.
        }
    }

    load();
})();
