using System.Diagnostics;
using Maieru.FiapTechChallenge.Chassis;

namespace FIAP.TechChallenge.Execution.Application.Messaging;

public sealed class MessageContext
{
    public Guid? IncomingMessageId { get; set; }
    public DateTimeOffset? IncomingOccurredAtUtc { get; set; }
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    public MessageEnvelope<T> Create<T>(string type, T payload, DateTimeOffset occurredAt) =>
        new(Guid.NewGuid(), type, 1, occurredAt, CorrelationId, IncomingMessageId,
            Activity.Current?.Id, Activity.Current?.TraceStateString, payload);
}
