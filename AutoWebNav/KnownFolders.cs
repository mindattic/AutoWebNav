using System.Runtime.InteropServices;

namespace AutoWebNav;

/// <summary>
/// Windows known-folder lookups .NET's own <see cref="Environment.SpecialFolder"/> doesn't cover.
/// Shared because more than one AutoWebNav host app needs the same answer — a Spectator Mode
/// recording is meant to be handed back to a developer, so it belongs wherever this Windows
/// account's browser already drops a downloaded file, not a fixed app-data path a person has to
/// go hunting for.
/// </summary>
public static class KnownFolders
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, nint token, out nint path);

    /// <summary>
    /// This Windows account's real Downloads folder — honors a relocation made in Windows Settings
    /// (Explorer's Downloads library target), unlike assuming <c>%UserProfile%\Downloads</c>.
    /// Falls back to that assumption if the lookup fails, or on a non-Windows OS.
    /// </summary>
    public static string Downloads
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var id = DownloadsId;
                if (SHGetKnownFolderPath(ref id, 0, 0, out var pathPtr) == 0)
                {
                    try
                    {
                        if (Marshal.PtrToStringUni(pathPtr) is { Length: > 0 } path) return path;
                    }
                    finally { Marshal.FreeCoTaskMem(pathPtr); }
                }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }
}
