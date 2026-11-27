// API client
(function (window) {
    "use strict";

    const STORAGE_KEYS = {
        ACCESS_TOKEN: "accessToken",
        REFRESH_TOKEN: "refreshToken"
    };

    function safeGetItem(key) {
        try {
            return window.localStorage ? window.localStorage.getItem(key) : null;
        } catch (e) {
            console.warn("Storage access failed:", e);
            return null;
        }
    }

    function safeSetItem(key, value) {
        try {
            if (window.localStorage) {
                window.localStorage.setItem(key, value);
            }
        } catch (e) {
            console.warn("Storage set failed:", e);
        }
    }

    function safeRemoveItem(key) {
        try {
            if (window.localStorage) {
                window.localStorage.removeItem(key);
            }
        } catch (e) {
            console.warn("Storage remove failed:", e);
        }
    }

    function formatBearer(token) {
        if (!token || typeof token !== "string") return "";
        const trimmed = token.trim();
        if (!trimmed) return "";
        return trimmed.toLowerCase().startsWith("bearer ") ? trimmed : ("Bearer " + trimmed);
    }

    function getRefreshUrl() {
        const base = (typeof window !== "undefined" && window.API_BASE) ? window.API_BASE : "";
        return base ? new URL("/api/Auth/refresh", base).href : "/api/Auth/refresh";
    }

    function getLogoutUrl() {
        const base = (typeof window !== "undefined" && window.API_BASE) ? window.API_BASE : "";
        return base ? new URL("/api/Auth/logout", base).href : "/api/Auth/logout";
    }

    const DevPilotApi = {
        /** Auth token keys in localStorage. */
        STORAGE_KEYS: STORAGE_KEYS,

        /** @returns {string} */
        getAccessToken() {
            return (safeGetItem(STORAGE_KEYS.ACCESS_TOKEN) || "").trim();
        },

        /** @returns {string} */
        getRefreshToken() {
            return (safeGetItem(STORAGE_KEYS.REFRESH_TOKEN) || "").trim();
        },

        /** Store access token; optional refresh token. */
        setTokens(accessToken, refreshToken) {
            if (accessToken) {
                safeSetItem(STORAGE_KEYS.ACCESS_TOKEN, accessToken.trim());
                // Refresh cached roles from the new JWT.
                safeRemoveItem("userRoles");
                const rolesFromToken = this.getRoles();
                if (rolesFromToken.length) {
                    this.setRoles(rolesFromToken);
                }
            }
            if (refreshToken) {
                safeSetItem(STORAGE_KEYS.REFRESH_TOKEN, refreshToken.trim());
            }
        },

        /** Drop tokens from localStorage. */
        clearTokens() {
            safeRemoveItem(STORAGE_KEYS.ACCESS_TOKEN);
            safeRemoveItem(STORAGE_KEYS.REFRESH_TOKEN);
            safeRemoveItem("userRoles");
        },

        /** @returns {boolean} */
        isAuthenticated() {
            return Boolean(this.getAccessToken());
        },

        /** JWT payload only (not verified). @returns {object|null} */
        getTokenClaims() {
            const token = this.getAccessToken();
            if (!token) return null;
            const parts = token.split(".");
            if (parts.length < 2) return null;
            try {
                const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
                const padded = payload + "=".repeat((4 - (payload.length % 4)) % 4);
                return JSON.parse(atob(padded));
            } catch (e) {
                return null;
            }
        },

        /** Roles from cache or JWT. @returns {string[]} */
        getRoles() {
            const cached = safeGetItem("userRoles");
            if (cached) {
                try {
                    const parsed = JSON.parse(cached);
                    if (Array.isArray(parsed) && parsed.length) {
                        return parsed.map(function (r) { return String(r); });
                    }
                } catch (e) { /* ignore */ }
            }

            const claims = this.getTokenClaims();
            if (!claims) return [];
            const roles = [];
            const pushRole = function (value) {
                if (value == null) return;
                if (Array.isArray(value)) {
                    value.forEach(pushRole);
                    return;
                }
                const text = String(value).trim();
                if (text) roles.push(text);
            };

            Object.keys(claims).forEach(function (key) {
                const lower = key.toLowerCase();
                if (lower === "role" || lower === "roles" || lower.endsWith("/role") || lower.endsWith("/claims/role")) {
                    pushRole(claims[key]);
                }
            });
            return roles;
        },

        /** Cache roles for navbar. */
        setRoles(roles) {
            if (!roles || !roles.length) {
                safeRemoveItem("userRoles");
                return;
            }
            safeSetItem("userRoles", JSON.stringify(roles.map(function (r) { return String(r); })));
        },

        /** @returns {boolean} */
        isAdmin() {
            return this.getRoles().some(function (r) {
                return String(r).toLowerCase() === "admin";
            });
        },

        /** Fetch /api/Users/me roles. @returns {Promise<string[]>} */
        async refreshRolesFromProfile() {
            try {
                const base = (typeof window !== "undefined" && window.API_BASE) ? window.API_BASE : "";
                const url = base ? new URL("/api/Users/me", base).href : "/api/Users/me";
                const res = await this.request(url);
                if (!res.ok || !res.data) return this.getRoles();
                const roles = res.data.roles || res.data.Roles || [];
                if (Array.isArray(roles) && roles.length) {
                    this.setRoles(roles);
                }
                return this.getRoles();
            } catch (e) {
                return this.getRoles();
            }
        },

        /** Prefix Bearer once. @returns {string} */
        formatBearer(token) {
            return formatBearer(token);
        },

        /**
         * fetch wrapper: JSON shape, Bearer auth, refresh on 401.
         * options.skipAuth skips the header; _isRetry is internal.
         */
        async request(url, options = {}) {
            const correlationId = "client-" + Date.now() + "-" + Math.random().toString(36).substring(2, 9);
            const headers = Object.assign({}, options.headers || {});

            if (!headers["Content-Type"] && !headers["content-type"] && !(options.body instanceof FormData)) {
                headers["Content-Type"] = "application/json";
            }
            if (!headers["X-Correlation-ID"] && !headers["x-correlation-id"]) {
                headers["X-Correlation-ID"] = correlationId;
            }

            // Bearer token when logged in
            let authHeaderKey = Object.keys(headers).find(k => k.toLowerCase() === "authorization");
            if (authHeaderKey && headers[authHeaderKey]) {
                headers[authHeaderKey] = formatBearer(headers[authHeaderKey]);
            } else if (!options.skipAuth) {
                const storedAccessToken = this.getAccessToken();
                if (storedAccessToken) {
                    headers["Authorization"] = formatBearer(storedAccessToken);
                }
            }

            try {
                const response = await fetch(url, Object.assign({}, options, { headers }));

                // 401: try refresh once, then retry
                const isAuthLifecycleEndpoint = /\/api\/auth\/(login|register|verify-email|resend-verification|refresh|refresh-token|logout|forgot-password|reset-password)/i.test(url);
                if (response.status === 401 && !options._isRetry && !isAuthLifecycleEndpoint) {
                    const refreshToken = this.getRefreshToken();
                    if (refreshToken) {
                        try {
                            const refreshPayload = JSON.stringify({ refreshToken: refreshToken });
                            const refreshResponse = await fetch(getRefreshUrl(), {
                                method: "POST",
                                headers: {
                                    "Content-Type": "application/json",
                                    "X-Correlation-ID": correlationId + "-refresh"
                                },
                                body: refreshPayload
                            });

                            const refreshJson = await refreshResponse.json().catch(function () { return null; });
                            if (refreshResponse.ok && refreshJson && refreshJson.success && refreshJson.data && refreshJson.data.accessToken) {
                                this.setTokens(refreshJson.data.accessToken, refreshJson.data.refreshToken || refreshToken);

                                // Retry with new access token
                                const retryHeaders = Object.assign({}, headers, {
                                    "Authorization": formatBearer(refreshJson.data.accessToken)
                                });

                                const retryOptions = Object.assign({}, options, {
                                    headers: retryHeaders,
                                    _isRetry: true
                                });

                                return await this.request(url, retryOptions);
                            } else {
                                // Refresh failed; clear tokens
                                this.clearTokens();
                                window.dispatchEvent(new CustomEvent("devpilot:unauthorized", { detail: { reason: "session_expired" } }));
                            }
                        } catch (refreshErr) {
                            console.warn("Silent token refresh attempt failed:", refreshErr);
                            this.clearTokens();
                        }
                    } else {
                        this.clearTokens();
                    }
                }

                const rawJson = await response.json().catch(function () {
                    return null;
                });

                if (rawJson && typeof rawJson === "object") {
                    const rawErrors = rawJson.errors;
                    let normalizedErrors = [];

                    if (Array.isArray(rawErrors)) {
                        normalizedErrors = rawErrors.map(function (err) {
                            if (typeof err === "string") {
                                return { field: "", message: err, code: "VALIDATION_ERROR" };
                            }
                            return {
                                field: (err.field || "").toLowerCase(),
                                message: err.message || "",
                                code: err.code || "VALIDATION_ERROR"
                            };
                        });
                    } else if (rawErrors && typeof rawErrors === "object") {
                        Object.keys(rawErrors).forEach(function (key) {
                            const val = rawErrors[key];
                            const field = key.replace("$.", "").toLowerCase();
                            const message = Array.isArray(val) ? val[0] : String(val);
                            normalizedErrors.push({ field: field, message: message, code: "VALIDATION_ERROR" });
                        });
                    }

                    return {
                        ok: response.ok,
                        status: response.status,
                        success: typeof rawJson.success === "boolean" ? rawJson.success : response.ok,
                        message: rawJson.message || (response.ok ? "Operation completed successfully." : "An error occurred."),
                        data: rawJson.data !== undefined ? rawJson.data : null,
                        errors: normalizedErrors,
                        errorCode: rawJson.errorCode || null,
                        traceId: rawJson.traceId || rawJson.requestId || correlationId
                    };
                }

                return {
                    ok: response.ok,
                    status: response.status,
                    success: response.ok,
                    message: response.ok ? "Operation completed successfully." : "The server returned an empty or non-JSON response.",
                    data: null,
                    errors: [],
                    errorCode: response.ok ? null : "EMPTY_RESPONSE",
                    traceId: correlationId
                };
            } catch (networkError) {
                console.error("Network or connectivity failure:", networkError);
                return {
                    ok: false,
                    status: 0,
                    success: false,
                    message: "Could not reach the server. Please check your internet connection and try again.",
                    data: null,
                    errors: [],
                    errorCode: "NETWORK_ERROR",
                    traceId: correlationId
                };
            }
        },

        /** POST logout, clear storage, redirect. */
        async logout(redirectUrl = "login.html") {
            const refreshToken = this.getRefreshToken();
            if (refreshToken) {
                try {
                    await fetch(getLogoutUrl(), {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ refreshToken: refreshToken })
                    });
                } catch (e) {
                    console.warn("Backend logout request failed, proceeding with local token clearance:", e);
                }
            }
            this.clearTokens();
            if (redirectUrl && typeof window !== "undefined" && window.location) {
                window.location.href = redirectUrl;
            }
        },

        /** Paint API field errors onto form inputs. @returns {boolean} */
        renderFieldErrors(fieldMap, errors) {
            if (!Array.isArray(errors) || errors.length === 0) {
                return false;
            }

            let rendered = false;
            errors.forEach(function (err) {
                const fieldName = (err.field || "").toLowerCase();
                const input = fieldMap[fieldName];
                if (input) {
                    const group = input.closest(".form-group") || input.parentElement;
                    if (group) {
                        group.classList.add("has-error");
                        input.setAttribute("aria-invalid", "true");
                        const errorLabel = group.querySelector(".error-text");
                        if (errorLabel) {
                            errorLabel.textContent = err.message;
                        }
                    }
                    rendered = true;
                }
            });

            return rendered;
        },

        /** Reset form error UI. */
        clearFieldErrors(formElement) {
            if (!formElement) return;
            formElement.querySelectorAll(".form-group.has-error").forEach(function (group) {
                group.classList.remove("has-error");
            });
            formElement.querySelectorAll("[aria-invalid='true']").forEach(function (input) {
                input.removeAttribute("aria-invalid");
            });
        },

        /** Message for alerts; adds trace id on 500. */
        getDisplayMessage(apiResult) {
            if (!apiResult) {
                return "An unexpected error occurred. Please try again.";
            }

            let msg = apiResult.message || "An unexpected error occurred. Please try again.";
            if (apiResult.status === 500 && apiResult.traceId) {
                msg += " (Ref: " + apiResult.traceId + ")";
            }
            return msg;
        }
    };

    window.DevPilotApi = DevPilotApi;
})(window);
