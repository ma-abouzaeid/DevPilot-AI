// load components
const includeBoxes = document.querySelectorAll("[data-include]")

includeBoxes.forEach(function (box) {
    fetch(box.dataset.include)
        .then(function (res) {
            if (!res.ok) {
                throw new Error(res.status + " " + res.statusText)
            }
            return res.text()
        })
        .then(function (html) {
            box.innerHTML = html
            setActiveLink(box)
        })
        .catch(function (err) {
            console.error("Could not load " + box.dataset.include, err)
        })
})

// active link
function setActiveLink(box) {
    const page = box.dataset.active
    if (!page) {
        return
    }

    const link = box.querySelector('[data-page="' + page + '"]')
    if (link) {
        link.classList.add("active")
        link.setAttribute("aria-current", "page")
    }
}