namespace FarmHand.Application.Common.Interfaces;

public record CostShareOption(
    RequestType Type,
    string Label,
    decimal FarmerCost,
    string Description);

public interface ICostShareCalculator
{
    IEnumerable<CostShareOption> GetOptions();
}
