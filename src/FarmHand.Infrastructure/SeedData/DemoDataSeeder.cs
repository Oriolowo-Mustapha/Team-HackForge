namespace FarmHand.Infrastructure.SeedData;

public interface IDemoDataSeeder
{
    Task SeedAsync();
}

public class DemoDataSeeder : IDemoDataSeeder
{
    private readonly FarmHandDbContext _dbContext;

    public DemoDataSeeder(FarmHandDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedAsync()
    {
        if (_dbContext.Farms.Any()) return;

        var farmId = Guid.NewGuid();
        var flockId = Guid.NewGuid();

        var farm = new Farm("demo-owner-1", "Mama Funmi Farm", "Odeda, Ogun State, Nigeria");
        farm.GetType().GetProperty("Id")!.SetValue(farm, farmId);

        var flock = new Flock(farmId, "Layers Flock A", "Laying Birds", 500);
        flock.GetType().GetProperty("Id")!.SetValue(flock, flockId);

        var observations = new List<PoultryObservation>();
        for (int i = 7; i >= 1; i--)
        {
            var obs = new PoultryObservation(
                flockId,
                DateTime.UtcNow.AddDays(-i),
                2,
                "normal",
                "normal",
                "10 crates",
                "No unusual behavior");
            obs.Confirm();
            obs.MarkAiExtracted("Seed");
            observations.Add(obs);
        }

        var vetPartner = new VetPartner("Dr. Ade", "Poultry Health", "+234-800-000-0000");
        vetPartner.GetType().GetProperty("Id")!.SetValue(vetPartner, Guid.NewGuid());

        _dbContext.Farms.Add(farm);
        _dbContext.Flocks.Add(flock);
        _dbContext.VetPartners.Add(vetPartner);
        foreach (var obs in observations)
            _dbContext.PoultryObservations.Add(obs);

        await _dbContext.SaveChangesAsync();
    }
}
