// settings
// API_BASE is in config.js
const API_URL = new URL("/api/Auth/register", API_BASE).href
const VERIFY_PAGE = "verify-email.html"
const LOGIN_PAGE = "login.html"

// error codes (check names with backend)
const ERROR_CODES = {
    emailExists: "EMAIL_ALREADY_EXISTS"
}

// elements
const registerForm = document.querySelector("#register-form")
const nameInput = document.querySelector("#name")
const emailInput = document.querySelector("#email")
const passwordInput = document.querySelector("#password")
const confirmInput = document.querySelector("#confirm")
const registerBtn = document.querySelector("#register-btn")
const registerAlert = document.querySelector("#register-alert")
const turnstileGroup = document.querySelector("#turnstile-group")
const turnstileError = document.querySelector("#turnstile-error")

const registerBtnText = registerBtn.innerHTML

// turnstile state & callbacks
let turnstileToken = ""

window.onTurnstileSuccess = function (token) {
    turnstileToken = token
    setTurnstileError(false)
}

window.onTurnstileExpired = function () {
    turnstileToken = ""
}

window.onTurnstileError = function () {
    turnstileToken = ""
}

function getTurnstileToken() {
    return turnstileToken ||
        (window.turnstile && typeof window.turnstile.getResponse === "function" ? window.turnstile.getResponse() : "") ||
        document.querySelector('[name="cf-turnstile-response"]')?.value ||
        ""
}

function setTurnstileError(hasError, message) {
    if (!turnstileGroup) return
    if (message && turnstileError) {
        turnstileError.textContent = message
    }
    turnstileGroup.classList.toggle("has-error", hasError)
}

function resetTurnstile() {
    turnstileToken = ""
    if (window.turnstile && typeof window.turnstile.reset === "function") {
        try {
            window.turnstile.reset()
        } catch (e) {
            console.warn("Could not reset Turnstile:", e)
        }
    }
}

// backend field names
const fieldsByName = {
    name: nameInput,
    email: emailInput,
    password: passwordInput
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
// check rules with backend
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
    registerAlert.className = "alert d-none"
    registerAlert.textContent = ""
}

// type: danger / warning
function showAlert(type, message, extraElement) {
    registerAlert.className = "alert alert-" + type
    registerAlert.textContent = message

    if (extraElement) {
        registerAlert.append(" ", extraElement)
    }
}

// loading
function setLoading(isLoading) {
    registerBtn.disabled = isLoading
    registerBtn.setAttribute("aria-busy", isLoading)

    if (isLoading) {
        registerBtn.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Creating account...'
    } else {
        registerBtn.innerHTML = registerBtnText
    }
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

function clearFieldErrors() {
    [nameInput, emailInput, passwordInput, confirmInput].forEach(function (input) {
        setFieldError(input, false)
    })
    setTurnstileError(false)
}

// validation
function isValidEmail(email) {
    return /^\S+@\S+\.\S+$/.test(email)
}

function validateForm() {
    const checks = [
        { input: nameInput, valid: nameInput.value.trim().length >= 2, message: "Please enter your name." },
        { input: emailInput, valid: isValidEmail(emailInput.value.trim()), message: "Please enter a valid email address." },
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

    const token = getTurnstileToken()
    const turnstileValid = token !== ""
    setTurnstileError(!turnstileValid, "Please complete the security check.")

    if (!turnstileValid && !firstWrong) {
        firstWrong = turnstileGroup
    }

    // focus the first wrong field
    if (firstWrong && typeof firstWrong.focus === "function") {
        firstWrong.focus()
    }

    return firstWrong === null
}

// api errors
// backend validation errors
function showBackendFieldErrors(data) {
    if (!data.errors || typeof data.errors !== "object") {
        return false
    }

    let shown = false

    Object.keys(data.errors).forEach(function (key) {
        // lowercase the key
        const field = fieldsByName[key.replace("$.", "").toLowerCase()]
        const messages = data.errors[key]

        if (field) {
            setFieldError(field, true, Array.isArray(messages) ? messages[0] : String(messages))
            shown = true
        }
    })

    return shown
}

function handleRegisterError(result) {
    const status = result.status;
    const errorCode = result.errorCode;

    // duplicate email
    if (status === 409 || errorCode === ERROR_CODES.emailExists) {
        const link = document.createElement("a");
        link.href = LOGIN_PAGE;
        link.className = "alert-link";
        link.textContent = "Log in instead";

        setFieldError(emailInput, true, "An account with this email already exists.");
        showAlert("warning", "This email is already registered.", link);
        emailInput.focus();
        return;
    }

    // bot check failed
    if (errorCode === "BOT_CHECK_FAILED") {
        setTurnstileError(true, "Security verification failed. Please try again.");
        showAlert("danger", "Security check failed. Please verify that you are human.");
        return;
    }

    // backend validation errors
    if (status === 400) {
        const rendered = DevPilotApi.renderFieldErrors(fieldsByName, result.errors);
        if (!rendered || (result.message && (!result.errors || result.errors.length === 0))) {
            showAlert("danger", result.message || "Please check the information you entered and try again.");
        }
        return;
    }

    if (status === 429 || errorCode === "RATE_LIMIT_EXCEEDED") {
        showAlert("warning", "Too many attempts. Please wait a moment and try again.");
        return;
    }

    // anything else
    showAlert("danger", DevPilotApi.getDisplayMessage(result));
}

// submit
registerForm.addEventListener("submit", async function (e) {
    e.preventDefault();
    clearAlert();
    clearFieldErrors();

    if (!validateForm()) {
        return;
    }

    setLoading(true);

    const token = getTurnstileToken();
    const result = await DevPilotApi.request(API_URL, {
        method: "POST",
        body: JSON.stringify({
            name: nameInput.value.trim(),
            email: emailInput.value.trim(),
            password: passwordInput.value,
            turnstileToken: token
        })
    });

    if (result.ok && result.success) {
        // go to the verify page
        window.location.href = VERIFY_PAGE + "?email=" + encodeURIComponent(emailInput.value.trim());
        return;
    }

    resetTurnstile();
    handleRegisterError(result);
    setLoading(false);
});