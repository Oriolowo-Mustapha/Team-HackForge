using FarmHand.Domain.Entities;
using FarmHand.Domain.ValueObjects;

namespace FarmHand.UnitTests;

public class PaymentTests
{
    [Fact]
    public void NewPayment_StartsPendingWithNoReference()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000));

        Assert.Equal("Pending", payment.Status);
        Assert.Equal(string.Empty, payment.TransactionReference);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public void MarkPending_StoresReferenceWithoutCompleting()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000));

        payment.MarkPending("FH_ref");

        Assert.Equal("Pending", payment.Status);
        Assert.Equal("FH_ref", payment.TransactionReference);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public void MarkCompleted_IsIdempotentForSameReference()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000));
        payment.MarkCompleted("FH_ref");
        var firstPaidAt = payment.PaidAt;

        payment.MarkCompleted("FH_ref");

        Assert.Equal("Completed", payment.Status);
        Assert.Equal(firstPaidAt, payment.PaidAt);
    }

    [Fact]
    public void MarkCompleted_WithNewReferenceUpdatesReferenceAndTimestamp()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000));
        payment.MarkCompleted("FH_first");
        var firstPaidAt = payment.PaidAt;

        payment.MarkCompleted("FH_second");

        Assert.Equal("FH_second", payment.TransactionReference);
        Assert.True(payment.PaidAt >= firstPaidAt);
    }

    [Fact]
    public void MarkFailed_SetsFailedStatus()
    {
        var payment = new Payment(Guid.NewGuid(), new Money(5000));

        payment.MarkFailed();

        Assert.Equal("Failed", payment.Status);
    }
}
