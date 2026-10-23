using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FarmHand.Application.Features.VetCoordination;

/// <summary>Creates a vet request for an incident owned by the caller.</summary>
/// <param name="IncidentId">Incident id; must match the route value.</param>
/// <param name="RequestType">Coordination model, sent as an integer (0-3): RemoteReview, SharedVisit, CooperativePlan, or ClinicVisit.</param>
/// <param name="FarmerCost">Farmer's share of the cost, in NGN.</param>
/// <param name="VetPartnerId">Optional preferred vet; enables automatic payout splitting. When omitted the request is broadcast to all active vets.</param>
public record CreateVetRequestCommand(
    Guid IncidentId,
    RequestType RequestType,
    decimal FarmerCost,
    Guid? VetPartnerId = null) : IRequest<Guid>;

public class CreateVetRequestCommandHandler : IRequestHandler<CreateVetRequestCommand, Guid>
{
    private readonly IVetRequestRepository _vetRequestRepository;
    private readonly IVetPartnerRepository _vetPartnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly IEmailService _emailService;
    private readonly ILogger<CreateVetRequestCommandHandler> _logger;

    public CreateVetRequestCommandHandler(
        IVetRequestRepository vetRequestRepository,
        IVetPartnerRepository vetPartnerRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        IEmailService emailService,
        ILogger<CreateVetRequestCommandHandler> logger)
    {
        _vetRequestRepository = vetRequestRepository;
        _vetPartnerRepository = vetPartnerRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateVetRequestCommand request, CancellationToken ct)
    {
        await _authorization.EnsureIncidentAccessAsync(request.IncidentId, ct);

        if (request.VetPartnerId is { } vetPartnerId)
        {
            var vet = await _unitOfWork.VetPartners.GetByIdAsync(vetPartnerId, ct);
            if (vet is null || !vet.IsActive)
                throw PublicException.BadRequest("The selected vet is not available.");
        }

        var vetRequest = new VetRequest(
            request.IncidentId,
            request.RequestType,
            new Domain.ValueObjects.Money(request.FarmerCost),
            request.VetPartnerId);

        await _vetRequestRepository.AddAsync(vetRequest, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await NotifyVetsAsync(vetRequest, ct);

        return vetRequest.Id;
    }

    /// <summary>
    /// Best-effort email notification: a targeted request informs only its preferred vet;
    /// an untargeted request broadcasts to every active vet. Failure never rejects the request.
    /// </summary>
    private async Task NotifyVetsAsync(VetRequest vetRequest, CancellationToken ct)
    {
        if (!_emailService.IsEnabled)
            return;

        try
        {
            var allVets = await _vetPartnerRepository.GetAllAsync(ct);
            var recipients = vetRequest.VetPartnerId is { } assignedVetId
                ? allVets.Where(v => v.Id == assignedVetId && v.IsActive).Select(v => v.Email)
                : allVets.Where(v => v.IsActive).Select(v => v.Email);

            var subject = $"New FarmHand vet request: {vetRequest.RequestType}";
            foreach (var email in recipients)
            {
                await _emailService.SendAsync(new EmailMessage(
                    email,
                    subject,
                    BuildHtmlBody(vetRequest)), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify vets about vet request {VetRequestId}", vetRequest.Id);
        }
    }

    private static string BuildHtmlBody(VetRequest vetRequest) =>
        $"""
        <p>A new vet request requires your attention.</p>
        <table>
        <tr><td>Request id</td><td>{vetRequest.Id}</td></tr>
        <tr><td>Incident id</td><td>{vetRequest.IncidentId}</td></tr>
        <tr><td>Type</td><td>{vetRequest.RequestType}</td></tr>
        <tr><td>Farmer cost</td><td>{vetRequest.FarmerCost.Amount:N2} {vetRequest.FarmerCost.Currency}</td></tr>
        <tr><td>Status</td><td>{vetRequest.Status}</td></tr>
        </table>
        <p>Log in to FarmHand to view the incident details and accept the request. The first vet to accept an unassigned request is assigned to it.</p>
        """;
}

public class CreateVetRequestCommandValidator : AbstractValidator<CreateVetRequestCommand>
{
    public CreateVetRequestCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEqual(Guid.Empty);
        RuleFor(x => x.RequestType).IsInEnum();
        RuleFor(x => x.FarmerCost).GreaterThanOrEqualTo(0);
    }
}
