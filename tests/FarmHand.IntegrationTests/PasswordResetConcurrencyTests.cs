using FarmHand.Domain.Entities;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.IntegrationTests;

public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
            Skip = "Set ConnectionStrings__DefaultConnection to an isolated PostgreSQL test database.";
    }
}

public class PasswordResetConcurrencyTests
{
    [DatabaseFact]
    public async Task ConcurrentReset_ConsumesTokenOnceAndRollsBackTheLosingPasswordChange()
    {
        var options = new DbContextOptionsBuilder<FarmHandDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")).Options;
        await using var setup = new FarmHandDbContext(options);
        await setup.Database.MigrateAsync();
        var user = new User($"reset-{Guid.NewGuid():N}@example.invalid", "initial-test-hash");
        var token = new PasswordResetToken(user.Id, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddMinutes(5));
        setup.Users.Add(user);
        setup.PasswordResetTokens.Add(token);
        await setup.SaveChangesAsync();
        try
        {
            await using var first = new FarmHandDbContext(options);
            await using var second = new FarmHandDbContext(options);
            var firstToken = await first.PasswordResetTokens.SingleAsync(t => t.Id == token.Id);
            var secondToken = await second.PasswordResetTokens.SingleAsync(t => t.Id == token.Id);
            var firstUser = await first.Users.SingleAsync(u => u.Id == user.Id);
            var secondUser = await second.Users.SingleAsync(u => u.Id == user.Id);
            firstToken.MarkUsed();
            firstUser.ChangePassword("winning-password-hash");
            await first.SaveChangesAsync();
            secondToken.MarkUsed();
            secondUser.ChangePassword("losing-password-hash");
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
            setup.ChangeTracker.Clear();
            Assert.Equal("winning-password-hash", (await setup.Users.SingleAsync(u => u.Id == user.Id)).PasswordHash);
        }
        finally
        {
            setup.ChangeTracker.Clear();
            await setup.PasswordResetTokens.Where(t => t.Id == token.Id).ExecuteDeleteAsync();
            await setup.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        }
    }
}
