using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;

namespace DevPilotAI.Infrastructure.Storage;

public class TicketAttachmentStorage : ITicketAttachmentStorage
{
    private readonly IHostEnvironment _env;

    public TicketAttachmentStorage(IHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> SaveAsync(byte[] bytes, string? contentType, CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(_env.ContentRootPath, "App_Data", "ticket-attachments");
        Directory.CreateDirectory(root);
        var mime = contentType ?? "image/jpeg";
        var ext = mime.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png"
            : mime.Contains("webp", StringComparison.OrdinalIgnoreCase) ? ".webp"
            : ".jpg";
        var name = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{ext}";
        await File.WriteAllBytesAsync(Path.Combine(root, name), bytes, cancellationToken);
        return $"/ticket-attachments/{name}";
    }
}
