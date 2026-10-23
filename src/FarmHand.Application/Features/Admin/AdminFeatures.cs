using FarmHand.Application.Common.Exceptions;

namespace FarmHand.Application.Features.Admin;

public record AdminUserDto(
    Guid Id,
    string Email,
    string? FullName,
    string? PhoneNumber,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    Guid? VetPartnerId);

public record AdminFarmDto(Guid Id, string DisplayName, string Location, string OwnerEmail, string? OwnerName, DateTime CreatedAt);

public record PlatformOverviewDto(PlatformOverview Counts, IReadOnlyList<AuditEventDto> RecentEvents, IntegrationStatus Integrations);

internal static class AdminGuard
{
    public static void Ensure(ICurrentUserService currentUser)
    {
        if (!currentUser.IsAdmin)
            throw new UnauthorizedAccessException("Administrator access is required.");
    }
}

public record GetPlatformOverviewQuery : IRequest<PlatformOverviewDto>;

public class GetPlatformOverviewQueryHandler : IRequestHandler<GetPlatformOverviewQuery, PlatformOverviewDto>
{
    private readonly IPlatformStatsRepository _stats;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemStatus _status;

    public GetPlatformOverviewQueryHandler(IPlatformStatsRepository stats, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ISystemStatus status)
    {
        _stats = stats;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _status = status;
    }

    public async Task<PlatformOverviewDto> Handle(GetPlatformOverviewQuery request, CancellationToken ct)
    {
        AdminGuard.Ensure(_currentUser);
        var counts = await _stats.GetOverviewAsync(ct);
        var events = await _unitOfWork.AuditEvents.SearchAsync(new AuditEventSearch(null, null, null, new Paging(1, 12)), ct);
        return new PlatformOverviewDto(
            counts,
            events.Items.Select(e => new AuditEventDto(e.Id, e.ActorId, e.EventType, e.RelatedRecordId, e.Timestamp, e.Details)).ToList(),
            _status.GetIntegrationStatus());
    }
}

public record GetUsersQuery(string? Role, string? Q, bool? Active, int Page = 1, int PageSize = 25) : IRequest<PagedResult<AdminUserDto>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PagedResult<AdminUserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public GetUsersQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AdminUserDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        AdminGuard.Ensure(_currentUser);
        var paging = new Paging(request.Page, request.PageSize);
        var page = await _unitOfWork.Users.SearchAsync(new UserSearch(request.Role, request.Q, request.Active, paging), ct);
        var vets = (await _unitOfWork.VetPartners.GetAllAsync(ct)).Where(v => v.UserId is not null).ToDictionary(v => v.UserId!.Value, v => v.Id);
        var items = page.Items.Select(u => new AdminUserDto(
            u.Id, u.Email, u.FullName, u.PhoneNumber, u.Role, u.IsActive, u.MustChangePassword, u.CreatedAt, u.LastLoginAt,
            vets.TryGetValue(u.Id, out var vetId) ? vetId : null)).ToList();
        return new PagedResult<AdminUserDto>(items, paging.Page, paging.PageSize, page.TotalCount,
            (int)Math.Ceiling(page.TotalCount / (double)paging.PageSize));
    }
}

/// <summary>Activates or deactivates a user. Deactivation revokes all sessions immediately; vet profiles follow their user.</summary>
public record SetUserActiveCommand(Guid UserId, bool Active) : IRequest;

public class SetUserActiveCommandHandler : IRequestHandler<SetUserActiveCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;

    public SetUserActiveCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUser, IAuditLogger audit)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Unit> Handle(SetUserActiveCommand request, CancellationToken ct)
    {
        AdminGuard.Ensure(_currentUser);
        if (request.UserId == _currentUser.UserId && !request.Active)
            throw PublicException.BadRequest("You cannot deactivate your own account.");

        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, ct)
            ?? throw PublicException.NotFound("User not found.");
        var vet = await _unitOfWork.VetPartners.GetByUserIdAsync(user.Id, ct);

        if (request.Active)
        {
            user.Activate();
            vet?.Activate();
        }
        else
        {
            user.Deactivate();
            vet?.Deactivate();
            await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(user.Id, ct);
        }

        await _audit.RecordAsync(request.Active ? "admin.user_activated" : "admin.user_deactivated", user.Id, user.Email, ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public record GetAdminFarmsQuery(string? Q, int Page = 1, int PageSize = 25) : IRequest<PagedResult<AdminFarmDto>>;

public class GetAdminFarmsQueryHandler : IRequestHandler<GetAdminFarmsQuery, PagedResult<AdminFarmDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public GetAdminFarmsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AdminFarmDto>> Handle(GetAdminFarmsQuery request, CancellationToken ct)
    {
        AdminGuard.Ensure(_currentUser);
        var paging = new Paging(request.Page, request.PageSize);
        var page = await _unitOfWork.Farms.ListAllAsync(request.Q, paging, ct);
        var owners = (await _unitOfWork.Users.GetByIdsAsync(page.Items.Select(f => Guid.TryParse(f.OwnerId, out var id) ? id : Guid.Empty), ct))
            .ToDictionary(u => u.Id.ToString(), StringComparer.OrdinalIgnoreCase);
        var items = page.Items.Select(f =>
        {
            owners.TryGetValue(f.OwnerId, out var owner);
            return new AdminFarmDto(f.Id, f.DisplayName, f.Location, owner?.Email ?? "unknown", owner?.FullName, f.CreatedAt);
        }).ToList();
        return new PagedResult<AdminFarmDto>(items, paging.Page, paging.PageSize, page.TotalCount,
            (int)Math.Ceiling(page.TotalCount / (double)paging.PageSize));
    }
}
