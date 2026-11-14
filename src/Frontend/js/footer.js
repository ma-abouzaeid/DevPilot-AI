// footer — legal links only (nav lives in the header)
const footerBox = document.querySelector("#footer");

if (footerBox) {
    footerBox.innerHTML = `
    <footer class="footer-dark">
        <div class="container d-flex flex-wrap justify-content-between align-items-center gap-3">
            <a href="index.html" aria-label="DevPilot AI home">
                <img src="images/logo-light.svg" alt="DevPilot AI" class="logo-img logo-svg">
            </a>
            <ul class="footer-links">
                <li class="d-inline-block"><a href="privacy.html">Privacy</a></li>
                <li class="d-inline-block"><a href="terms.html">Terms</a></li>
            </ul>
            <p class="mb-0 text-gray">&copy; 2026 DevPilot AI. All rights reserved.</p>
        </div>
    </footer>`;
}
