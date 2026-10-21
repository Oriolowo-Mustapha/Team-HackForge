namespace FarmHand.Domain.Entities;

public class Flock : BaseEntity
{
    public Guid FarmId { get; protected set; }
    public string FlockName { get; protected set; } = string.Empty;
    public string BirdType { get; protected set; } = string.Empty;
    public int CurrentRecordedPopulation { get; protected set; }

    protected Flock() { }

    public Flock(Guid farmId, string flockName, string birdType, int currentRecordedPopulation)
    {
        FarmId = farmId;
        FlockName = flockName;
        BirdType = birdType;
        CurrentRecordedPopulation = currentRecordedPopulation;
    }

    public void UpdatePopulation(int population)
    {
        CurrentRecordedPopulation = population;
        MarkUpdated();
    }
}
