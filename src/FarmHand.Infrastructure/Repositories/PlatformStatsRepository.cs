using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class PlatformStatsRepository : IPlatformStatsRepository
{
    private readonly FarmHandDbContext _db;

    public PlatformStatsRepository(FarmHandDbContext db)
    {
        _db = db;
    }

    public async Task<PlatformOverview> GetOverviewAsync(CancellationToken ct = default)
    {
        var usersByRole = await _db.Users.GroupBy(u => u.Role)
            .Select(g => new CountByKey(g.Key, g.Count())).ToListAsync(ct);
        var activeUsers = await _db.Users.CountAsync(u => u.IsActive, ct);
        var farms = await _db.Farms.CountAsync(ct);
        var animals = await _db.LivestockAnimals.CountAsync(a => a.IsActive, ct);
        var flockHeadcount = await _db.LivestockCategories.SumAsync(c => (int?)c.AnimalCount, ct) ?? 0;

        var observations = (await _db.PoultryObservations.GroupBy(o => o.ConfirmationStatus)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new CountByKey(x.Key.ToString(), x.Count)).ToList();
        var incidentsByStatus = (await _db.Incidents.GroupBy(i => i.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new CountByKey(x.Key.ToString(), x.Count)).ToList();
        var incidentsBySeverity = (await _db.Incidents.GroupBy(i => i.Severity)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new CountByKey(x.Key.ToString(), x.Count)).ToList();
        var requests = (await _db.VetRequests.GroupBy(v => v.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new CountByKey(x.Key.ToString(), x.Count)).ToList();
        var payments = await _db.Payments.GroupBy(p => p.Status)
            .Select(g => new CountByKey(g.Key, g.Count())).ToListAsync(ct);

        var completed = await _db.Payments.Where(p => p.Status == PaymentStatus.Completed)
            .Select(p => new { p.Mode, Total = p.TotalAmount.Amount, Commission = p.PlatformCommission.Amount })
            .ToListAsync(ct);

        return new PlatformOverview(
            usersByRole,
            activeUsers,
            farms,
            animals,
            flockHeadcount,
            observations,
            incidentsByStatus,
            incidentsBySeverity,
            requests,
            payments,
            completed.Where(p => p.Mode == PaymentMode.Live).Sum(p => p.Total),
            completed.Where(p => p.Mode == PaymentMode.Simulated).Sum(p => p.Total),
            completed.Where(p => p.Mode == PaymentMode.Live).Sum(p => p.Commission));
    }
}
