using FarmHand.Domain.Entities;
using FarmHand.Domain.ValueObjects;

namespace FarmHand.UnitTests;

public class PaymentTests
{
    [Fact]
    public void NewPayment_StartsPendingWithNoReference()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000), 5m);

        Assert.Equal("Pending", payment.Status);
        Assert.Equal(string.Empty, payment.TransactionReference);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public void MarkPending_StoresReferenceWithoutCompleting()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000), 5m);

        payment.MarkPending("FH_ref");

        Assert.Equal("Pending", payment.Status);
        Assert.Equal("FH_ref", payment.TransactionReference);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public void MarkPending_StoresCheckoutUrlAndIdempotencyKey()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000), 5m);

        payment.MarkPending("FH_ref", "https://checkout.example/pay", "client-key", true);

        Assert.Equal("https://checkout.example/pay", payment.CheckoutUrl);
        Assert.Equal("client-key", payment.IdempotencyKey);
        Assert.True(payment.VetSubaccountUsed);
    }

    [Fact]
    public void MarkCompleted_IsIdempotentForSameReference()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000), 5m);
        payment.MarkCompleted("FH_ref");
        var firstPaidAt = payment.PaidAt;

        payment.MarkCompleted("FH_ref");

        Assert.Equal("Completed", payment.Status);
        Assert.Equal(firstPaidAt, payment.PaidAt);
    }

    [Fact]
    public void MarkCompleted_PreservesMerchantReferenceAndFirstProviderId()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000), 5m);
        payment.MarkPending("FH_merchant");
        payment.MarkCompleted("provider_first");
        var firstPaidAt = payment.PaidAt;

        payment.MarkCompleted("provider_second");

        Assert.Equal("FH_merchant", payment.TransactionReference);
        Assert.Equal("provider_first", payment.ProviderTransactionId);
        Assert.Equal(firstPaidAt, payment.PaidAt);
    }

    [Fact]
    public void MarkFailed_SetsFailedStatus()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000), 5m);

        payment.MarkFailed();

        Assert.Equal("Failed", payment.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompletedPayment_CannotBeFailedOrCancelled(bool fail)
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000), 5m);
        payment.MarkPending("merchant-reference");
        payment.MarkCompleted("provider-id");
        Assert.Throws<InvalidOperationException>(() => { if (fail) payment.MarkFailed(); else payment.Cancel(); });
    }

    [Fact]
    public void CancelledPayment_CannotBeCompleted()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000), 5m);
        payment.Cancel();
        Assert.Throws<InvalidOperationException>(() => payment.MarkCompleted());
    }
}
