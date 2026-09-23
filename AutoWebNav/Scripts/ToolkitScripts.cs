using System.IO;
using System.Reflection;

namespace AutoWebNav;

/// <summary>
/// Loads the JS building blocks embedded in this assembly, so every IBrowserSurface implementation
/// — and every app built on one — gets the same scripts.
/// <para>
/// The page-side globals keep their original <c>window.__automata*</c> names. They are a wire
/// protocol between these scripts and their callers (including Automata's recorder), and renaming
/// them would break every consumer at once for no behavioral gain.
/// </para>
/// <para>
/// Most of them can be injected on demand, once per call, and are. Two cannot, and
/// <see cref="DocumentStartJs"/> is where they go.
/// </para>
/// </summary>
public static class ToolkitScripts
{
    /// <summary>Prepended to every other script here: both of them ask it the same question,
    /// and answering it twice is how the two copies drifted apart.</summary>
    public static string StabilityJs { get; } = Load("stability.js");

    public static string FingerprintJs { get; } = Load("fingerprint.js");
    public static string ResolverJs { get; } = Load("resolver.js");
    public static string HarvestJs { get; } = Load("harvest.js");

    /// <summary>
    /// Records every CLOSED shadow root the page opens. Useless unless it runs BEFORE the page's own
    /// script — see <c>DocumentStartJs</c>.
    /// </summary>
    public static string ClosedRootsJs { get; } = Load("closed.js");

    /// <summary>Talks to the copy of the resolver running inside a cross-origin frame.</summary>
    public static string FramesJs { get; } = Load("frames.js");

    /// <summary>
    /// The bundle a host installs at document-creation time, in every frame.
    /// <para>
    /// Two of these have to be there before the page runs, and for different reasons.
    /// <c>closed.js</c> can only see a closed shadow root at the instant it is created, so arriving
    /// after the page's own script means arriving after every root it built. And the resolver has to
    /// be inside a CROSS-ORIGIN frame already, because nothing outside can put it there — which is
    /// the whole mechanism frames.js depends on.
    /// </para>
    /// <para>
    /// harvest.js is here for the second reason rather than the first. It CAN be injected late, and
    /// is, into the top document — but a harvest that has to read a list inside a cross-origin frame
    /// is answered by the copy running in that frame, called by name over the bridge. A name is only
    /// callable if something already put it there.
    /// </para>
    /// <para>
    /// Ordering is the file order: the registry first, then stability, which both fingerprint.js and
    /// harvest.js read, then the resolver, then the bridge that calls into it, then the harvester —
    /// which uses the resolver's root walk.
    /// </para>
    /// </summary>
    public static string DocumentStartJs { get; } = string.Join(
        Environment.NewLine, ClosedRootsJs, StabilityJs, FingerprintJs, ResolverJs, FramesJs, HarvestJs);

    private static string Load(string fileName)
    {
        var resource = $"AutoWebNav.Scripts.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded script '{resource}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
