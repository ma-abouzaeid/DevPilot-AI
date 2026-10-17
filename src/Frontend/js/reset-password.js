// settings
// API_BASE is in config.js
const API_URL = new URL("/api/Auth/reset-password", API_BASE).href

// error codes
const ERROR_CODES = {
    invalidToken: "INVALID_TOKEN",
    expiredToken: "TOKEN_EXPIRED"
}

// token from the link
// link has the token, email is optional
const params = new URLSearchParams(window.location.search)
const email = params.get("email")
// browser turns + into a space, put it back
const token = (params.get("token") || "").replace(/ /g, "+")

// elements
const formView = document.querySelector("#form-view")
const invalidView = document.querySelector("#invalid-view")
const successView = document.querySelector("#success-view")
const resetForm = document.querySelector("#reset-form")
const passwordInput = document.querySelector("#password")
const confirmInput = document.querySelector("#confirm")
const resetBtn = document.querySelector("#reset-btn")
const resetAlert = document.querySelector("#reset-alert")

const resetBtnText = resetBtn.innerHTML

// views
function showView(view) {
    [formView, invalidView, successView].forEach(function (item) {
        item.classList.add("d-none")
    })
    view.classList.remove("d-none")
    view.focus()
}

// show / hide password
document.querySelectorAll(".toggle-password").forEach(function (btn) {
    btn.addEventListener("click", function () {
        const input = document.querySelector("#" + btn.dataset.target)
        const icon = btn.querySelector("i")

        if (input.type === "password") {
            input.type = "text"
            icon.className = "fa-regular fa-eye-slash"
            btn.setAttribute("aria-label", "Hide password")
        } else {
            input.type = "password"
            icon.className = "fa-regular fa-eye"
            btn.setAttribute("aria-label", "Show password")
        }
    })
})

// password rules
const passwordRules = {
    length: function (v) { return v.length >= 8 },
    upper: function (v) { return /[A-Z]/.test(v) },
    lower: function (v) { return /[a-z]/.test(v) },
    number: function (v) { return /[0-9]/.test(v) },
    special: function (v) { return /[^A-Za-z0-9]/.test(v) }
}

function updateRules() {
    let allMet = true

    document.querySelectorAll("[data-rule]").forEach(function (item) {
        const met = passwordRules[item.dataset.rule](passwordInput.value)

        item.classList.toggle("rule-ok", met)
        item.querySelector("i").className = met ? "fa-solid fa-circle-check" : "fa-regular fa-circle"
        // hidden text for screen readers
        item.querySelector(".visually-hidden").textContent = met ? " (met)" : " (not met)"

        if (!met) {
            allMet = false
        }
    })

    return allMet
}

passwordInput.addEventListener("input", updateRules)

// alert
function clearAlert() {
    resetAlert.className = "alert d-none"
    resetAlert.textContent = ""
}

// type: danger / warning
function showAlert(type, message) {
    resetAlert.className = "alert alert-" + type
    resetAlert.textContent = message
}

// loading
function setLoading(isLoading) {
    resetBtn.disabled = isLoading
    resetBtn.setAttribute("aria-busy", isLoading)
    resetBtn.innerHTML = isLoading
        ? '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Resetting...'
        : resetBtnText
}

// field errors
// custom message is optional
function setFieldError(input, hasError, message) {
    const group = input.closest(".form-group")

    if (message) {
        group.querySelector(".error-text").textContent = message
    }
    group.classList.toggle("has-error", hasError)
    input.setAttribute("aria-invalid", hasError)
}

// validation
function validateForm() {
    const checks = [
        { input: passwordInput, valid: updateRules(), message: "Password does not meet the requirements." },
        { input: confirmInput, valid: confirmInput.value === passwordInput.value && confirmInput.value !== "", message: "Passwords do not match." }
    ]

    let firstWrong = null

    checks.forEach(function (check) {
        setFieldError(check.input, !check.valid, check.message)

        if (!check.valid && !firstWrong) {
            firstWrong = check.input
        }
    })

    // focus the first wrong field
    if (firstWrong) {
        firstWrong.focus()
    }

    return firstWrong === null
}

// api errors
// backend validation errors
function showBackendPasswordError(data) {
    if (!data.errors || typeof data.errors !== "object") {
        return false
    }

    let shown = false

    Object.keys(data.errors).forEach(function (key) {
        const name = key.replace("$.", "").toLowerCase()
        const messages = data.errors[key]

        if (name === "newpassword" || name === "password") {
            setFieldError(passwordInput, true, Array.isArray(messages) ? messages[0] : String(messages))
            passwordInput.focus()
            shown = true
        }
    })

    return shown
}

function handleResetError(result) {
    const status = result.status;
    const errorCode = result.errorCode;

    // weak password or model validation
    if (status === 400 && result.errors && result.errors.length > 0) {
        const passwordError = result.errors.find(function (e) {
            return e.field === "newpassword" || e.field === "password";
        });
        if (passwordError) {
            setFieldError(passwordInput, true, passwordError.message);
            passwordInput.focus();
            return;
        }
    }

    // bad or expired link
    if (status === 400 || status === 401 || status === 404 || status === 410 ||
        errorCode === ERROR_CODES.invalidToken || errorCode === ERROR_CODES.expiredToken) {
        showView(invalidView);
        return;
    }

    if (status === 429 || errorCode === "RATE_LIMIT_EXCEEDED") {
        showAlert("warning", "Too many attempts. Please wait a moment and try again.");
        return;
    }

    showAlert("danger", DevPilotApi.getDisplayMessage(result));
}

// request body
// field names from swagger
function buildRequestBody() {
    const body = { token: token, newPassword: passwordInput.value };

    // optional
    if (email) {
        body.email = email;
    }
    return body;
}

// submit
resetForm.addEventListener("submit", async function (e) {
    e.preventDefault();
    clearAlert();

    if (!validateForm()) {
        return;
    }

    setLoading(true);

    const result = await DevPilotApi.request(API_URL, {
        method: "POST",
        body: JSON.stringify(buildRequestBody())
    });

    if (result.ok && result.success) {
        passwordInput.value = "";
        confirmInput.value = "";
        showView(successView);
        setLoading(false);
        return;
    }

    handleResetError(result);
    setLoading(false);
});

// start
// no token, show the invalid view
if (!token) {
    showView(invalidView)
}