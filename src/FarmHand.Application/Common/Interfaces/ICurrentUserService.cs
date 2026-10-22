namespace FarmHand.Application.Common.Interfaces;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    bool IsAdmin { get; }
    bool IsVet { get; }
}
