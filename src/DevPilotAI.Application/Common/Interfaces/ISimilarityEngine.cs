namespace DevPilotAI.Application.Common.Interfaces;

public record SimilarityMatchResult(
    int MatchedProblemId,
    string MatchedProblemCode,
    string MatchedTitle,
    string ProvenSolution,
    double SimilarityScore
);

public interface ISimilarityEngine
{
    Task<byte[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<SimilarityMatchResult?> FindBestMatchAsync(int currentRequestId, string currentText, string? framework, double threshold = 0.85, CancellationToken cancellationToken = default);
    double CalculateCosineSimilarity(byte[] embeddingA, byte[] embeddingB);
}
