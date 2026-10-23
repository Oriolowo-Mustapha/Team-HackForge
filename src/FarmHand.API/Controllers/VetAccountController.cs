using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FarmHand.API.Controllers;

/// <summary>Vet self-service account settings, including payout destination.</summary>
[ApiController]
[Route("api/vet/me")]
[Authorize(Roles = "Vet")]
[Produces("application/json")]
public class VetAccountController : ControllerBase
{
    private readonly IVetPartnerRepository _vetPartnerRepository;
    private readonly IPaymentService _paymentService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<VetAccountController> _logger;
    private readonly IFieldEncryptor _encryptor;

    public VetAccountController(
        IVetPartnerRepository vetPartnerRepository,
        IPaymentService paymentService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILogger<VetAccountController> logger,
        IFieldEncryptor encryptor)
    {
        _encryptor = encryptor;
        _vetPartnerRepository = vetPartnerRepository;
        _paymentService = paymentService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>Get the current vet's payout account details (account number masked).</summary>
    /// <response code="200">Payout account returned; fields are null when not configured.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="404">No vet profile is linked to this account.</response>
    [HttpGet("payout-account")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayoutAccount(CancellationToken ct)
    {
        var vet = await GetCurrentVetAsync(ct);
        return Ok(ToResponse(vet));
    }

    /// <summary>
    /// Set or update the current vet's payout account. The account is registered with the
    /// payment provider as a subaccount so visit payments split automatically.
    /// </summary>
    /// <response code="200">Payout account saved and linked to a payment subaccount.</response>
    /// <response code="400">Invalid account details or the provider rejected the account.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="404">No vet profile is linked to this account.</response>
    /// <response code="502">The payment provider is unavailable.</response>
    [HttpPut("payout-account")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> UpdatePayoutAccount(
        [FromBody] UpdatePayoutAccountRequest request,
        CancellationToken ct)
    {
        var vet = await GetCurrentVetAsync(ct);

        var bankCode = request.BankCode.Trim();
        var accountNumber = request.AccountNumber.Trim();
        var accountName = request.AccountName.Trim();
        var country = string.IsNullOrWhiteSpace(request.Country)
            ? "NG"
            : request.Country.Trim().ToUpperInvariant();

        var subaccount = await _paymentService.CreateOrUpdateSubaccountAsync(
            new VetSubaccountRequest(
                vet.Id,
                vet.Name,
                vet.Email,
                vet.Contact,
                bankCode,
                accountNumber,
                accountName,
                country,
                vet.PaymentSubaccountId),
            ct);

        if (!subaccount.Success || string.IsNullOrWhiteSpace(subaccount.SubaccountId))
        {
            throw subaccount.IsTransient
                ? PublicException.Upstream("The payment provider is temporarily unavailable. Please try again.")
                : PublicException.BadRequest(subaccount.ErrorMessage ?? "The payout account could not be verified.");
        }

        // The account number is encrypted at rest; only the last four digits are kept in clear for display.
        vet.UpdatePayoutAccount(bankCode, _encryptor.Encrypt(accountNumber), accountNumber[^4..], accountName, country, subaccount.SubaccountId);
        _vetPartnerRepository.Update(vet);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Payout account updated for vet {VetId}", vet.Id);

        return Ok(new
        {
            message = "Payout account saved.",
            payoutAccount = ToResponse(vet)
        });
    }

    private async Task<VetPartner> GetCurrentVetAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId)
            throw PublicException.Forbidden("No authenticated vet account was found.");

        var vet = await _vetPartnerRepository.GetByUserIdAsync(userId, ct)
            ?? throw PublicException.NotFound("No vet profile is linked to this account.");

        if (!vet.IsActive)
            throw PublicException.Forbidden("This vet account is inactive.");

        return vet;
    }

    private static object ToResponse(VetPartner vet) => new
    {
        configured = vet.HasPayoutAccount,
        bankCode = vet.PayoutBankCode,
        accountName = vet.PayoutAccountName,
        accountNumberMasked = string.IsNullOrWhiteSpace(vet.PayoutAccountLast4) ? null : $"******{vet.PayoutAccountLast4}",
        country = vet.PayoutCountry,
        subaccountConfigured = !string.IsNullOrWhiteSpace(vet.PaymentSubaccountId)
    };

}

public record UpdatePayoutAccountRequest(
    [Required, MinLength(2), MaxLength(20)] string BankCode,
    [Required, RegularExpression("^[0-9]{6,20}$", ErrorMessage = "Account number must be 6-20 digits."), MaxLength(20)] string AccountNumber,
    [Required, MinLength(3), MaxLength(150)] string AccountName,
    [MaxLength(10)] string? Country = "NG");
