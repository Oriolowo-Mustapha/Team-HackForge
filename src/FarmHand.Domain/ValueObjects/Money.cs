namespace FarmHand.Domain.ValueObjects;

public record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "NGN";

    public Money(decimal amount, string currency = "NGN")
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero => new(0, "NGN");
}
