// footer
// usage: <div id="footer" data-variant="simple"></div> for the short one
const footerBox = document.querySelector("#footer")

if (footerBox) {
    // short footer: legal links only
    let links = `
                <li class="d-inline-block"><a href="privacy.html">Privacy</a></li>
                <li class="d-inline-block"><a href="terms.html">Terms</a></li>`

    if (footerBox.dataset.variant !== "simple") {
        links = `
                <li class="d-inline-block"><a href="index.html">Home</a></li>
                <li class="d-inline-block"><a href="articles.html">Articles</a></li>
                <li class="d-inline-block"><a href="about.html">About</a></li>
                <li class="d-inline-block"><a href="community.html">Community</a></li>` + links
    }

    footerBox.innerHTML = `
    <footer class="footer-dark">
        <div class="container d-flex flex-wrap justify-content-between align-items-center gap-3">
            <a href="index.html">
                <img src="images/logo.png" alt="DevPilot AI" class="logo-img">
            </a>
            <ul class="footer-links">${links}
            </ul>
            <p class="mb-0 text-gray">&copy; 2026 DevPilot AI. All rights reserved.</p>
        </div>
    </footer>`
}