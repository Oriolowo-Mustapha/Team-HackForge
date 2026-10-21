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
        var poultryCategoryId = Guid.NewGuid();
        var goatCategoryId = Guid.NewGuid();
        var henId = Guid.NewGuid();
        var goatId = Guid.NewGuid();

        var farm = new Farm("demo-owner-1", "Mama Funmi Farm", "Odeda, Ogun State, Nigeria");
        farm.GetType().GetProperty("Id")!.SetValue(farm, farmId);

        var poultryCategory = new LivestockCategory(farmId, "Poultry", "Laying birds flock");
        poultryCategory.GetType().GetProperty("Id")!.SetValue(poultryCategory, poultryCategoryId);

        var goatCategory = new LivestockCategory(farmId, "Goats", "Dairy goats");
        goatCategory.GetType().GetProperty("Id")!.SetValue(goatCategory, goatCategoryId);

        var hen = new LivestockAnimal(farmId, poultryCategoryId, "Hen #1", "Chicken", "Layer", 18, "Female", "Laying hen");
        hen.GetType().GetProperty("Id")!.SetValue(hen, henId);

        var adunni = new LivestockAnimal(farmId, goatCategoryId, "Adunni", "Goat", "West African Dwarf", 24, "Female", "Breeding doe");
        adunni.GetType().GetProperty("Id")!.SetValue(adunni, goatId);

        var observations = new List<PoultryObservation>();
        for (int i = 7; i >= 1; i--)
        {
            var obs = new PoultryObservation(
                henId,
                poultryCategoryId,
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

        // Seed admin user
        var adminUser = new User("admin@farmhand.ai", "$2a$11$hashedpassword", "Admin", "System Admin", "+234-800-000-0001", farmId);
        adminUser.GetType().GetProperty("Id")!.SetValue(adminUser, Guid.NewGuid());

        _dbContext.Users.Add(adminUser);
        _dbContext.Farms.Add(farm);
        _dbContext.LivestockCategories.Add(poultryCategory);
        _dbContext.LivestockCategories.Add(goatCategory);
        _dbContext.LivestockAnimals.Add(hen);
        _dbContext.LivestockAnimals.Add(adunni);
        _dbContext.VetPartners.Add(vetPartner);
        foreach (var obs in observations)
            _dbContext.PoultryObservations.Add(obs);

        await _dbContext.SaveChangesAsync();
    }
}
