using FarmHand.Domain.Entities;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FarmHand.Infrastructure.SeedData;

public interface IDemoDataSeeder
{
    Task SeedAsync();
}

public class DemoDataSeeder : IDemoDataSeeder
{
    private readonly FarmHandDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        FarmHandDbContext dbContext,
        IConfiguration configuration,
        ILogger<DemoDataSeeder> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var password = _configuration["Seed:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Seed:Password is required for development seeding. Set a strong, non-placeholder password in appsettings or user secrets.");

        var adminEmail = (_configuration["Seed:AdminEmail"] ?? "admin@farmhand.ai").Trim().ToLowerInvariant();
        var farmerEmail = (_configuration["Seed:FarmerEmail"] ?? "farmer@farmhand.ai").Trim().ToLowerInvariant();
        var vetEmail = (_configuration["Seed:VetEmail"] ?? "vet@farmhand.ai").Trim().ToLowerInvariant();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        var admin = await EnsureUserAsync(adminEmail, password, passwordHash, "Admin", "System Admin", "+234-800-000-0001");
        var farmer = await EnsureUserAsync(farmerEmail, password, passwordHash, "Farmer", "Mama Funmi", "+234-800-000-0002");
        var vetUser = await EnsureUserAsync(vetEmail, password, passwordHash, "Vet", "Dr. Ade", "+234-800-000-0003");

        var ownerId = farmer.Id.ToString();
        var farm = await _dbContext.Farms.FirstOrDefaultAsync(f => f.OwnerId == ownerId);
        if (farm is null)
        {
            farm = new Farm(ownerId, "Mama Funmi Farm", "Odeda, Ogun State, Nigeria");
            _dbContext.Farms.Add(farm);
            _logger.LogInformation("Seeding demo farm {FarmId}", farm.Id);
        }

        if (farmer.FarmId != farm.Id)
        {
            farmer.AssignFarm(farm.Id);
        }

        var poultryCategory = await EnsureCategoryAsync(farm.Id, "Poultry", "Laying birds flock");
        var goatCategory = await EnsureCategoryAsync(farm.Id, "Goats", "Dairy goats");

        var hen = await EnsureAnimalAsync(
            farm.Id, poultryCategory.Id, "Hen #1", "Chicken", "Layer", 18, "Female", "Laying hen");
        await EnsureAnimalAsync(
            farm.Id, goatCategory.Id, "Adunni", "Goat", "West African Dwarf", 24, "Female", "Breeding doe");

        var hasObservations = await _dbContext.PoultryObservations.AnyAsync(o => o.FarmId == farm.Id);
        if (!hasObservations)
        {
            for (var i = 7; i >= 1; i--)
            {
                var observation = new PoultryObservation(
                    farm.Id,
                    hen.Id,
                    poultryCategory.Id,
                    DateTime.UtcNow.AddDays(-i),
                    2,
                    "normal",
                    "normal",
                    "10 crates",
                    "No unusual behavior");
                observation.Confirm();
                observation.MarkAiExtracted("Seed");
                _dbContext.PoultryObservations.Add(observation);
            }
        }

        var vetPartner = await _dbContext.VetPartners.FirstOrDefaultAsync(v => v.UserId == vetUser.Id);
        if (vetPartner is null)
        {
            _dbContext.VetPartners.Add(new VetPartner(
                vetUser.Id,
                "Dr. Ade",
                vetEmail,
                "Poultry Health",
                "+234-800-000-0003"));
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task<User> EnsureUserAsync(
        string email,
        string password,
        string passwordHash,
        string role,
        string fullName,
        string phoneNumber)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            user = new User(email, passwordHash, role, fullName, phoneNumber);
            _dbContext.Users.Add(user);
            _logger.LogInformation("Seeding demo {Role} user {Email}", role, email);
            return user;
        }

        // Repair users created by the old seeder with an invalid hash.
        var hashIsValid = false;
        try
        {
            hashIsValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch (Exception)
        {
            hashIsValid = false;
        }

        if (!hashIsValid)
        {
            user.ChangePassword(BCrypt.Net.BCrypt.HashPassword(password));
            _dbContext.Users.Update(user);
        }

        return user;
    }

    private async Task<LivestockCategory> EnsureCategoryAsync(Guid farmId, string name, string description)
    {
        var category = await _dbContext.LivestockCategories
            .FirstOrDefaultAsync(c => c.FarmId == farmId && c.Name == name);

        if (category is not null) return category;

        category = new LivestockCategory(farmId, name, description);
        _dbContext.LivestockCategories.Add(category);
        return category;
    }

    private async Task<LivestockAnimal> EnsureAnimalAsync(
        Guid farmId,
        Guid categoryId,
        string name,
        string species,
        string? breed,
        int? ageMonths,
        string? gender,
        string? notes)
    {
        var animal = await _dbContext.LivestockAnimals
            .FirstOrDefaultAsync(a => a.FarmId == farmId && a.Name == name);

        if (animal is not null) return animal;

        animal = new LivestockAnimal(farmId, categoryId, name, species, breed, ageMonths, gender, notes);
        _dbContext.LivestockAnimals.Add(animal);
        return animal;
    }
}
