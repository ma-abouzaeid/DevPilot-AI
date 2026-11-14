(function () {
    "use strict";

    if (!window.DevPilotApi || !window.DevPilotApi.isAuthenticated()) {
        window.location.href = "login.html?returnUrl=" + encodeURIComponent("settings.html");
        return;
    }

    const alertBox = document.getElementById("settings-alert");
    const nameInput = document.getElementById("profile-name");
    const emailInput = document.getElementById("profile-email");
    const verifiedHint = document.getElementById("email-verified-hint");
    const form = document.getElementById("profile-form");
    const saveBtn = document.getElementById("btn-save-profile");
    const deleteBtn = document.getElementById("btn-delete-account");

    function showAlert(message, type) {
        alertBox.textContent = message;
        alertBox.className = "alert alert-" + (type || "danger");
        alertBox.classList.remove("d-none");
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
        emailInput.value = data.email || data.Email || "";
        const verifiedAt = data.emailVerifiedAt || data.EmailVerifiedAt;
        verifiedHint.textContent = verifiedAt
            ? "Verified · email cannot be changed here."
            : "Not verified yet · email cannot be changed here.";
    }

    form.addEventListener("submit", async function (e) {
        e.preventDefault();
        clearAlert();
        const name = (nameInput.value || "").trim();
        if (name.length < 2 || name.length > 150) {
            showAlert("Name must be between 2 and 150 characters.", "danger");
            return;
        }

        saveBtn.disabled = true;
        saveBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Saving…';
        try {
            const url = new URL("/api/Users/me", API_BASE).href;
            const res = await window.DevPilotApi.request(url, {
                method: "PUT",
                body: JSON.stringify({ name: name })
            });
            if (!res.ok || !res.data) throw new Error(res.message || "Update failed");
            nameInput.value = res.data.name || res.data.Name || name;
            showAlert("Profile updated.", "success");
        } catch (err) {
            showAlert(err.message || "Update failed", "danger");
        } finally {
            saveBtn.disabled = false;
            saveBtn.innerHTML = '<i class="fa-solid fa-floppy-disk"></i> Save changes';
        }
    });

    deleteBtn.addEventListener("click", async function () {
        clearAlert();
        const confirmed = window.confirm(
            "Deactivate your account? You will be signed out and lose access to your workspace."
        );
        if (!confirmed) return;

        const typed = window.prompt('Type DELETE to confirm account deactivation:');
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
