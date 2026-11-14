namespace DevPilotAI.Domain.Entities;

public class ProblemEmbedding
{
    public long EmbeddingId { get; set; }
    public int ProblemRequestId { get; set; }
    public ProblemRequest ProblemRequest { get; set; } = null!;

    public byte[] EmbeddingData { get; set; } = Array.Empty<byte>();
    public string ModelVersion { get; set; } = "default-embedding";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
