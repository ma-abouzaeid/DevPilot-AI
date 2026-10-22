// DevPilot AI — Centralized API Client & Response Handler
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
        /**
         * Storage keys used by DevPilot authentication.
         */
        STORAGE_KEYS: STORAGE_KEYS,

        /**
         * Gets the current access token from localStorage.
         * @returns {string}
         */
        getAccessToken() {
            return (safeGetItem(STORAGE_KEYS.ACCESS_TOKEN) || "").trim();
        },

        /**
         * Gets the current refresh token from localStorage.
         * @returns {string}
         */
        getRefreshToken() {
            return (safeGetItem(STORAGE_KEYS.REFRESH_TOKEN) || "").trim();
        },

        /**
         * Persists access and optional refresh tokens in localStorage.
         * @param {string} accessToken 
         * @param {string} [refreshToken] 
         */
        setTokens(accessToken, refreshToken) {
            if (accessToken) {
                safeSetItem(STORAGE_KEYS.ACCESS_TOKEN, accessToken.trim());
            }
            if (refreshToken) {
                safeSetItem(STORAGE_KEYS.REFRESH_TOKEN, refreshToken.trim());
            }
        },

        /**
         * Clears all stored authentication tokens.
         */
        clearTokens() {
            safeRemoveItem(STORAGE_KEYS.ACCESS_TOKEN);
            safeRemoveItem(STORAGE_KEYS.REFRESH_TOKEN);
        },

        /**
         * Checks whether an access token is present.
         * @returns {boolean}
         */
        isAuthenticated() {
            return Boolean(this.getAccessToken());
        },

        /**
         * Ensures a token is formatted with the standard "Bearer <token>" scheme exactly once.
         * @param {string} token 
         * @returns {string}
         */
        formatBearer(token) {
            return formatBearer(token);
        },

        /**
         * Dispatches a fetch request and returns a normalized ApiResponse structure.
         * Automatically attaches Authorization: Bearer <token> for authenticated calls,
         * and attempts transparent refresh token rotation on 401 when eligible.
         * @param {string} url 
         * @param {RequestInit & {_isRetry?: boolean, skipAuth?: boolean}} options 
         * @returns {Promise<{ok: boolean, status: number, success: boolean, message: string, data: any, errors: Array<{field: string, message: string, code: string}>, errorCode: string|null, traceId: string|null}>}
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

            // Handle Authorization header
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

                // Handle 401 Unauthorized with token refresh if eligible
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

                                // Retry original request with newly acquired access token
                                const retryHeaders = Object.assign({}, headers, {
                                    "Authorization": formatBearer(refreshJson.data.accessToken)
                                });

                                const retryOptions = Object.assign({}, options, {
                                    headers: retryHeaders,
                                    _isRetry: true
                                });

                                return await this.request(url, retryOptions);
                            } else {
                                // Refresh failed or expired: clear tokens
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

        /**
         * Logs out the user by notifying the backend and purging local storage.
         * @param {string} [redirectUrl]
         * @returns {Promise<void>}
         */
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

        /**
         * Renders field-level validation errors next to input elements.
         * @param {Object.<string, HTMLElement>} fieldMap Map of lowercase field names to input elements
         * @param {Array<{field: string, message: string, code: string}>} errors 
         * @returns {boolean} True if any field error was matched and rendered
         */
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

        /**
         * Clears all error states from a form.
         * @param {HTMLElement} formElement 
         */
        clearFieldErrors(formElement) {
            if (!formElement) return;
            formElement.querySelectorAll(".form-group.has-error").forEach(function (group) {
                group.classList.remove("has-error");
            });
            formElement.querySelectorAll("[aria-invalid='true']").forEach(function (input) {
                input.removeAttribute("aria-invalid");
            });
        },

        /**
         * Builds a user-friendly error message, appending the trace reference when appropriate.
         * @param {Object} apiResult 
         * @returns {string}
         */
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
