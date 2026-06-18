namespace HermesPaperBot.Models;

/// <summary>
/// Bundle checksum entry.
/// </summary>
public sealed class ChecksumEntry
{
    /// <summary>
    /// Bundle path.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// SHA-256 checksum.
    /// </summary>
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long SizeBytes { get; init; } = 0;

    /// <summary>
    /// Generation timestamp.
    /// </summary>
    public string GeneratedAt { get; init; } = string.Empty;

    /// <summary>
    /// Whether the file is required.
    /// </summary>
    public bool Required { get; init; } = true;
}
