namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Validates paper-only bot configuration.
/// </summary>
public sealed class ConfigurationValidator
{
    /// <summary>
    /// Validates the provided configuration.
    /// </summary>
    public ValidationResult Validate(BotConfiguration config)
    {
        return new ValidationResult
        {
            IsValid = false,
            Status = "not_implemented",
            Reason = "blocked_by_skeleton",
        };
    }
}
