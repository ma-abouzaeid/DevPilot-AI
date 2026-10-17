// DevPilot AI — Centralized API Client & Response Handler
(function (window) {
    "use strict";

    const DevPilotApi = {
        /**
         * Dispatches a fetch request and returns a normalized ApiResponse structure.
         * @param {string} url 
         * @param {RequestInit} options 
         * @returns {Promise<{ok: boolean, status: number, success: boolean, message: string, data: any, errors: Array<{field: string, message: string, code: string}>, errorCode: string|null, traceId: string|null}>}
         */
        async request(url, options = {}) {
            const correlationId = "client-" + Date.now() + "-" + Math.random().toString(36).substring(2, 9);
            const headers = Object.assign(
                {
                    "Content-Type": "application/json",
                    "X-Correlation-ID": correlationId
                },
                options.headers || {}
            );

            try {
                const response = await fetch(url, Object.assign({}, options, { headers }));
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
