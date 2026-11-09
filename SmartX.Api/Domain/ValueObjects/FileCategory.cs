namespace SmartX.Api.Domain.ValueObjects;

/// <summary>The classes an attached sensor file can fall into.</summary>
public static class FileCategory
{
    public const string Config = "Config";
    public const string Photo = "Photo";
    public const string Log = "Log";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = new[] { Config, Photo, Log, Other };

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".json"] = Config, [".yaml"] = Config, [".yml"] = Config, [".xml"] = Config, [".ini"] = Config,
        [".conf"] = Config, [".cfg"] = Config, [".toml"] = Config, [".bin"] = Config,
        [".jpg"] = Photo, [".jpeg"] = Photo, [".png"] = Photo, [".gif"] = Photo, [".webp"] = Photo, [".heic"] = Photo,
        [".log"] = Log, [".txt"] = Log, [".csv"] = Log, [".out"] = Log
    };

    public static bool IsValid(string? category) =>
        category is not null && All.Contains(category, StringComparer.OrdinalIgnoreCase);

    public static string Normalise(string category) =>
        All.First(c => c.Equals(category, StringComparison.OrdinalIgnoreCase));

    /// <summary>An explicit, valid choice wins; otherwise the class is inferred from the file extension.</summary>
    public static string Classify(string fileName, string? requested)
    {
        if (IsValid(requested)) return Normalise(requested!);
        return ByExtension.TryGetValue(Path.GetExtension(fileName), out var inferred) ? inferred : Other;
    }
}