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

    async function loadProfile() {
        const url = new URL("/api/Users/me", API_BASE).href;
        const res = await window.DevPilotApi.request(url);
        if (!res.ok || !res.data) throw new Error(res.message || "Failed to load profile");

        const data = res.data;
        nameInput.value = data.name || data.Name || "";
        emailDisplay.value = data.email || data.Email || "";
        const verifiedAt = data.emailVerifiedAt || data.EmailVerifiedAt;
        verifiedHint.textContent = verifiedAt
            ? "Verified on this account."
            : "Not verified yet.";
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

    async function loadPayments() {
        var host = document.getElementById("payments-list");
        if (!host) return;
        try {
            var url = new URL("/api/Payments/mine", API_BASE).href;
            var res = await window.DevPilotApi.request(url);
            var items = res.data || [];
            if (!items.length) {
                host.innerHTML = '<p class="mb-0 text-gray">No payment attempts yet.</p>';
                return;
            }
            host.innerHTML = '<div class="table-responsive"><table class="table table-sm align-middle mb-0"><thead><tr>' +
                "<th>Code</th><th>Plan</th><th>Status</th><th>Amount</th><th>Date</th></tr></thead><tbody>" +
                items.map(function (p) {
                    return "<tr><td><code>" + (p.merchantOrderId || p.MerchantOrderId) + "</code></td>" +
                        "<td>" + (p.planCode || p.PlanCode) + "</td>" +
                        "<td>" + (p.status || p.Status) + "</td>" +
                        "<td>$" + Number(p.amountUsd ?? p.AmountUsd ?? 0).toFixed(2) + "</td>" +
                        "<td class='small text-gray'>" + new Date(p.createdAt || p.CreatedAt).toLocaleString() + "</td></tr>";
                }).join("") + "</tbody></table></div>";
        } catch (err) {
            host.textContent = err.message || "Could not load payments.";
        }
    }

    loadProfile().catch(function (err) {
        showAlert(err.message || "Failed to load profile", "danger");
    });
    loadPayments();
})();
