// settings
// API_BASE is in config.js
const VERIFY_URL = new URL("/api/Auth/verify-email", API_BASE).href
const RESEND_URL = new URL("/api/Auth/resend-verification", API_BASE).href

// page after verify
const AFTER_VERIFY_PAGE = "login.html"
const LOGIN_PAGE = "login.html"

const CODE_LENGTH = 6
const CODE_LIFETIME_SECONDS = 300      // code lifetime
const RESEND_COOLDOWN_SECONDS = 45     // resend wait
const RATE_LIMIT_COOLDOWN_SECONDS = 60 // wait after 429
const MAX_FAILED_ATTEMPTS = 5          // before the lock
const LOCK_SECONDS = 30

// error codes
const ERROR_CODES = {
    invalidCode: "INVALID_CODE",
    expiredCode: "CODE_EXPIRED",
    alreadyVerified: "EMAIL_ALREADY_VERIFIED"
}

// elements
const verifyView = document.querySelector("#verify-view")
const successView = document.querySelector("#success-view")
const verifyForm = document.querySelector("#verify-form")
const verifyAlert = document.querySelector("#verify-alert")
const otpInputs = document.querySelectorAll(".otp-input")
const otpWrap = document.querySelector(".otp-wrap")
const otpError = document.querySelector("#otp-error")
const verifyBtn = document.querySelector("#verify-btn")
const resendBtn = document.querySelector("#resend-btn")
const resendLabel = document.querySelector("#resend-label")
const resendHint = document.querySelector("#resend-hint")
const timerLabel = document.querySelector("#timer-label")
const countdownText = document.querySelector("#countdown")
const timerBox = document.querySelector("#timer-box")

const verifyBtnText = verifyBtn.innerHTML

// email comes from the url
const email = new URLSearchParams(window.location.search).get("email")

// opened from the login page
const fromLogin = new URLSearchParams(window.location.search).get("from") === "login"

// state
let isLoading = false
let isExpired = false
let isLocked = false
let failedAttempts = 0
let cooldownTimer = null
let expiryTimer = null

// helpers
function formatTime(totalSeconds) {
    const minutes = String(Math.floor(totalSeconds / 60)).padStart(2, "0")
    const seconds = String(totalSeconds % 60).padStart(2, "0")
    return minutes + ":" + seconds
}

function updateVerifyButton() {
    verifyBtn.disabled = isLoading || isExpired || isLocked
}

function setLoading(loading) {
    isLoading = loading
    verifyBtn.setAttribute("aria-busy", loading)
    verifyBtn.innerHTML = loading
        ? '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Verifying...'
        : verifyBtnText
    updateVerifyButton()
}

// alert
function clearAlert() {
    verifyAlert.className = "alert d-none"
    verifyAlert.textContent = ""
}

// type: danger / warning / success
function showAlert(type, message, extraElement) {
    verifyAlert.className = "alert alert-" + type
    verifyAlert.textContent = message

    if (extraElement) {
        verifyAlert.append(" ", extraElement)
    }
}

// otp inputs
function setOtpError(hasError, message) {
    otpWrap.classList.toggle("has-error", hasError)
    otpInputs.forEach(function (input) {
        input.setAttribute("aria-invalid", hasError)
    })

    if (message) {
        otpError.textContent = message
    }
}

function getCode() {
    return Array.from(otpInputs).map(function (input) {
        return input.value
    }).join("")
}

function clearOtpInputs() {
    otpInputs.forEach(function (input) {
        input.value = ""
    })
    otpInputs[0].focus()
}

// fill the boxes (paste / autofill)
function fillDigits(digits) {
    digits.split("").slice(0, CODE_LENGTH).forEach(function (digit, index) {
        otpInputs[index].value = digit
    })
    otpInputs[Math.min(digits.length, CODE_LENGTH - 1)].focus()
}

otpInputs.forEach(function (input, index) {
    input.addEventListener("input", function () {
        setOtpError(false)
        const digits = input.value.replace(/\D/g, "")

        // autofill
        if (digits.length > 1) {
            fillDigits(digits)
            return
        }

        input.value = digits
        if (digits && otpInputs[index + 1]) {
            otpInputs[index + 1].focus()
        }
    })

    input.addEventListener("keydown", function (e) {
        if (e.key === "Backspace" && !input.value && otpInputs[index - 1]) {
            otpInputs[index - 1].focus()
        }
        if (e.key === "ArrowLeft" && otpInputs[index - 1]) {
            otpInputs[index - 1].focus()
        }
        if (e.key === "ArrowRight" && otpInputs[index + 1]) {
            otpInputs[index + 1].focus()
        }
    })

    input.addEventListener("paste", function (e) {
        e.preventDefault()
        setOtpError(false)
        fillDigits((e.clipboardData.getData("text") || "").replace(/\D/g, ""))
    })

    input.addEventListener("focus", function () {
        input.select()
    })
})

// expiry timer
// saved so a refresh keeps the timer
const expiryKey = "verifyExpiry:" + email

function readExpiry() {
    try {
        return Number(sessionStorage.getItem(expiryKey))
    } catch (error) {
        return 0
    }
}

function saveExpiry(timestamp) {
    try {
        sessionStorage.setItem(expiryKey, String(timestamp))
    } catch (error) {
        // ignore
    }
}

function startExpiryCountdown(freshStart) {
    let expiresAt = readExpiry()

    if (freshStart || !expiresAt) {
        expiresAt = Date.now() + CODE_LIFETIME_SECONDS * 1000
        saveExpiry(expiresAt)
    }

    clearInterval(expiryTimer)

    function tick() {
        const left = Math.max(0, Math.round((expiresAt - Date.now()) / 1000))
        countdownText.textContent = formatTime(left)

        if (left === 0) {
            clearInterval(expiryTimer)
            markExpired()
        }
    }

    tick()
    expiryTimer = setInterval(tick, 1000)
}

