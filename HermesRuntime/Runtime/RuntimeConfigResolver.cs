namespace Hermes.Runtime;

public static class RuntimeConfigResolver
{
    private const string ConfigFileName = "hermes.runtime.json";

    public static string Resolve(string[] args)
    {
        var explicitPath = ReadExplicitConfigPath(args);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        var current = Directory.GetCurrentDirectory();
        var baseDirectory = AppContext.BaseDirectory;

        var candidates = new[]
        {
            Path.Combine(current, "config", ConfigFileName),
            Path.Combine(current, "HermesRuntime", "config", ConfigFileName),
            Path.Combine(baseDirectory, "config", ConfigFileName),
            Path.Combine(baseDirectory, "..", "..", "..", "config", ConfigFileName),
            Path.Combine(baseDirectory, "..", "..", "..", "..", "config", ConfigFileName),
        };

        foreach (var candidate in candidates.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "Could not find hermes.runtime.json. Run from the repo root or pass --config <path>.");
    }

    private static string? ReadExplicitConfigPath(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--config" or "-c")
            {
                return i + 1 < args.Length ? args[i + 1] : null;
            }
        }

        return null;
    }
}
