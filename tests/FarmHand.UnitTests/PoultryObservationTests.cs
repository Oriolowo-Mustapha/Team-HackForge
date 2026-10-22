using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;

namespace FarmHand.UnitTests;

public class PoultryObservationTests
{
    private static PoultryObservation CreateObservation(string? notes = "Initial note") =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            DateTime.UtcNow,
            2,
            "20L",
            "10kg",
            null,
            notes);

    [Fact]
    public void NewObservation_IsPending()
    {
        var observation = CreateObservation();

        Assert.Equal(ObservationConfirmationStatus.Pending, observation.ConfirmationStatus);
    }

    [Fact]
    public void Confirm_WithCorrectedNotes_ReplacesFarmerNotes()
    {
        var observation = CreateObservation();

        observation.Confirm("Corrected note");

        Assert.Equal(ObservationConfirmationStatus.Confirmed, observation.ConfirmationStatus);
        Assert.Equal("Corrected note", observation.FarmerNotes);
    }

    [Fact]
    public void Confirm_WithoutCorrectedNotes_PreservesFarmerNotes()
    {
        var observation = CreateObservation();

        observation.Confirm();

        Assert.Equal("Initial note", observation.FarmerNotes);
    }

    [Fact]
    public void SetImageAnalysis_StoresImageAndAnalysis()
    {
        var observation = CreateObservation();

        observation.SetImageAnalysis("https://example.test/image.jpg", "Healthy flock");

        Assert.Equal("https://example.test/image.jpg", observation.ImageUrl);
        Assert.Equal("Healthy flock", observation.VisionAnalysis);
    }
}
