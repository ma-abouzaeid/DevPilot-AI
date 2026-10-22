// api
const API_URL = new URL("/api/Auth/login", API_BASE).href
const DASHBOARD_PAGE = "dashboard.html"
const VERIFY_PAGE = "verify-email.html"

const ERROR_CODES = {
    invalidCredentials: "INVALID_CREDENTIALS",
    emailNotVerified: "EMAIL_NOT_VERIFIED"
}

// elements
const loginForm = document.querySelector("#login-form")
const emailInput = document.querySelector("#email")
const passwordInput = document.querySelector("#password")
const rememberInput = document.querySelector("#remember")
const togglePasswordBtn = document.querySelector("#toggle-password")
const loginBtn = document.querySelector("#login-btn")
const loginAlert = document.querySelector("#login-alert")
const turnstileGroup = document.querySelector("#turnstile-group")
const turnstileError = document.querySelector("#turnstile-error")

const loginBtnText = loginBtn.innerHTML

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

// show / hide password
togglePasswordBtn.addEventListener("click", function () {
    const icon = togglePasswordBtn.querySelector("i")

    if (passwordInput.type === "password") {
        passwordInput.type = "text"
        icon.className = "fa-regular fa-eye-slash"
        togglePasswordBtn.setAttribute("aria-label", "Hide password")
    } else {
        passwordInput.type = "password"
        icon.className = "fa-regular fa-eye"
        togglePasswordBtn.setAttribute("aria-label", "Show password")
    }
})

// alert
function clearAlert() {
    loginAlert.className = "alert d-none"
    loginAlert.textContent = ""
}

function showAlert(type, message, extraElement) {
    loginAlert.className = "alert alert-" + type
    loginAlert.textContent = message

    if (extraElement) {
        loginAlert.append(" ", extraElement)
    }
}

// loading
function setLoading(isLoading) {
    loginBtn.disabled = isLoading
    loginBtn.setAttribute("aria-busy", isLoading)

    if (isLoading) {
        loginBtn.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Logging in...'
    } else {
        loginBtn.innerHTML = loginBtnText
    }
}

// validation
function isValidEmail(email) {
    return /^\S+@\S+\.\S+$/.test(email)
}

function setError(input, hasError) {
    input.closest(".form-group").classList.toggle("has-error", hasError)
    input.setAttribute("aria-invalid", hasError)
}

function validateForm() {
    const emailValid = isValidEmail(emailInput.value.trim())
    const passwordValid = passwordInput.value !== ""
    const token = getTurnstileToken()
    const turnstileValid = token !== ""

    setError(emailInput, !emailValid)
    setError(passwordInput, !passwordValid)
    setTurnstileError(!turnstileValid, "Please complete the security check.")

    if (!emailValid) {
        emailInput.focus()
    } else if (!passwordValid) {
        passwordInput.focus()
    }

    return emailValid && passwordValid && turnstileValid
}

// next page after login
function getNextPage() {
    const redirect = new URLSearchParams(window.location.search).get("redirect")

    // only page names, no external links
    if (redirect && /^[a-z0-9-]+\.html$/i.test(redirect)) {
        return redirect
    }
    return DASHBOARD_PAGE
}

// api errors
function handleLoginError(result) {
    const status = result.status;
    const code = result.errorCode;
    const serverMessage = typeof result.message === "string" ? result.message : "";

    const isUnverified = code === ERROR_CODES.emailNotVerified || /not verified/i.test(serverMessage);

    if (isUnverified) {
        const link = document.createElement("a");
        link.href = VERIFY_PAGE + "?email=" + encodeURIComponent(emailInput.value.trim()) + "&from=login";
        link.className = "alert-link";
        link.textContent = "Verify your email or resend the code";

        showAlert("warning", "Your email is not verified yet.", link);
        return;
    }

    if (code === "BOT_CHECK_FAILED") {
        setTurnstileError(true, "Security verification failed. Please try again.");
        showAlert("danger", "Security check failed. Please verify that you are human.");
        return;
    }

    if (status === 401 || code === ERROR_CODES.invalidCredentials) {
        showAlert("danger", "Invalid email or password.");
        return;
    }

    if (status === 429 || code === "RATE_LIMIT_EXCEEDED") {
        showAlert("warning", "Too many attempts. Please wait a moment and try again.");
        return;
    }

    if (status === 400 && result.errors && result.errors.length > 0) {
        showAlert("danger", result.errors[0].message || "Invalid login request.");
        return;
    }

    showAlert("danger", DevPilotApi.getDisplayMessage(result));
}

// submit
loginForm.addEventListener("submit", async function (e) {
    e.preventDefault();
    clearAlert();

    if (!validateForm()) {
        return;
    }

    setLoading(true);

    const token = getTurnstileToken();
    const payload = {
        email: emailInput.value.trim(),
        password: passwordInput.value,
        rememberMe: rememberInput.checked
    };

    if (token) {
        payload.turnstileToken = token;
    }

    const result = await DevPilotApi.request(API_URL, {
        method: "POST",
        body: JSON.stringify(payload)
    });

    if (result.ok && result.success) {
        if (result.data && result.data.accessToken) {
            DevPilotApi.setTokens(result.data.accessToken, result.data.refreshToken);
        }
        window.location.href = getNextPage();
        return;
    }

    resetTurnstile();
    handleLoginError(result);
    setLoading(false);
});