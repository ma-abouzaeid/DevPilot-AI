using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Plans.DTOs;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Plans;

public class CreditCostService : ICreditCostService
{
    private readonly IApplicationDbContext _db;

    public CreditCostService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetCostAsync(string actionKey, CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(actionKey);
        try
        {
            var row = await _db.CreditActionCosts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.ActionKey == key, cancellationToken);
            if (row != null)
            {
                return Math.Max(0, row.Credits);
            }
        }
        catch
        {
            // Table may not exist yet on older environments — fall back to defaults.
        }

        return CreditCosts.ForTaskType(key);
    }

    public async Task<IReadOnlyList<CreditCostItemDto>> ListPublicAsync(CancellationToken cancellationToken = default)
    {
        var all = await ListAllAsync(cancellationToken);
        return all.Where(c => c.ShowOnPlansPage).ToList();
    }

    public async Task<IReadOnlyList<CreditCostItemDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _db.CreditActionCosts.AsNoTracking()
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.ActionKey)
                .ToListAsync(cancellationToken);

            if (rows.Count > 0)
            {
                return rows.Select(ToDto).ToList();
            }
        }
        catch
        {
            // fall through to defaults
        }

        return DefaultRows().Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<CreditCostItemDto>> UpdateAsync(
        IReadOnlyList<UpdateCreditCostItemDto> items,
        CancellationToken cancellationToken = default)
    {
        if (items == null || items.Count == 0)
        {
            throw AppError.InvalidRequest("At least one credit cost item is required.", "INVALID_REQUEST");
        }

        await EnsureSeededAsync(cancellationToken);

        foreach (var item in items)
        {
            var key = NormalizeKey(item.ActionKey);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (item.Credits < 0)
            {
                throw AppError.InvalidRequest($"Credits for '{key}' cannot be negative.", "INVALID_REQUEST", nameof(item.Credits));
            }

            var row = await _db.CreditActionCosts
                .FirstOrDefaultAsync(c => c.ActionKey == key, cancellationToken)
                ?? throw AppError.NotFound($"Credit cost '{key}' was not found.");

            row.Credits = item.Credits;
            if (item.ShowOnPlansPage.HasValue)
            {
                row.ShowOnPlansPage = item.ShowOnPlansPage.Value;
            }

            row.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await ListAllAsync(cancellationToken);
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.CreditActionCosts.Select(c => c.ActionKey).ToListAsync(cancellationToken);
        foreach (var seed in DefaultRows())
        {
            if (existing.Contains(seed.ActionKey, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            _db.CreditActionCosts.Add(seed);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeKey(string? actionKey) =>
        (actionKey ?? string.Empty).Trim();

    private static CreditCostItemDto ToDto(CreditActionCost row) =>
        new(row.ActionKey, row.DisplayName, row.Credits, row.ShowOnPlansPage, row.SortOrder);

    private static List<CreditActionCost> DefaultRows()
    {
        var now = DateTime.UtcNow;
        return
        [
            new CreditActionCost { ActionKey = "Debugging", DisplayName = "AI Debug", Credits = CreditCosts.Debugging, ShowOnPlansPage = true, SortOrder = 1, UpdatedAt = now },
            new CreditActionCost { ActionKey = "KnowledgeAdaptation", DisplayName = "Knowledge reuse", Credits = CreditCosts.KnowledgeAdaptation, ShowOnPlansPage = true, SortOrder = 2, UpdatedAt = now },
            new CreditActionCost { ActionKey = "ChallengeEvaluation", DisplayName = "Challenge evaluate", Credits = CreditCosts.ChallengeEvaluation, ShowOnPlansPage = true, SortOrder = 3, UpdatedAt = now },
            new CreditActionCost { ActionKey = "CodeReview", DisplayName = "Code Review", Credits = CreditCosts.CodeReview, ShowOnPlansPage = true, SortOrder = 4, UpdatedAt = now },
            new CreditActionCost { ActionKey = "ChallengeGeneration", DisplayName = "Challenge generation", Credits = CreditCosts.ChallengeGeneration, ShowOnPlansPage = false, SortOrder = 5, UpdatedAt = now }
        ];
    }
}
