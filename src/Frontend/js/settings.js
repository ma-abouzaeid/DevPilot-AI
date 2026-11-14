(function () {
    "use strict";

    if (!window.DevPilotApi || !window.DevPilotApi.isAuthenticated()) {
        window.location.href = "login.html?returnUrl=" + encodeURIComponent("settings.html");
        return;
    }

    const alertBox = document.getElementById("settings-alert");
    const nameInput = document.getElementById("profile-name");
    const emailDisplay = document.getElementById("profile-email-display");
    const verifiedHint = document.getElementById("email-verified-hint");
    const profileForm = document.getElementById("profile-form");
    const passwordForm = document.getElementById("password-form");
    const emailChangeForm = document.getElementById("email-change-form");
    const emailConfirmForm = document.getElementById("email-confirm-form");
    const emailConfirmPanel = document.getElementById("email-confirm-panel");
    const pendingEmailLabel = document.getElementById("pending-email-label");
    const deleteBtn = document.getElementById("btn-delete-account");

    function showAlert(message, type) {
        alertBox.textContent = message;
        alertBox.className = "alert alert-" + (type || "danger");
        alertBox.classList.remove("d-none");
        alertBox.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    function clearAlert() {
        alertBox.classList.add("d-none");
        alertBox.textContent = "";
    }

    function showPendingEmail(pending) {
        if (pending) {
            emailConfirmPanel.classList.remove("d-none");
            pendingEmailLabel.textContent = pending;
            document.getElementById("new-email").value = pending;
        } else {
            emailConfirmPanel.classList.add("d-none");
            pendingEmailLabel.textContent = "";
        }
    }

    async function loadProfile() {
        const url = new URL("/api/Users/me", API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        if (!res.ok || !res.data) throw new Error(res.message || "Failed to load profile");

        const data = res.data;
        nameInput.value = data.name || data.Name || "";
        emailDisplay.value = data.email || data.Email || "";
        const verifiedAt = data.emailVerifiedAt || data.EmailVerifiedAt;
        verifiedHint.textContent = verifiedAt
            ? "Verified · change email in the section below."
            : "Not verified yet · you can still change email below.";
        showPendingEmail(data.pendingEmailChange || data.PendingEmailChange || null);
    }

    profileForm.addEventListener("submit", async function (e) {
        e.preventDefault();
        clearAlert();
        const name = (nameInput.value || "").trim();
        if (name.length < 2 || name.length > 150) {
            showAlert("Name must be between 2 and 150 characters.", "danger");
            return;
        }

        const saveBtn = document.getElementById("btn-save-profile");
        saveBtn.disabled = true;
        try {
            const url = new URL("/api/Users/me", API_BASE).href;
            const res = await window.DevPilotApi.request(url, {
                method: "PUT",
                body: JSON.stringify({ name: name })
            });
            if (!res.ok || !res.data) throw new Error(res.message || "Update failed");
            nameInput.value = res.data.name || res.data.Name || name;
            showAlert("Display name updated.", "success");
        } catch (err) {
            showAlert(err.message || "Update failed", "danger");
        } finally {
            saveBtn.disabled = false;
        }
    });

    passwordForm.addEventListener("submit", async function (e) {
        e.preventDefault();
        clearAlert();
        const currentPassword = document.getElementById("current-password").value;
        const newPassword = document.getElementById("new-password").value;
        const confirmNewPassword = document.getElementById("confirm-password").value;

        if (newPassword !== confirmNewPassword) {
            showAlert("New password and confirmation do not match.", "danger");
            return;
        }

        const btn = document.getElementById("btn-change-password");
        btn.disabled = true;
        try {
            const url = new URL("/api/Users/me/change-password", API_BASE).href;
            const res = await window.DevPilotApi.request(url, {
                method: "POST",
                body: JSON.stringify({
                    currentPassword: currentPassword,
                    newPassword: newPassword,
                    confirmNewPassword: confirmNewPassword
                })
            });
            if (!res.ok) throw new Error(res.message || "Password update failed");
            passwordForm.reset();
            showAlert("Password updated successfully.", "success");
        } catch (err) {
            showAlert(err.message || "Password update failed", "danger");
        } finally {
            btn.disabled = false;
        }
    });

    emailChangeForm.addEventListener("submit", async function (e) {
        e.preventDefault();
        clearAlert();
        const btn = document.getElementById("btn-request-email");
        btn.disabled = true;
        try {
            const url = new URL("/api/Users/me/change-email", API_BASE).href;
            const res = await window.DevPilotApi.request(url, {
                method: "POST",
                body: JSON.stringify({
                    currentPassword: document.getElementById("email-current-password").value,
                    newEmail: document.getElementById("new-email").value.trim()
                })
            });
            if (!res.ok || !res.data) throw new Error(res.message || "Could not start email change");
            const pending = res.data.pendingEmail || res.data.PendingEmail;
            showPendingEmail(pending);
            document.getElementById("email-current-password").value = "";
            showAlert(res.message || res.data.message || "Verification code sent.", "success");
        } catch (err) {
            showAlert(err.message || "Could not start email change", "danger");
        } finally {
            btn.disabled = false;
        }
    });

    emailConfirmForm.addEventListener("submit", async function (e) {
        e.preventDefault();
        clearAlert();
        const btn = document.getElementById("btn-confirm-email");
        btn.disabled = true;
        try {
            const url = new URL("/api/Users/me/confirm-email-change", API_BASE).href;
            const res = await window.DevPilotApi.request(url, {
                method: "POST",
                body: JSON.stringify({ code: document.getElementById("email-otp").value.trim() })
            });
            if (!res.ok || !res.data) throw new Error(res.message || "Email confirmation failed");
            emailDisplay.value = res.data.email || res.data.Email || emailDisplay.value;
            document.getElementById("email-otp").value = "";
            document.getElementById("new-email").value = "";
            showPendingEmail(null);
            verifiedHint.textContent = "Verified · change email in the section below.";
            showAlert("Email updated. Your account data stayed the same.", "success");
        } catch (err) {
            showAlert(err.message || "Email confirmation failed", "danger");
        } finally {
            btn.disabled = false;
        }
    });

    document.getElementById("btn-resend-email").addEventListener("click", async function () {
        clearAlert();
        const btn = document.getElementById("btn-resend-email");
        btn.disabled = true;
        try {
            const url = new URL("/api/Users/me/resend-email-change", API_BASE).href;
            const res = await window.DevPilotApi.request(url, { method: "POST" });
            if (!res.ok) throw new Error(res.message || "Resend failed");
            const pending = res.data && (res.data.pendingEmail || res.data.PendingEmail);
            if (pending) showPendingEmail(pending);
            showAlert(res.message || "Code resent.", "success");
        } catch (err) {
            showAlert(err.message || "Resend failed", "danger");
        } finally {
            btn.disabled = false;
        }
    });

    deleteBtn.addEventListener("click", async function () {
        clearAlert();
        const confirmed = window.confirm(
            "Deactivate your account? You will be signed out and lose access to your workspace."
        );
        if (!confirmed) return;

        const typed = window.prompt("Type DELETE to confirm account deactivation:");
        if ((typed || "").trim().toUpperCase() !== "DELETE") {
            showAlert("Deactivation cancelled.", "warning");
            return;
        }

        deleteBtn.disabled = true;
        try {
            const url = new URL("/api/Users/me", API_BASE).href;
            const res = await window.DevPilotApi.request(url, { method: "DELETE" });
            if (!res.ok) throw new Error(res.message || "Could not deactivate account");
            if (typeof window.DevPilotApi.logout === "function") {
                window.DevPilotApi.logout("login.html");
            } else {
                localStorage.removeItem("accessToken");
                localStorage.removeItem("refreshToken");
                window.location.href = "login.html";
            }
        } catch (err) {
            deleteBtn.disabled = false;
            showAlert(err.message || "Could not deactivate account", "danger");
        }
    });

    loadProfile().catch(function (err) {
        showAlert(err.message || "Failed to load profile", "danger");
    });
})();
