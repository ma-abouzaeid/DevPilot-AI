// settings
// API_BASE is in config.js
const API_URL = new URL("/api/Auth/forgot-password", API_BASE).href

const RESEND_COOLDOWN_SECONDS = 45
const DEFAULT_RATE_LIMIT_SECONDS = 60   // default wait

// elements
const formView = document.querySelector("#form-view")
const successView = document.querySelector("#success-view")
const forgotForm = document.querySelector("#forgot-form")
const emailInput = document.querySelector("#email")
const sendBtn = document.querySelector("#send-btn")
const forgotAlert = document.querySelector("#forgot-alert")
const resendAlert = document.querySelector("#resend-alert")
const successEmail = document.querySelector("#success-email")
const resendBtn = document.querySelector("#resend-btn")
const resendLabel = document.querySelector("#resend-label")
const resendHint = document.querySelector("#resend-hint")

const sendBtnText = sendBtn.innerHTML

let sendTimer = null
let resendTimer = null

// helpers
function formatTime(totalSeconds) {
    const minutes = String(Math.floor(totalSeconds / 60)).padStart(2, "0")
    const seconds = String(totalSeconds % 60).padStart(2, "0")
    return minutes + ":" + seconds
}

function isValidEmail(email) {
    return /^\S+@\S+\.\S+$/.test(email)
}

// alert
function clearAlert(box) {
    box.className = "alert d-none"
    box.textContent = ""
}

// type: danger / warning / success
function showAlert(box, type, message) {
    box.className = "alert alert-" + type
    box.textContent = message
}

// field error
function setFieldError(hasError) {
    emailInput.closest(".form-group").classList.toggle("has-error", hasError)
    emailInput.setAttribute("aria-invalid", hasError)
}

// loading
function setLoading(isLoading) {
    sendBtn.disabled = isLoading
    sendBtn.setAttribute("aria-busy", isLoading)
    sendBtn.innerHTML = isLoading
        ? '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Sending...'
        : sendBtnText
}

// wait timers
// disable the button until the timer ends
function startWait(seconds, onTick, onDone) {
    let left = seconds

    const timer = setInterval(function () {
        left--
        if (left <= 0) {
            clearInterval(timer)
            onDone()
            return
        }
        onTick(left)
    }, 1000)

    onTick(left)
    return timer
}

function lockSendButton(seconds) {
    clearInterval(sendTimer)
    sendBtn.disabled = true

    sendTimer = startWait(seconds, function (left) {
        sendBtn.textContent = "Try again in " + formatTime(left)
    }, function () {
        sendBtn.disabled = false
        sendBtn.innerHTML = sendBtnText
        clearAlert(forgotAlert)
    })
}

function startResendCooldown(seconds) {
    clearInterval(resendTimer)
    resendBtn.disabled = true

    resendTimer = startWait(seconds, function (left) {
        resendHint.textContent = "You can resend in " + formatTime(left)
    }, function () {
        resendBtn.disabled = false
        resendHint.textContent = ""
    })
}

// api call
// returns the result type
// api call
// returns the result type
async function requestResetLink(email) {
    const result = await DevPilotApi.request(API_URL, {
        method: "POST",
        body: JSON.stringify({ email: email })
    });

    // 404 counts as success too, so we don't reveal which emails exist
    if ((result.ok && result.success) || result.status === 404) {
        return { type: "success" };
    }

    if (result.status === 429 || result.errorCode === "RATE_LIMIT_EXCEEDED") {
        return { type: "rate", seconds: DEFAULT_RATE_LIMIT_SECONDS, message: result.message };
    }

    if (result.status === 400 || result.status === 422) {
        const fieldError = result.errors && result.errors.length > 0 ? result.errors[0].message : null;
        return { type: "invalid", message: fieldError || result.message };
    }

    return { type: "error", message: DevPilotApi.getDisplayMessage(result) };
}

function errorMessage(result) {
    return result.message || "Something went wrong on our side. Please try again later.";
}

// success view
function showSuccessView(email) {
    // same message either way
    successEmail.textContent = email;
    formView.classList.add("d-none");
    successView.classList.remove("d-none");
    successView.focus();
    startResendCooldown(RESEND_COOLDOWN_SECONDS);
}

// submit
forgotForm.addEventListener("submit", async function (e) {
    e.preventDefault();
    clearAlert(forgotAlert);

    const email = emailInput.value.trim();

    if (!isValidEmail(email)) {
        setFieldError(true);
        emailInput.focus();
        return;
    }

    setFieldError(false);
    setLoading(true);

    const result = await requestResetLink(email);

    if (result.type === "success") {
        showSuccessView(email);
        setLoading(false);
        return;
    }

    if (result.type === "rate") {
        showAlert(forgotAlert, "warning", result.message || "Too many requests. Please wait a moment before trying again.");
        setLoading(false);
        lockSendButton(result.seconds);
        return;
    }

    if (result.type === "invalid") {
        setFieldError(true);
        emailInput.focus();
    } else {
        showAlert(forgotAlert, "danger", errorMessage(result));
    }

    setLoading(false);
});

emailInput.addEventListener("input", function () {
    setFieldError(false);
});

// resend
resendBtn.addEventListener("click", async function () {
    clearAlert(resendAlert);
    resendBtn.disabled = true;
    resendLabel.textContent = "Sending...";

    const result = await requestResetLink(emailInput.value.trim());

    resendLabel.textContent = "Resend Email";

    if (result.type === "success") {
        showAlert(resendAlert, "success", "If the account exists, we sent the email again.");
        startResendCooldown(RESEND_COOLDOWN_SECONDS);
        return;
    }

    if (result.type === "rate") {
        showAlert(resendAlert, "warning", result.message || "Too many requests. Please wait a moment before asking again.");
        startResendCooldown(result.seconds);
        return;
    }

    showAlert(resendAlert, "danger", result.type === "invalid" ? "Please go back and check your email address." : errorMessage(result));
    resendBtn.disabled = false;
});