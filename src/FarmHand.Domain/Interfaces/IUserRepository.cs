namespace FarmHand.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<IEnumerable<User>> GetByRoleAsync(string role, CancellationToken ct = default);
}
