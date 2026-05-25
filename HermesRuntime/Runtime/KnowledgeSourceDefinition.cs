namespace Hermes.Runtime;

public sealed record KnowledgeSourceDefinition(
    string SourceId,
    string SourceName,
    string SourceUrl,
    string SourceTrust,
    string Category,
    IReadOnlyList<string> ExtractedConcepts,
    DateTimeOffset CuratedAtUtc);
