namespace FarmHand.Domain.Entities;

public class LivestockRecord : BaseEntity
{
    public Guid FarmId { get; protected set; }
    public string Species { get; protected set; } = string.Empty;
    public string AnimalName { get; protected set; } = string.Empty;
    public string Sex { get; protected set; } = string.Empty;
    public int? ApproximateAge { get; protected set; }
    public DateTime? MatingDate { get; protected set; }
    public DateTime? EstimatedDueDate { get; protected set; }
    public string? Notes { get; protected set; }

    protected LivestockRecord() { }

    public LivestockRecord(
        Guid farmId,
        string species,
        string animalName,
        string sex,
        int? approximateAge = null)
    {
        FarmId = farmId;
        Species = species;
        AnimalName = animalName;
        Sex = sex;
        ApproximateAge = approximateAge;
    }

    public void RecordMating(DateTime matingDate, DateTime? estimatedDueDate = null)
    {
        MatingDate = matingDate;
        EstimatedDueDate = estimatedDueDate;
        MarkUpdated();
    }
}
