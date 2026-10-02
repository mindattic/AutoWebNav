namespace AutoWebNav;

/// <summary>One kind of user action a Spectator Mode recording can capture.</summary>
public enum RecordedStepKind
{
    Navigate,
    Click,
    TypeText,
    SetValue,
    PressEnter,
    Check,
    Uncheck,
    SelectRadio,
    SelectOption,
    UploadFile,
}

/// <summary>
/// One coalesced, human-readable action from a Spectator Mode recording — the portable output of
/// <see cref="RecordingBuilder.Build"/>. Deliberately minimal: an app with a richer step model
/// (Automata's <c>Step</c>, with flow control, bindings and task-tree linkage) maps this shape onto
/// its own; an app with none (JobHunt, Prose.KdpPublish) can use it, or its JSON export via
/// <see cref="RecordingExport"/>, directly.
/// </summary>
public sealed class RecordedStep
{
    public RecordedStepKind Kind { get; set; }
    public string Label { get; set; } = "";
    public ElementFingerprint? Target { get; set; }
    public string? Value { get; set; }
    public string? Url { get; set; }

    /// <summary>True when <see cref="Value"/> was withheld because the field was a password.
    /// Sticky: once set, a later unmasked event folding into this step cannot clear it.</summary>
    public bool Masked { get; set; }

    /// <summary>A best guess that this step submits/commits something (its wording, or its
    /// <c>type="submit"</c>), for a caller that wants to flag "this is probably the point of no
    /// return" without re-deriving the heuristic.</summary>
    public bool IsCommitPoint { get; set; }
}