function markExpired() {
    isExpired = true
    timerLabel.textContent = "Code expired"
    countdownText.textContent = ""
    updateVerifyButton()
    showAlert("warning", "Your code has expired. Please request a new one.")
}

// resend cooldown
function startCooldown(seconds) {
    clearInterval(cooldownTimer)
    let left = seconds
    resendBtn.disabled = true

    function tick() {
        if (left <= 0) {
            clearInterval(cooldownTimer)
            resendBtn.disabled = false
            resendHint.textContent = ""
            return
        }
        resendHint.textContent = "You can resend in " + formatTime(left)
        left--
    }

    tick()
    cooldownTimer = setInterval(tick, 1000)
}

// lock after wrong codes
function lockVerify() {
    isLocked = true
    updateVerifyButton()
    showAlert("warning", "Too many attempts. Please wait " + LOCK_SECONDS + " seconds and try again.")

    setTimeout(function () {
        isLocked = false
        failedAttempts = 0
        updateVerifyButton()
        clearAlert()
    }, LOCK_SECONDS * 1000)
}

// success
function showSuccess() {
    clearInterval(expiryTimer)
    clearInterval(cooldownTimer)

    try {
        sessionStorage.removeItem(expiryKey)
    } catch (error) {
        // ignore
    }

    document.querySelector("#continue-btn").href = AFTER_VERIFY_PAGE
    verifyView.classList.add("d-none")
    successView.classList.remove("d-none")
    successView.focus()
}

// verify errors
function handleVerifyError(result) {
    const status = result.status;
    const code = result.errorCode;

    // already verified
    if (code === ERROR_CODES.alreadyVerified) {
        showSuccess();
        return;
    }

    if (status === 410 || code === ERROR_CODES.expiredCode) {
        markExpired();
        return;
    }

    if (status === 429 || code === "RATE_LIMIT_EXCEEDED") {
        showAlert("warning", "Too many attempts. Please wait a moment and try again.");
        return;
    }

    if (code === "MAX_ATTEMPTS_EXCEEDED") {
        lockVerify();
        return;
    }

    // wrong code
    if (status === 400 || status === 401 || status === 404 || status === 422 || code === ERROR_CODES.invalidCode) {
        failedAttempts++;
        setOtpError(true, "The code is incorrect. Please check it and try again.");
        clearOtpInputs();

        if (failedAttempts >= MAX_FAILED_ATTEMPTS) {
            lockVerify();
        }
        return;
    }

    // anything else
    showAlert("danger", DevPilotApi.getDisplayMessage(result));
}

// submit
verifyForm.addEventListener("submit", async function (e) {
    e.preventDefault();
    clearAlert();

    const code = getCode();

    if (code.length !== CODE_LENGTH) {
        setOtpError(true, "Please enter all 6 digits.");
        otpInputs[code.length].focus();
        return;
    }

    setOtpError(false);
    setLoading(true);

    const result = await DevPilotApi.request(VERIFY_URL, {
        method: "POST",
        body: JSON.stringify({ email: email, code: code })
    });

    if (result.ok && result.success) {
        showSuccess();
        return;
    }

    handleVerifyError(result);
    setLoading(false);
});

// resend
resendBtn.addEventListener("click", async function () {
    clearAlert();
    setOtpError(false);
    resendBtn.disabled = true;
    resendLabel.textContent = "Sending...";

    try {
        const result = await DevPilotApi.request(RESEND_URL, {
            method: "POST",
            body: JSON.stringify({ email: email })
        });

        if (result.ok && result.success) {
            isExpired = false;
            failedAttempts = 0;
            timerLabel.textContent = "Code expires in";
            timerBox.classList.remove("d-none");
            updateVerifyButton();
            clearOtpInputs();
            startExpiryCountdown(true);
            startCooldown(RESEND_COOLDOWN_SECONDS);
            showAlert("success", result.message || "A new code has been sent. Please check your email.");
            return;
        }

        if (result.status === 429 || result.errorCode === "RATE_LIMIT_EXCEEDED" || result.errorCode === "RESEND_COOLDOWN") {
            showAlert("warning", result.message || "Too many requests. Please wait a moment before asking for a new code.");
            startCooldown(RATE_LIMIT_COOLDOWN_SECONDS);
            return;
        }

        showAlert("danger", DevPilotApi.getDisplayMessage(result));
        resendBtn.disabled = false;
    } finally {
        resendLabel.textContent = "Resend Code";
    }
});

// start
function showMissingEmail() {
    const link = document.createElement("a")
    link.href = LOGIN_PAGE
    link.className = "alert-link"
    link.textContent = "Go to Log in"

    showAlert("warning", "We could not find which email to verify.", link)
    verifyBtn.disabled = true
    resendBtn.disabled = true
    otpInputs.forEach(function (input) {
        input.disabled = true
    })
}

if (email) {
    document.querySelectorAll("[data-email]").forEach(function (item) {
        item.textContent = email
    })

    if (fromLogin) {
        // no timer, the old code may be expired
        timerBox.classList.add("d-none")
        showAlert("warning", "Your email is not verified yet. Enter the code we sent you, or request a new one.")
    } else {
        startExpiryCountdown(false)
        startCooldown(RESEND_COOLDOWN_SECONDS)
    }
    otpInputs[0].focus()
} else {
    showMissingEmail()
}