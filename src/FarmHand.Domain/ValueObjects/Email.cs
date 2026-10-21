namespace FarmHand.Domain.ValueObjects;

public record Email
{
    public string Value { get; init; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains("@"))
            throw new ArgumentException("Invalid email address.", nameof(value));

        Value = value.Trim().ToLowerInvariant();
    }
}
