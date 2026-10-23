namespace FarmHand.Domain.Entities;

public class Farm : BaseEntity
{
    public string OwnerId { get; protected set; } = string.Empty;
    public string DisplayName { get; protected set; } = string.Empty;
    public string Location { get; protected set; } = string.Empty;
    public double? Latitude { get; protected set; }
    public double? Longitude { get; protected set; }
    public string? CityOrTown { get; protected set; }
    public string? State { get; protected set; }
    public string? Country { get; protected set; } = "Nigeria";
    public string? LocationSource { get; protected set; }

    protected Farm() { }

    public Farm(
        string ownerId,
        string displayName,
        string location,
        double? latitude = null,
        double? longitude = null,
        string? cityOrTown = null,
        string? state = null,
        string? country = null,
        string? locationSource = null)
    {
        OwnerId = ownerId;
        DisplayName = displayName;
        Location = location;
        Latitude = latitude;
        Longitude = longitude;
        CityOrTown = cityOrTown;
        State = state;
        Country = string.IsNullOrWhiteSpace(country) ? "Nigeria" : country;
        LocationSource = locationSource;
    }

    public void UpdateDetails(
        string displayName,
        string location,
        double? latitude = null,
        double? longitude = null,
        string? cityOrTown = null,
        string? state = null,
        string? country = null,
        string? locationSource = null)
    {
        DisplayName = displayName;
        Location = location;
        Latitude = latitude;
        Longitude = longitude;
        CityOrTown = cityOrTown;
        State = state;
        if (!string.IsNullOrWhiteSpace(country))
        {
            Country = country;
        }
        if (!string.IsNullOrWhiteSpace(locationSource))
        {
            LocationSource = locationSource;
        }
        MarkUpdated();
    }
}
