namespace Hermes.Runtime;

public sealed record JobLease(
    string LeaseId,
    string JobId,
    DateTimeOffset LeasedAtUtc,
    DateTimeOffset ExpiresAtUtc);
