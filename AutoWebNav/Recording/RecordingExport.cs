using System.Text.Json;

namespace AutoWebNav;

/// <summary>
/// Export/import of a finished Spectator Mode recording as one portable JSON file
/// (<c>*.autowebnav-recording.json</c>) — the artifact a developer (human or LLM) reads to write
/// or fix automation for the site that was recorded, independent of any single app's own task or
/// database model.
/// </summary>
public static class RecordingExport
{
    public const string Format = "autowebnav-recording";
    public const int CurrentVersion = 1;
    public const string FileExtension = ".autowebnav-recording.json";

    public static string Export(IReadOnlyList<RecordedStep> steps, DateTimeOffset recordedAt) =>
        JsonSerializer.Serialize(new RecordingEnvelope
        {
            Format = Format,
            Version = CurrentVersion,
            RecordedAt = recordedAt,
            Steps = steps,
        }, WebNavJson.Options);

    /// <summary>Parses an exported file. Throws <see cref="FormatException"/> with a readable
    /// message for anything that isn't one.</summary>
    public static IReadOnlyList<RecordedStep> Import(string json)
    {
        RecordingEnvelope? envelope;
        try { envelope = JsonSerializer.Deserialize<RecordingEnvelope>(json, WebNavJson.Options); }
        catch (JsonException ex) { throw new FormatException($"Not an AutoWebNav recording file: {ex.Message}", ex); }

        if (envelope is null || envelope.Format != Format)
            throw new FormatException($"Not an AutoWebNav recording file (missing \"format\": \"{Format}\").");
        if (envelope.Version > CurrentVersion)
            throw new FormatException($"This recording was exported by a newer AutoWebNav (format version {envelope.Version}). Update the app to read it.");

        return envelope.Steps ?? [];
    }

    private sealed class RecordingEnvelope
    {
        public string Format { get; set; } = "";
        public int Version { get; set; }
        public DateTimeOffset RecordedAt { get; set; }
        public IReadOnlyList<RecordedStep>? Steps { get; set; }
    }
}
