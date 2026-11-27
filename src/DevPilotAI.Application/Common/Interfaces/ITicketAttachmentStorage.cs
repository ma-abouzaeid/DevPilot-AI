namespace DevPilotAI.Application.Common.Interfaces;

public interface ITicketAttachmentStorage
{
    Task<string> SaveAsync(byte[] bytes, string? contentType, CancellationToken cancellationToken = default);
}
