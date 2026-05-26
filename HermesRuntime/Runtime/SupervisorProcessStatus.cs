namespace Hermes.Runtime;

public sealed record SupervisorProcessStatus(
    bool Running,
    int? Pid,
    bool StalePid,
    string PidPath,
    string LogPath,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? HeartbeatUtc,
    double? HeartbeatAgeSeconds,
    string? Warning);
