using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Plans;
using DevPilotAI.Application.Support;
using DevPilotAI.Application.Support.DTOs;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Application.Telegram;

public class TelegramBotService : ITelegramBotService
{
    private readonly IApplicationDbContext _db;
    private readonly ITelegramBotClient _bot;
    private readonly IEmailService _email;
    private readonly IPlanUsageService _plans;
    private readonly ISupportTicketService _tickets;
    private readonly ITicketAttachmentStorage _attachments;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(
        IApplicationDbContext db,
        ITelegramBotClient bot,
        IEmailService email,
        IPlanUsageService plans,
        ISupportTicketService tickets,
        ITicketAttachmentStorage attachments,
        IConfiguration configuration,
        ILogger<TelegramBotService> logger)
    {
        _db = db;
        _bot = bot;
        _email = email;
        _plans = plans;
        _tickets = tickets;
        _attachments = attachments;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(JsonElement update, CancellationToken cancellationToken = default)
    {
        if (!_bot.IsConfigured) return;

        try
        {
            if (update.TryGetProperty("callback_query", out var cb))
            {
                await HandleCallbackAsync(cb, cancellationToken);
                return;
            }

            if (!update.TryGetProperty("message", out var message)) return;

            var chatId = message.GetProperty("chat").GetProperty("id").GetInt64();
            var text = message.TryGetProperty("text", out var t) ? (t.GetString() ?? string.Empty).Trim() : string.Empty;
            string? photoFileId = null;
            if (message.TryGetProperty("photo", out var photos) && photos.ValueKind == JsonValueKind.Array && photos.GetArrayLength() > 0)
            {
                photoFileId = photos[photos.GetArrayLength() - 1].GetProperty("file_id").GetString();
            }

            var convo = await GetOrCreateConversationAsync(chatId, cancellationToken);

            if (string.Equals(text, TelegramBotCommands.Start, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, TelegramBotCommands.Cancel, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "/cancel", StringComparison.OrdinalIgnoreCase))
            {
                await SetStateAsync(convo, TelegramBotStates.Idle, null, cancellationToken);
                await SendMenuAsync(chatId, "Choose an option:", cancellationToken);
                return;
            }

            if (convo.State == TelegramBotStates.Idle || string.IsNullOrWhiteSpace(convo.State))
            {
                await HandleIdleCommandAsync(chatId, convo, text, cancellationToken);
                return;
            }

            if (!string.IsNullOrWhiteSpace(photoFileId) && convo.State == TelegramBotStates.PaymentAwaitProof)
            {
                await HandlePaymentProofAsync(chatId, convo, photoFileId!, cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, "Please send a text message, or tap Cancel."), cancellationToken);
                return;
            }

            switch (convo.State)
            {
                case TelegramBotStates.RestoreAwaitEmail:
                    await HandleRestoreEmailAsync(chatId, convo, text, cancellationToken);
                    break;
                case TelegramBotStates.RestoreAwaitOtp:
                    await HandleRestoreOtpAsync(chatId, convo, text, cancellationToken);
                    break;
                case TelegramBotStates.EmailChangeAwaitAccountEmail:
                    await HandleEmailChangeAccountEmailAsync(chatId, convo, text, cancellationToken);
                    break;
                case TelegramBotStates.EmailChangeAwaitOldOtp:
                    await HandleEmailChangeOldOtpAsync(chatId, convo, text, cancellationToken);
                    break;
                case TelegramBotStates.EmailChangeAwaitNewEmail:
                    await HandleEmailChangeNewEmailAsync(chatId, convo, text, cancellationToken);
                    break;
                case TelegramBotStates.EmailChangeAwaitNewOtp:
                    await HandleEmailChangeNewOtpAsync(chatId, convo, text, cancellationToken);
                    break;
                case TelegramBotStates.SupportAwaitTicketConfirm:
                    await HandleSupportTicketConfirmAsync(chatId, convo, text, cancellationToken);
                    break;
                case TelegramBotStates.PaymentAwaitCode:
                    await HandlePaymentCodeAsync(chatId, convo, text, cancellationToken);
                    break;
                case TelegramBotStates.FeedbackAwaitText:
                    await HandleFeedbackTextAsync(chatId, convo, text, cancellationToken);
                    break;
                default:
                    await SetStateAsync(convo, TelegramBotStates.Idle, null, cancellationToken);
                    await SendMenuAsync(chatId, "Session reset. Choose an option:", cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram update handling failed");
        }
    }

    private async Task HandleIdleCommandAsync(long chatId, TelegramConversation convo, string text, CancellationToken ct)
    {
        if (text == TelegramBotCommands.Restore || text.Equals("/restore", StringComparison.OrdinalIgnoreCase))
        {
            await SetStateAsync(convo, TelegramBotStates.RestoreAwaitEmail, null, ct);
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
                "Send the email of the deactivated account.\nTap Cancel anytime to stop."), ct);
            return;
        }

        if (text == TelegramBotCommands.ChangeEmail || text.Equals("/email", StringComparison.OrdinalIgnoreCase))
        {
            await SetStateAsync(convo, TelegramBotStates.EmailChangeAwaitAccountEmail, null, ct);
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
                "Send your current account email so we can verify it's you."), ct);
            return;
        }

        if (text == TelegramBotCommands.Payment || text.Equals("/payment", StringComparison.OrdinalIgnoreCase))
        {
            await SetStateAsync(convo, TelegramBotStates.PaymentAwaitCode, null, ct);
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
                "Send your payment code from Account → Payments (starts with DP-)."), ct);
            return;
        }

        if (text == TelegramBotCommands.Feedback || text.Equals("/feedback", StringComparison.OrdinalIgnoreCase))
        {
            await SetStateAsync(convo, TelegramBotStates.FeedbackAwaitText, null, ct);
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
                "Send your suggestion or feedback in one message."), ct);
            return;
        }

        if (text == TelegramBotCommands.Help || text.Equals("/help", StringComparison.OrdinalIgnoreCase))
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
                "Tell me what's going wrong on the site (login, plans, GitHub, tickets…). I'll try to help."), ct);
            await SetStateAsync(convo, TelegramBotStates.SupportAwaitTicketConfirm,
                JsonSerializer.Serialize(new { pendingHelp = true }), ct);
            return;
        }

        // Knowledge / free-form help
        await HandleKnowledgeOrHelpAsync(chatId, convo, text, ct);
    }

    private async Task HandleKnowledgeOrHelpAsync(long chatId, TelegramConversation convo, string text, CancellationToken ct)
    {
        var term = text.Trim();
        if (term.Length < 3)
        {
            await SendMenuAsync(chatId, "Please send a clearer question, or pick a menu option.", ct);
            return;
        }

        var lower = term.ToLowerInvariant();
        var articles = await _db.KnowledgeArticles.AsNoTracking()
            .Where(a => a.IsPublished && !a.IsDeleted &&
                        (a.Title.ToLower().Contains(lower) || a.Summary.ToLower().Contains(lower)))
            .OrderByDescending(a => a.CreatedAt)
            .Take(3)
            .Select(a => new { a.ArticleCode, a.Title })
            .ToListAsync(ct);

        var site = FrontendBaseUrl();
        if (articles.Count > 0)
        {
            var sb = new StringBuilder("I found related articles:\n");
            foreach (var a in articles)
            {
                sb.AppendLine($"• <a href=\"{site}/article-details.html?code={Uri.EscapeDataString(a.ArticleCode)}\">{System.Net.WebUtility.HtmlEncode(a.Title)}</a>");
            }
            sb.AppendLine("\nIf that doesn't solve it, open DevPilot and run AI debugging:");
            sb.AppendLine($"👉 {site}/submit-problem.html");
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, sb.ToString()), ct);
            await SendMenuAsync(chatId, "Anything else?", ct);
            return;
        }

        // Lightweight FAQ
        string? tip = null;
        if (lower.Contains("login") || lower.Contains("password"))
            tip = $"Try Forgot Password on the site: {site}/forgot-password.html\nOr use Restore account here if the account was deactivated.";
        else if (lower.Contains("credit") || lower.Contains("plan") || lower.Contains("pro"))
            tip = $"Check Plans & Credits: {site}/plans.html\nFor payment issues use the Payment issue button.";
        else if (lower.Contains("github"))
            tip = $"Reconnect GitHub from: {site}/github.html";

        if (tip != null)
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, tip + "\n\nShould I open a support ticket with an admin? Reply Yes or No."), ct);
            await SetStateAsync(convo, TelegramBotStates.SupportAwaitTicketConfirm,
                JsonSerializer.Serialize(new { problem = term }), ct);
            return;
        }

        await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
            $"I couldn't find a matching article. Use the full AI debugger on the site:\n👉 {site}/submit-problem.html\n\nWant me to open a support ticket with an admin? Reply Yes or No."), ct);
        await SetStateAsync(convo, TelegramBotStates.SupportAwaitTicketConfirm,
            JsonSerializer.Serialize(new { problem = term }), ct);
    }

    #region Restore

    private async Task HandleRestoreEmailAsync(long chatId, TelegramConversation convo, string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (!normalized.Contains('@'))
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, "That doesn't look like an email. Try again."), ct);
            return;
        }

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, ct);

        if (user == null || !user.IsDeleted)
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
                "No deactivated account found for that email. Check the address or Cancel."), ct);
            return;
        }

        var otp = await CreateAndSendOtpAsync(user, user.Email, "TelegramRestore", ct);
        await SetStateAsync(convo, TelegramBotStates.RestoreAwaitOtp,
            JsonSerializer.Serialize(new { userId = user.UserId, email = user.Email }), ct);
        await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
            $"I sent an OTP to {user.Email}. Reply with the code."), ct);
        _ = otp;
    }

    private async Task HandleRestoreOtpAsync(long chatId, TelegramConversation convo, string code, CancellationToken ct)
    {
        var payload = ParsePayload(convo.PayloadJson);
        if (!payload.TryGetProperty("userId", out var uidEl))
        {
            await ResetIdleAsync(chatId, convo, "Session expired. Start again.", ct);
            return;
        }

        var userId = uidEl.GetInt32();
        var ok = await VerifyOtpAsync(userId, code, "TelegramRestore", ct);
        if (!ok)
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, "Invalid or expired OTP. Try again or Cancel."), ct);
            return;
        }

        var user = await _db.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == userId, ct);
        user.IsDeleted = false;
        user.DeletedAt = null;
        await _db.SaveChangesAsync(ct);

        await SetStateAsync(convo, TelegramBotStates.Idle, null, ct);
        await SendMenuAsync(chatId, "✅ Account restored successfully. You can log in on the website now.", ct);
    }

    #endregion

    #region Email change

    private async Task HandleEmailChangeAccountEmailAsync(long chatId, TelegramConversation convo, string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized && !u.IsDeleted, ct);
        if (user == null)
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, "No active account found for that email."), ct);
            return;
        }

        await CreateAndSendOtpAsync(user, user.Email, "TelegramEmailChangeOld", ct);
        await SetStateAsync(convo, TelegramBotStates.EmailChangeAwaitOldOtp,
            JsonSerializer.Serialize(new { userId = user.UserId, oldEmail = user.Email }), ct);
        await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
            $"OTP sent to {user.Email}. Reply with the code to prove ownership."), ct);
    }

    private async Task HandleEmailChangeOldOtpAsync(long chatId, TelegramConversation convo, string code, CancellationToken ct)
    {
        var payload = ParsePayload(convo.PayloadJson);
        var userId = payload.GetProperty("userId").GetInt32();
        if (!await VerifyOtpAsync(userId, code, "TelegramEmailChangeOld", ct))
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, "Invalid or expired OTP. Try again."), ct);
            return;
        }

        await SetStateAsync(convo, TelegramBotStates.EmailChangeAwaitNewEmail, convo.PayloadJson, ct);
        await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, "Verified. Send your new email address."), ct);
    }

    private async Task HandleEmailChangeNewEmailAsync(long chatId, TelegramConversation convo, string newEmail, CancellationToken ct)
    {
        var normalized = newEmail.Trim().ToLowerInvariant();
        if (!normalized.Contains('@'))
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, "Invalid email. Try again."), ct);
            return;
        }

        var taken = await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email.ToLower() == normalized, ct);
        if (taken)
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, "That email is already in use."), ct);
            return;
        }

        var payload = ParsePayload(convo.PayloadJson);
        var userId = payload.GetProperty("userId").GetInt32();
        var user = await _db.Users.FirstAsync(u => u.UserId == userId, ct);

        await CreateAndSendOtpAsync(user, normalized, "TelegramEmailChangeNew", ct, pendingEmail: normalized);

        var next = JsonSerializer.Serialize(new
        {
            userId,
            oldEmail = payload.GetProperty("oldEmail").GetString(),
            newEmail = normalized
        });
        await SetStateAsync(convo, TelegramBotStates.EmailChangeAwaitNewOtp, next, ct);
        await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
            $"OTP sent to {normalized}. Reply with the code."), ct);
    }

    private async Task HandleEmailChangeNewOtpAsync(long chatId, TelegramConversation convo, string code, CancellationToken ct)
    {
        var payload = ParsePayload(convo.PayloadJson);
        var userId = payload.GetProperty("userId").GetInt32();
        var newEmail = payload.GetProperty("newEmail").GetString()!;
        if (!await VerifyOtpAsync(userId, code, "TelegramEmailChangeNew", ct, pendingEmail: newEmail))
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, "Invalid or expired OTP. Try again."), ct);
            return;
        }

        var user = await _db.Users.FirstAsync(u => u.UserId == userId, ct);
        user.Email = newEmail;
        user.EmailVerifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await SetStateAsync(convo, TelegramBotStates.Idle, null, ct);
        await SendMenuAsync(chatId, $"✅ Email updated and verified to {newEmail}.", ct);
    }

    #endregion

    #region Support ticket

    private async Task HandleSupportTicketConfirmAsync(long chatId, TelegramConversation convo, string text, CancellationToken ct)
    {
        var yes = text.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                  text.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                  text.Equals("اه", StringComparison.OrdinalIgnoreCase) ||
                  text.Equals("أيوه", StringComparison.OrdinalIgnoreCase) ||
                  text.Equals("ايوه", StringComparison.OrdinalIgnoreCase);

        var no = text.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                 text.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                 text.Equals("لا", StringComparison.OrdinalIgnoreCase);

        if (no)
        {
            await ResetIdleAsync(chatId, convo, "OK — no ticket opened.", ct);
            return;
        }

        if (!yes && !text.StartsWith("email:", StringComparison.OrdinalIgnoreCase))
        {
            await HandleKnowledgeOrHelpAsync(chatId, convo, text, ct);
            return;
        }

        var payload = ParsePayload(convo.PayloadJson);
        var problem = payload.TryGetProperty("problem", out var p) ? p.GetString() : "Site issue reported via Telegram";
        var userId = await ResolveUserForChatAsync(chatId, convo, text, ct);
        if (userId == null)
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
                "To open a ticket, send your registered email like:\nemail: you@example.com"), ct);
            return;
        }

        var ticket = await _tickets.CreateAsync(userId.Value, new CreateSupportTicketDto
        {
            Title = "Telegram support request",
            Description = problem ?? "Site issue reported via Telegram"
        }, ct);

        await ResetIdleAsync(chatId, convo,
            $"✅ Ticket {ticket.TicketCode} opened. An admin will reply on the website soon.\nOpen: {FrontendBaseUrl()}/support.html", ct);
    }

    #endregion

    #region Payment

    private async Task HandlePaymentCodeAsync(long chatId, TelegramConversation convo, string code, CancellationToken ct)
    {
        var merchantId = code.Trim();
        var order = await _db.PaymentOrders
            .Include(o => o.Plan)
            .FirstOrDefaultAsync(o => o.MerchantOrderId == merchantId, ct);

        if (order == null)
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, "Payment code not found. Check Account → Payments."), ct);
            return;
        }

        var paid = string.Equals(order.Status, "Paid", StringComparison.OrdinalIgnoreCase) || order.PaidAt.HasValue;
        if (paid)
        {
            var usage = await _plans.GetMyUsageAsync(order.UserId, ct);
            if (string.Equals(usage.PlanCode, order.Plan.Code, StringComparison.OrdinalIgnoreCase))
            {
                await ResetIdleAsync(chatId, convo, $"Payment {merchantId} is Paid and {order.Plan.Code} is already active.", ct);
                return;
            }

            await _plans.ActivatePlanAsync(order.UserId, order.Plan.Code, ct, assignmentSource: "Paymob");
            await ResetIdleAsync(chatId, convo, $"✅ Payment verified. {order.Plan.Code} plan activated.", ct);
            return;
        }

        // Not marked paid — ask for proof
        await SetStateAsync(convo, TelegramBotStates.PaymentAwaitProof,
            JsonSerializer.Serialize(new { merchantOrderId = merchantId, userId = order.UserId }), ct);
        await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId,
            "This payment is not marked Paid in our system. Please send a screenshot/photo of the payment proof."), ct);
    }

    private async Task HandlePaymentProofAsync(long chatId, TelegramConversation convo, string fileId, CancellationToken ct)
    {
        var payload = ParsePayload(convo.PayloadJson);
        var merchantId = payload.GetProperty("merchantOrderId").GetString()!;
        var userId = payload.GetProperty("userId").GetInt32();

        var file = await _bot.DownloadFileAsync(fileId, ct);
        if (file == null || file.Bytes.Length == 0)
        {
            await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, "Could not download the photo. Try again."), ct);
            return;
        }

        var relPath = await _attachments.SaveAsync(file.Bytes, file.MimeType ?? "image/jpeg", ct);
        var ticket = await _tickets.CreateAsync(userId, new CreateSupportTicketDto
        {
            Title = $"Payment proof: {merchantId}",
            Description = $"User submitted payment proof for {merchantId} via Telegram. Status in DB was not Paid. Please review and activate if valid."
        }, ct);

        // Attach proof as a ticket message owned by the user.
        var msg = new SupportTicketMessage
        {
            SupportTicketId = ticket.SupportTicketId,
            SenderUserId = userId,
            IsFromAdmin = false,
            Body = $"Payment proof attached for {merchantId}",
            AttachmentUrl = relPath,
            AttachmentContentType = file.MimeType ?? "image/jpeg",
            CreatedAt = DateTime.UtcNow
        };
        _db.SupportTicketMessages.Add(msg);
        await _db.SaveChangesAsync(ct);

        await ResetIdleAsync(chatId, convo,
            $"✅ Proof received. Ticket {ticket.TicketCode} sent to admins. They will activate your plan if payment is valid.\nTrack it on: {FrontendBaseUrl()}/support.html", ct);
    }

    #endregion

    #region Feedback

    private async Task HandleFeedbackTextAsync(long chatId, TelegramConversation convo, string text, CancellationToken ct)
    {
        var userId = convo.LinkedUserId ?? await TryFindUserByRecentEmailAsync(ct);
        _db.UserFeedbacks.Add(new UserFeedback
        {
            UserId = userId,
            TelegramChatId = chatId,
            Source = "Telegram",
            Category = "Suggestion",
            Message = text.Length > 4000 ? text[..4000] : text,
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await ResetIdleAsync(chatId, convo, "🙏 Thanks! Your feedback was saved for the team.", ct);
    }

    #endregion

    private async Task HandleCallbackAsync(JsonElement cb, CancellationToken ct)
    {
        var id = cb.TryGetProperty("id", out var cid) ? cid.GetString() : null;
        if (!string.IsNullOrWhiteSpace(id))
        {
            await _bot.AnswerCallbackAsync(id, cancellationToken: ct);
        }
    }

    private async Task<string> CreateAndSendOtpAsync(
        User user,
        string recipientEmail,
        string method,
        CancellationToken ct,
        string? pendingEmail = null)
    {
        var rawOtp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var verification = new EmailVerification
        {
            UserId = user.UserId,
            CodeHash = HashString(rawOtp),
            Method = method,
            PendingEmail = pendingEmail,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        _db.EmailVerifications.Add(verification);
        await _db.SaveChangesAsync(ct);
        await _email.SendVerificationEmailAsync(recipientEmail, user.Name, rawOtp, ct);
        return rawOtp;
    }

    private async Task<bool> VerifyOtpAsync(
        int userId,
        string code,
        string method,
        CancellationToken ct,
        string? pendingEmail = null)
    {
        var codeHash = HashString(code.Trim());
        var query = _db.EmailVerifications
            .Where(v => v.UserId == userId && v.Method == method && v.VerifiedAt == null);
        if (!string.IsNullOrWhiteSpace(pendingEmail))
        {
            query = query.Where(v => v.PendingEmail == pendingEmail);
        }

        var verification = await query.OrderByDescending(v => v.CreatedAt).FirstOrDefaultAsync(ct);
        if (verification == null) return false;
        if (verification.ExpiresAt <= DateTime.UtcNow) return false;
        if (verification.Attempts >= 5) return false;

        verification.Attempts += 1;
        if (!string.Equals(verification.CodeHash, codeHash, StringComparison.OrdinalIgnoreCase))
        {
            await _db.SaveChangesAsync(ct);
            return false;
        }

        verification.VerifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<TelegramConversation> GetOrCreateConversationAsync(long chatId, CancellationToken ct)
    {
        var convo = await _db.TelegramConversations.FirstOrDefaultAsync(c => c.ChatId == chatId, ct);
        if (convo != null) return convo;

        convo = new TelegramConversation
        {
            ChatId = chatId,
            State = TelegramBotStates.Idle,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.TelegramConversations.Add(convo);
        await _db.SaveChangesAsync(ct);
        return convo;
    }

    private async Task SetStateAsync(TelegramConversation convo, string state, string? payload, CancellationToken ct)
    {
        convo.State = state;
        convo.PayloadJson = payload;
        convo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ResetIdleAsync(long chatId, TelegramConversation convo, string message, CancellationToken ct)
    {
        await SetStateAsync(convo, TelegramBotStates.Idle, null, ct);
        await SendMenuAsync(chatId, message, ct);
    }

    private async Task SendMenuAsync(long chatId, string preface, CancellationToken ct)
    {
        var markup = JsonSerializer.Serialize(new
        {
            keyboard = new[]
            {
                new[] { new { text = TelegramBotCommands.Restore }, new { text = TelegramBotCommands.ChangeEmail } },
                new[] { new { text = TelegramBotCommands.Help }, new { text = TelegramBotCommands.Payment } },
                new[] { new { text = TelegramBotCommands.Feedback }, new { text = TelegramBotCommands.Cancel } }
            },
            resize_keyboard = true
        });

        await _bot.SendMessageAsync(new TelegramOutgoingMessage(chatId, preface, markup), ct);
    }

    private async Task<int?> ResolveUserForChatAsync(long chatId, TelegramConversation convo, string? text, CancellationToken ct)
    {
        if (convo.LinkedUserId.HasValue) return convo.LinkedUserId;

        if (!string.IsNullOrWhiteSpace(text) &&
            text.StartsWith("email:", StringComparison.OrdinalIgnoreCase))
        {
            var email = text["email:".Length..].Trim().ToLowerInvariant();
            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email && !u.IsDeleted, ct);
            if (user != null)
            {
                convo.LinkedUserId = user.UserId;
                convo.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                return user.UserId;
            }
        }

        return null;
    }

    private Task<int?> TryFindUserByRecentEmailAsync(CancellationToken ct) => Task.FromResult<int?>(null);

    private string FrontendBaseUrl() =>
        (_configuration["Frontend:BaseUrl"] ?? "https://devpilot-ai-three.vercel.app").TrimEnd('/');

    private static JsonElement ParsePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return JsonDocument.Parse("{}").RootElement.Clone();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static string HashString(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
