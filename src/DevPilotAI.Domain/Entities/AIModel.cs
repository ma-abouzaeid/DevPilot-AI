namespace DevPilotAI.Domain.Entities;

public class AIModel
{
    public int AIModelId { get; set; }
    public int AIProviderId { get; set; }
    public AIProvider Provider { get; set; } = null!;

    public string ModelName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int MaxContextTokens { get; set; } = 8192;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AIRequest> Requests { get; set; } = new List<AIRequest>();
}
