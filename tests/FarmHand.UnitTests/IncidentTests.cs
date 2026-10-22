using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;

namespace FarmHand.UnitTests;

public class IncidentTests
{
    private static Incident CreateOpenIncident() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            DateTime.UtcNow,
            "Mortality spike",
            Severity.High,
            "3 days of elevated mortality",
            "Mortality exceeded threshold");

    [Fact]
    public void Acknowledge_WhenOpen_TransitionsToAcknowledged()
    {
        var incident = CreateOpenIncident();

        incident.Acknowledge();

        Assert.Equal(IncidentStatus.Acknowledged, incident.Status);
    }

    [Fact]
    public void Acknowledge_WhenAlreadyAcknowledged_Throws()
    {
        var incident = CreateOpenIncident();
        incident.Acknowledge();

        Assert.Throws<InvalidOperationException>(() => incident.Acknowledge());
    }

    [Fact]
    public void Resolve_SetsResolvedStatusAndTimestamp()
    {
        var incident = CreateOpenIncident();

        incident.Resolve();

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.NotNull(incident.ResolvedAt);
    }

    [Fact]
    public void Reopen_ClearsResolvedTimestamp()
    {
        var incident = CreateOpenIncident();
        incident.Resolve();

        incident.Reopen();

        Assert.Equal(IncidentStatus.Reopened, incident.Status);
        Assert.Null(incident.ResolvedAt);
    }

    [Fact]
    public void RequireFollowUp_StoresFollowUpDate()
    {
        var incident = CreateOpenIncident();
        var followUp = DateTime.UtcNow.AddDays(2);

        incident.RequireFollowUp(followUp);

        Assert.Equal(IncidentStatus.FollowUpRequired, incident.Status);
        Assert.Equal(followUp, incident.FollowUpDate);
    }
}
