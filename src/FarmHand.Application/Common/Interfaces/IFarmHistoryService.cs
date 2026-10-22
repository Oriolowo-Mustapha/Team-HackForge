namespace FarmHand.Application.Common.Interfaces;

public interface IFarmHistoryService
{
    Task<FarmHistoryContext> GetFarmHistoryAsync(Guid farmId, CancellationToken ct = default);
}
