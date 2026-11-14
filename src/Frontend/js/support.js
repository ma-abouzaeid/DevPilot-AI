(function () {
    "use strict";

    if (!window.DevPilotApi || !window.DevPilotApi.isAuthenticated()) {
        window.location.href = "login.html?redirect=" + encodeURIComponent("support.html");
        return;
    }

    const alertBox = document.getElementById("support-alert");
    const listEl = document.getElementById("tickets-list");
    const detailEl = document.getElementById("ticket-detail");
    let activeTicketId = null;
    let hubConnection = null;
    let joinedTicketId = null;

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function showAlert(type, message) {
        alertBox.className = "alert alert-" + type;
        alertBox.textContent = message;
        alertBox.classList.remove("d-none");
    }

    function clearAlert() {
        alertBox.classList.add("d-none");
        alertBox.textContent = "";
    }

    function statusBadge(status) {
        const s = String(status || "").toLowerCase();
        if (s === "assigned") return '<span class="badge-soft status-progress">Assigned</span>';
        if (s === "resolved") return '<span class="badge-soft solved">Resolved</span>';
        return '<span class="badge-soft open">Open</span>';
    }

    function formatDate(value) {
        if (!value) return "";
        try { return new Date(value).toLocaleString(); } catch (e) { return String(value); }
    }

    async function api(path, options) {
        const url = new URL(path, API_BASE).href;
        const res = await window.DevPilotApi.request(url, options || {});
        if (!res.ok) throw new Error(res.message || "Request failed");
        return res;
    }

    async function ensureHub() {
        if (hubConnection && hubConnection.state === signalR.HubConnectionState.Connected) {
            return hubConnection;
        }
        if (hubConnection) {
            try { await hubConnection.stop(); } catch (e) { /* ignore */ }
            hubConnection = null;
            joinedTicketId = null;
        }
        if (!window.signalR) throw new Error("SignalR client failed to load.");

        const hubOptions = {
            accessTokenFactory: function () { return window.DevPilotApi.getAccessToken(); }
        };
        if (window.DEVPILOT_SIGNALR_LONG_POLLING) {
            hubOptions.transport = signalR.HttpTransportType.LongPolling;
        }

        hubConnection = new signalR.HubConnectionBuilder()
            .withUrl(new URL("/hubs/support-tickets", API_BASE).href, hubOptions)
            .withAutomaticReconnect()
            .build();

        hubConnection.on("messageReceived", function (msg) {
            const ticketId = msg.supportTicketId || msg.SupportTicketId;
            if (Number(ticketId) !== Number(activeTicketId)) return;
            appendMessage(msg);
        });

        hubConnection.on("ticketUpdated", function () {
            if (activeTicketId) openTicket(activeTicketId, false);
            loadTickets();
        });

        hubConnection.on("ticketResolved", function (ticket) {
            if (activeTicketId) renderDetail(ticket, []);
            loadTickets();
            leaveTicketRoom();
        });

        try {
            await hubConnection.start();
        } catch (err) {
            hubConnection = null;
            throw err;
        }
        return hubConnection;
    }

    async function joinTicketRoom(ticketId) {
        const conn = await ensureHub();
        if (conn.state !== signalR.HubConnectionState.Connected) {
            throw new Error("Live chat is not connected yet.");
        }
        if (joinedTicketId && joinedTicketId !== ticketId) {
            try { await conn.invoke("LeaveTicket", joinedTicketId); } catch (e) { /* ignore */ }
        }
        await conn.invoke("JoinTicket", ticketId);
        joinedTicketId = ticketId;
    }

    async function leaveTicketRoom() {
        if (!hubConnection || !joinedTicketId) return;
        try { await hubConnection.invoke("LeaveTicket", joinedTicketId); } catch (e) { /* ignore */ }
        joinedTicketId = null;
    }

    function appendMessage(msg) {
        const box = document.getElementById("chat-messages");
        if (!box) return;
        const empty = box.querySelector(".empty-state");
        if (empty) empty.remove();

        const fromAdmin = !!(msg.isFromAdmin || msg.IsFromAdmin);
        // User page: my messages on the right, admin on the left.
        const name = msg.senderName || msg.SenderName || (fromAdmin ? "Admin" : "You");
        const body = msg.body || msg.Body || "";
        const created = formatDate(msg.createdAt || msg.CreatedAt);
        const row = document.createElement("div");
        row.className = "support-chat-bubble " + (fromAdmin ? "is-admin" : "is-user is-mine");
        const role = fromAdmin ? "Admin" : "You";
        row.innerHTML =
            '<div class="support-chat-meta"><span><span class="role-tag">' + role + "</span><strong>" +
            escapeHtml(name) + "</strong></span>" +
            "<span>" + escapeHtml(created) + "</span></div>" +
            "<p>" + escapeHtml(body) + "</p>";
        box.appendChild(row);
        box.scrollTop = box.scrollHeight;
    }

    function getMyUserId() {
        const claims = window.DevPilotApi.getTokenClaims() || {};
        return claims.nameid || claims.sub || claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] || "";
    }

    async function loadTickets() {
        listEl.innerHTML = '<div class="empty-state">Loading…</div>';
        try {
            const res = await api("/api/support-tickets/mine");
            const items = res.data || [];
            if (!items.length) {
                listEl.innerHTML = '<div class="empty-state">No tickets yet. Submit one on the left.</div>';
                return;
            }

            listEl.innerHTML = items.map(function (t) {
                const id = t.supportTicketId || t.SupportTicketId;
                const code = t.ticketCode || t.TicketCode || "";
                const title = t.title || t.Title || "";
                const status = t.status || t.Status || "";
                return (
                    '<button type="button" class="support-ticket-item' + (Number(id) === Number(activeTicketId) ? " is-active" : "") + '" data-id="' + id + '">' +
                    '<div class="d-flex justify-content-between gap-2">' +
                    '<strong>' + escapeHtml(code) + "</strong>" + statusBadge(status) +
                    "</div>" +
                    '<div class="text-gray small mt-1">' + escapeHtml(title) + "</div>" +
                    "</button>"
                );
            }).join("");

            listEl.querySelectorAll("[data-id]").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    openTicket(Number(btn.getAttribute("data-id")), true);
                });
            });
        } catch (e) {
            listEl.innerHTML = '<div class="empty-state text-danger">' + escapeHtml(e.message) + "</div>";
        }
    }

    function renderDetail(ticket, messages) {
        const id = ticket.supportTicketId || ticket.SupportTicketId;
        const canChat = !!(ticket.canChat || ticket.CanChat);
        const status = ticket.status || ticket.Status || "";
        const resolution = ticket.resolutionSummary || ticket.ResolutionSummary || "";

        let chatHtml = "";
        if (String(status).toLowerCase() === "open") {
            chatHtml = '<div class="alert alert-success-soft mb-0">Waiting for an admin to accept this ticket. Chat unlocks after Accept.</div>';
        } else if (String(status).toLowerCase() === "resolved") {
            chatHtml =
                '<div class="alert alert-success-soft">' +
                "<strong>Resolved</strong><br>" + escapeHtml(resolution || "No resolution notes.") +
                "</div>" +
                '<p class="text-gray small mb-0">Chat history was deleted when the ticket was closed.</p>';
        } else if (canChat) {
            chatHtml =
                '<div class="support-chat" id="chat-panel">' +
                '<div class="support-chat-label"><span>Conversation</span>' +
                '<div class="support-chat-legend"><span class="leg-admin">Admin left</span><span class="leg-user">You right</span></div></div>' +
                '<div class="support-chat-messages" id="chat-messages"></div>' +
                '<form id="chat-form" class="support-chat-form">' +
                '<input type="text" class="form-control" id="chat-input" maxlength="4000" placeholder="Type a message…" required>' +
                '<button type="submit" class="btn btn-primary">Send</button>' +
                "</form></div>";
        } else {
            chatHtml = '<div class="empty-state">Chat is not available for this ticket.</div>';
        }

        detailEl.innerHTML =
            '<div class="d-flex flex-wrap justify-content-between gap-2 mb-3">' +
            "<div><h2 class=\"h5 mb-1\">" + escapeHtml(ticket.ticketCode || ticket.TicketCode || "") + "</h2>" +
            '<p class="mb-0 text-gray small">Created ' + escapeHtml(formatDate(ticket.createdAt || ticket.CreatedAt)) + "</p></div>" +
            statusBadge(status) +
            "</div>" +
            "<h3 class=\"h6\">" + escapeHtml(ticket.title || ticket.Title || "") + "</h3>" +
            '<p class="text-gray">' + escapeHtml(ticket.description || ticket.Description || "") + "</p>" +
            '<p class="text-gray small">Assigned admin: ' +
            escapeHtml(ticket.assignedAdminName || ticket.AssignedAdminName || "—") + "</p>" +
            chatHtml;

        const box = document.getElementById("chat-messages");
        if (box) {
            if (!messages.length) {
                box.innerHTML = '<div class="empty-state">No messages yet. Say hello.</div>';
            } else {
                messages.forEach(appendMessage);
            }
        }

        const form = document.getElementById("chat-form");
        if (form) {
            form.addEventListener("submit", async function (e) {
                e.preventDefault();
                const input = document.getElementById("chat-input");
                const body = (input.value || "").trim();
                if (!body) return;
                form.querySelector("button").disabled = true;
                try {
                    let conn = null;
                    try { conn = await ensureHub(); } catch (hubErr) { conn = null; }
                    if (conn && conn.state === signalR.HubConnectionState.Connected) {
                        if (Number(joinedTicketId) !== Number(id)) {
                            await joinTicketRoom(id);
                        }
                        await conn.invoke("SendMessage", id, body, false);
                    } else {
                        const sent = await api("/api/support-tickets/" + id + "/messages", {
                            method: "POST",
                            body: JSON.stringify({ body: body, asAdmin: false })
                        });
                        if (sent.data) appendMessage(sent.data);
                    }
                    input.value = "";
                } catch (err) {
                    showAlert("danger", err.message || "Failed to send message");
                } finally {
                    form.querySelector("button").disabled = false;
                }
            });
        }
    }

    async function openTicket(ticketId, joinChat) {
        clearAlert();
        activeTicketId = ticketId;
        detailEl.innerHTML = '<div class="empty-state">Loading ticket…</div>';
        try {
            const detailRes = await api("/api/support-tickets/" + ticketId);
            const ticket = detailRes.data;
            let messages = [];
            const canChat = !!(ticket.canChat || ticket.CanChat);
            if (canChat) {
                const msgRes = await api("/api/support-tickets/" + ticketId + "/messages");
                messages = msgRes.data || [];
            } else {
                await leaveTicketRoom();
            }
            renderDetail(ticket, messages);
            loadTickets();
            if (canChat && joinChat) {
                joinTicketRoom(ticketId).catch(function () { /* REST chat still works */ });
            }
        } catch (e) {
            detailEl.innerHTML = '<div class="empty-state text-danger">' + escapeHtml(e.message) + "</div>";
        }
    }

    document.getElementById("ticket-form").addEventListener("submit", async function (e) {
        e.preventDefault();
        clearAlert();
        const title = document.getElementById("ticket-title").value.trim();
        const description = document.getElementById("ticket-description").value.trim();
        const btn = document.getElementById("btn-create-ticket");
        btn.disabled = true;
        try {
            const res = await api("/api/support-tickets", {
                method: "POST",
                body: JSON.stringify({ title: title, description: description })
            });
            document.getElementById("ticket-form").reset();
            showAlert("success", "Ticket submitted. An admin will accept it soon.");
            await loadTickets();
            const id = res.data.supportTicketId || res.data.SupportTicketId;
            if (id) openTicket(id, false);
        } catch (err) {
            showAlert("danger", err.message || "Could not create ticket");
        } finally {
            btn.disabled = false;
        }
    });

    document.getElementById("btn-refresh-tickets").addEventListener("click", loadTickets);

    loadTickets();
})();
