using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.Payments;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FarmHand.Application.Features.VetCoordination;

/// <summary>Creates a vet request for an incident owned by the caller.</summary>
/// <param name="IncidentId">Incident id; must match the route value.</param>
/// <param name="RequestType">Coordination model, sent as an integer (0-3): RemoteReview, SharedVisit, CooperativePlan, or ClinicVisit.</param>
/// <param name="VetPartnerId">Optional preferred vet. When omitted the request is open to all active vets.</param>
/// <param name="Description">What the farmer needs help with.</param>
/// <param name="PreferredVisitDate">Optional preferred date for a visit.</param>
/// <remarks>The consultation fee is a fixed platform price (<see cref="PaymentPolicy.FixedConsultationFee"/>, in NGN) — callers do not offer their own amount.</remarks>
public record CreateVetRequestCommand(
    Guid IncidentId,
    RequestType RequestType,
    Guid? VetPartnerId = null,
    string? Description = null,
    DateTime? PreferredVisitDate = null) : IRequest<Guid>;

public class CreateVetRequestCommandHandler : IRequestHandler<CreateVetRequestCommand, Guid>
{
    private readonly IVetRequestRepository _vetRequestRepository;
    private readonly IVetPartnerRepository _vetPartnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _emailService;
    private readonly IAuditLogger _audit;
    private readonly ILogger<CreateVetRequestCommandHandler> _logger;

    public CreateVetRequestCommandHandler(
        IVetRequestRepository vetRequestRepository,
        IVetPartnerRepository vetPartnerRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        ICurrentUserService currentUser,
        IEmailService emailService,
        IAuditLogger audit,
        ILogger<CreateVetRequestCommandHandler> logger)
    {
        _vetRequestRepository = vetRequestRepository;
        _vetPartnerRepository = vetPartnerRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _currentUser = currentUser;
        _emailService = emailService;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateVetRequestCommand request, CancellationToken ct)
    {
        var incident = await _unitOfWork.Incidents.GetByIdAsync(request.IncidentId, ct)
            ?? throw PublicException.NotFound("Case not found.");

        // Only the farm owner (or an admin) can ask for veterinary help on a case.
        await _authorization.EnsureFarmAccessAsync(incident.FarmId, ct);

        if (incident.IsClosed)
            throw PublicException.Conflict("This case is resolved. Reopen it before requesting a vet.");

        var existing = await _vetRequestRepository.GetByIncidentIdAsync(incident.Id, ct);
        if (existing.Any(r => r.IsActive))
            throw PublicException.Conflict("This case already has an active vet request. Cancel it before creating another.");

        if (request.VetPartnerId is { } vetPartnerId)
        {
            var vet = await _unitOfWork.VetPartners.GetByIdAsync(vetPartnerId, ct);
            if (vet is null || !vet.IsActive)
                throw PublicException.BadRequest("The selected vet is not available.");
        }

        var vetRequest = new VetRequest(
            request.IncidentId,
            request.RequestType,
            new Domain.ValueObjects.Money(PaymentPolicy.FixedConsultationFee),
            request.VetPartnerId,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.PreferredVisitDate,
            _currentUser.UserId);

        await _vetRequestRepository.AddAsync(vetRequest, ct);
        await _audit.RecordAsync("vet_request.created", vetRequest.Id,
            $"incident={incident.Id}; targeted={(request.VetPartnerId is null ? "no" : "yes")}; cost={PaymentPolicy.FixedConsultationFee:0.00}", ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await NotifyVetsAsync(vetRequest, incident, ct);

        return vetRequest.Id;
    }

    /// <summary>
    /// Best-effort email notification: a targeted request informs only its preferred vet;
    /// an untargeted request broadcasts to every active vet. Failure never rejects the request.
    /// </summary>
    private async Task NotifyVetsAsync(VetRequest vetRequest, Incident incident, CancellationToken ct)
    {
        if (!_emailService.IsEnabled)
            return;

        try
        {
            var allVets = await _vetPartnerRepository.GetActiveAsync(ct);
            var recipients = vetRequest.VetPartnerId is { } assignedVetId
                ? allVets.Where(v => v.Id == assignedVetId).Select(v => v.Email)
                : allVets.Select(v => v.Email);

            var subject = $"New FarmHand case: {incident.Severity} severity";
            foreach (var email in recipients)
            {
                await _emailService.SendAsync(new EmailMessage(email, subject, BuildHtmlBody(vetRequest, incident)), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify vets about vet request {VetRequestId}", vetRequest.Id);
        }
    }

    private static string BuildHtmlBody(VetRequest vetRequest, Incident incident) =>
        $"""
        <p>A farmer on FarmHand has asked for veterinary help.</p>
        <table>
        <tr><td>Case</td><td>{System.Net.WebUtility.HtmlEncode(incident.IncidentType)} ({incident.Severity} severity)</td></tr>
        <tr><td>Service</td><td>{vetRequest.RequestType}</td></tr>
        <tr><td>Consultation fee</td><td>{vetRequest.FarmerCost.Amount:N2} {vetRequest.FarmerCost.Currency}</td></tr>
        </table>
        <p>Sign in to FarmHand to review the case summary. The first vet to accept an open request is assigned to it.</p>
        """;
}

public class CreateVetRequestCommandValidator : AbstractValidator<CreateVetRequestCommand>
{
    public CreateVetRequestCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEqual(Guid.Empty);
        RuleFor(x => x.RequestType).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.PreferredVisitDate)
            .GreaterThanOrEqualTo(_ => DateTime.UtcNow.Date)
            .When(x => x.PreferredVisitDate is not null)
            .WithMessage("The preferred visit date cannot be in the past.");
    }
}
