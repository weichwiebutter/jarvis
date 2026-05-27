namespace Hermes.Runtime;

public sealed record RejectionReasonSummary(
    string Reason,
    int Count,
    double Share,
    string Category,
    string ImprovementHint);
