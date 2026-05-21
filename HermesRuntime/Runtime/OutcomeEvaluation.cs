namespace Hermes.Runtime;

public sealed record OutcomeEvaluation(
    string OutcomeId,
    string SignalId,
    string Symbol,
    string Timeframe,
    string Direction,
    string OutcomeStatus,
    bool HitTarget,
    bool HitStop,
    bool Expired,
    bool Invalidated,
    double Mfe,
    double Mae,
    double FinalR,
    DateTimeOffset EvaluatedAtUtc,
    string Notes);
