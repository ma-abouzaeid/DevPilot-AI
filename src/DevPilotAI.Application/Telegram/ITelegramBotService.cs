using System.Text.Json;

namespace DevPilotAI.Application.Telegram;

public interface ITelegramBotService
{
    Task HandleUpdateAsync(JsonElement update, CancellationToken cancellationToken = default);
}
