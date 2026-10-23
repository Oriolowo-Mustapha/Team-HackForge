namespace FarmHand.Domain.Interfaces;

/// <summary>Read-only aggregate counts for the administrator overview.</summary>
public interface IPlatformStatsRepository
{
    Task<PlatformOverview> GetOverviewAsync(CancellationToken ct = default);
}
