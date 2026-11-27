namespace DevPilotAI.Application.Telegram;

public static class TelegramBotStates
{
    public const string Idle = "Idle";
    public const string RestoreAwaitEmail = "RestoreAwaitEmail";
    public const string RestoreAwaitOtp = "RestoreAwaitOtp";
    public const string EmailChangeAwaitAccountEmail = "EmailChangeAwaitAccountEmail";
    public const string EmailChangeAwaitOldOtp = "EmailChangeAwaitOldOtp";
    public const string EmailChangeAwaitNewEmail = "EmailChangeAwaitNewEmail";
    public const string EmailChangeAwaitNewOtp = "EmailChangeAwaitNewOtp";
    public const string SupportAwaitTicketConfirm = "SupportAwaitTicketConfirm";
    public const string PaymentAwaitCode = "PaymentAwaitCode";
    public const string PaymentAwaitProof = "PaymentAwaitProof";
    public const string FeedbackAwaitText = "FeedbackAwaitText";
}

public static class TelegramBotCommands
{
    public const string Start = "/start";
    public const string Restore = "🔐 Restore account";
    public const string ChangeEmail = "✉️ Change email";
    public const string Help = "❓ Site help";
    public const string Payment = "💳 Payment issue";
    public const string Feedback = "💡 Suggestion";
    public const string Cancel = "✖️ Cancel";
}
