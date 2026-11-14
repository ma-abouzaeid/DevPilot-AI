// footer — site + legal links
const footerBox = document.querySelector("#footer");

if (footerBox) {
    footerBox.innerHTML = `
    <footer class="footer-dark">
        <div class="container d-flex flex-wrap justify-content-between align-items-center gap-3">
            <a href="index.html" aria-label="DevPilot AI home">
                <img src="images/logo-light.svg" alt="DevPilot AI" class="logo-img logo-svg">
            </a>
            <ul class="footer-links">
                <li class="d-inline-block"><a href="support.html">Support</a></li>
                <li class="d-inline-block"><a href="community.html">Community</a></li>
                <li class="d-inline-block"><a href="privacy.html">Privacy</a></li>
                <li class="d-inline-block"><a href="terms.html">Terms</a></li>
            </ul>
            <p class="mb-0 text-gray">&copy; 2026 DevPilot AI. All rights reserved.</p>
        </div>
    </footer>`;
}

// Guided tour (floating Tour button) — load once on every page that includes footer.js
(function loadTourScript() {
    if (window.DevPilotTour) return;
    if (document.querySelector('script[src*="tour.js"]')) return;
    const script = document.createElement("script");
    script.src = "js/tour.js";
    script.defer = true;
    document.body.appendChild(script);
})();
