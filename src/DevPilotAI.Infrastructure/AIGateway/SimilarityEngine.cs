using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.AIGateway;

public partial class SimilarityEngine : ISimilarityEngine
{
    private const int VectorDimensions = 256;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<SimilarityEngine> _logger;

    public SimilarityEngine(IApplicationDbContext dbContext, ILogger<SimilarityEngine> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task<byte[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new byte[VectorDimensions * sizeof(float)]);
        }

        var vector = ComputeFeatureVector(text);
        var bytes = new byte[VectorDimensions * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);

        return Task.FromResult(bytes);
    }

    public async Task<SimilarityMatchResult?> FindBestMatchAsync(
        int currentRequestId,
        string currentText,
        string? framework,
        double threshold = 0.85,
        CancellationToken cancellationToken = default)
    {
        var currentEmbeddingBytes = await GenerateEmbeddingAsync(currentText, cancellationToken);
        var currentVector = BytesToFloats(currentEmbeddingBytes);

        var solvedProblems = await _dbContext.Problems
            .AsNoTracking()
            .Where(p => p.Status == ProblemStatus.Solved)
            .Where(p => p.ProblemSolutions.Any())
            .Include(p => p.ProblemSolutions)
            .Include(p => p.ProblemRequests)
                .ThenInclude(r => r.Embedding)
            .ToListAsync(cancellationToken);

        SimilarityMatchResult? bestMatch = null;
        double highestScore = 0.0;

        foreach (var problem in solvedProblems)
        {
            var successfulSolution = problem.ProblemSolutions
                .OrderByDescending(s => s.SolvedAt)
                .FirstOrDefault();

            if (successfulSolution == null) continue;

            foreach (var req in problem.ProblemRequests)
            {
                if (req.ProblemRequestId == currentRequestId) continue;
                if (req.Embedding == null || req.Embedding.EmbeddingData.Length == 0) continue;

                var candidateVector = BytesToFloats(req.Embedding.EmbeddingData);
                var score = CalculateCosineSimilarityVectors(currentVector, candidateVector);

                if (!string.IsNullOrWhiteSpace(framework) && !string.IsNullOrWhiteSpace(req.Framework))
                {
                    if (string.Equals(framework.Trim(), req.Framework.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Min(1.0, score + 0.03);
                    }
                }

                if (score > highestScore)
                {
                    highestScore = score;
                    if (score >= threshold)
                    {
                        bestMatch = new SimilarityMatchResult(
                            MatchedProblemId: problem.ProblemId,
                            MatchedProblemCode: problem.ProblemCode,
                            MatchedTitle: problem.Title,
                            ProvenSolution: successfulSolution.DetailedSteps,
                            SimilarityScore: Math.Round(score, 4)
                        );
                    }
                }
            }
        }

        if (bestMatch != null)
        {
            _logger.LogInformation("Semantic match identified for Request {RequestId}: Matched Problem {ProblemCode} with score {Score}",
                currentRequestId, bestMatch.MatchedProblemCode, bestMatch.SimilarityScore);
        }

        return bestMatch;
    }

    public double CalculateCosineSimilarity(byte[] embeddingA, byte[] embeddingB)
    {
        if (embeddingA == null || embeddingB == null || embeddingA.Length == 0 || embeddingB.Length == 0)
        {
            return 0.0;
        }

        var vectorA = BytesToFloats(embeddingA);
        var vectorB = BytesToFloats(embeddingB);

        return CalculateCosineSimilarityVectors(vectorA, vectorB);
    }

    private static float[] ComputeFeatureVector(string text)
    {
        var vector = new float[VectorDimensions];
        var tokens = Tokenize(text);

        if (tokens.Count == 0)
        {
            return vector;
        }

        foreach (var token in tokens)
        {
            var hash = ComputeDeterministicHash(token);
            var index = (int)(hash % VectorDimensions);
            var weight = GetTokenWeight(token);
            vector[index] += weight;

            if (token.Length > 4)
            {
                var prefixHash = ComputeDeterministicHash(token[..4]);
                var prefixIndex = (int)(prefixHash % VectorDimensions);
                vector[prefixIndex] += weight * 0.5f;
            }
        }

        var sumSq = 0.0;
        for (int i = 0; i < VectorDimensions; i++)
        {
            sumSq += vector[i] * vector[i];
        }

        var magnitude = Math.Sqrt(sumSq);
        if (magnitude > 1e-6)
        {
            for (int i = 0; i < VectorDimensions; i++)
            {
                vector[i] = (float)(vector[i] / magnitude);
            }
        }

        return vector;
    }

    private static List<string> Tokenize(string text)
    {
        var matches = TokenRegex().Matches(text.ToLowerInvariant());
        var list = new List<string>(matches.Count);
        foreach (Match match in matches)
        {
            if (match.Value.Length > 1)
            {
                list.Add(match.Value);
            }
        }
        return list;
    }

    private static float GetTokenWeight(string token)
    {
        return token switch
        {
            "nullreferenceexception" or "nullreference" or "npe" => 3.5f,
            "unauthorized" or "401" or "forbidden" or "403" => 3.5f,
            "notfound" or "404" or "badrequest" or "400" => 3.0f,
            "stacktrace" or "exception" or "error" or "fatal" => 2.5f,
            "jwt" or "bearer" or "token" or "claim" => 3.0f,
            "dbcontext" or "ef" or "efcore" or "migration" or "sql" => 3.0f,
            "cors" or "headers" or "origin" or "policy" => 3.0f,
            "async" or "task" or "await" or "deadlock" => 2.5f,
            "timeout" or "connection" or "socket" => 2.5f,
            _ => 1.0f
        };
    }

    private static uint ComputeDeterministicHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return BitConverter.ToUInt32(hash, 0);
    }

    private static double CalculateCosineSimilarityVectors(float[] vectorA, float[] vectorB)
    {
        var length = Math.Min(vectorA.Length, vectorB.Length);
        if (length == 0) return 0.0;

        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        for (int i = 0; i < length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        if (denom < 1e-9) return 0.0;

        var score = dotProduct / denom;
        return Math.Clamp(score, 0.0, 1.0);
    }

    private static float[] BytesToFloats(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    [GeneratedRegex(@"[a-zA-Z0-9_\-\.]+")]
    private static partial Regex TokenRegex();
}
