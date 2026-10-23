namespace FarmHand.Domain.ValueObjects;

public record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "NGN";

    public Money(decimal amount, string currency = "NGN")
    {
        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency;
    }

    public static Money Zero => new(0, "NGN");
}
