using NUnit.Framework;

namespace AutoWebNav.Tests;

[TestFixture]
public class RecordingExportTests
{
    [Test]
    public void RoundTrips_StepsAndFingerprints()
    {
        var steps = new List<RecordedStep>
        {
            new() { Kind = RecordedStepKind.Navigate, Url = "https://example.test/", Label = "Go to example.test" },
            new()
            {
                Kind = RecordedStepKind.TypeText, Value = "hello",
                Target = new ElementFingerprint { Tag = "input", CssSelector = "#q", NearbyLabelText = "Search" },
                Label = "Type 'hello' into Search",
            },
            new() { Kind = RecordedStepKind.Click, IsCommitPoint = true, Label = "Click 'Sign in'" },
        };
        var recordedAt = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

        var json = RecordingExport.Export(steps, recordedAt);
        var imported = RecordingExport.Import(json);

        Assert.That(imported, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(imported[0].Kind, Is.EqualTo(RecordedStepKind.Navigate));
            Assert.That(imported[1].Target!.CssSelector, Is.EqualTo("#q"));
            Assert.That(imported[2].IsCommitPoint, Is.True);
        });
    }

    [Test]
    public void Import_RejectsAFileThatIsNotARecording()
    {
        Assert.That(() => RecordingExport.Import("""{"format":"something-else","steps":[]}"""),
            Throws.InstanceOf<FormatException>());
    }

    [Test]
    public void Import_RejectsGarbage()
    {
        Assert.That(() => RecordingExport.Import("not json"), Throws.InstanceOf<FormatException>());
    }

    [Test]
    public void Import_RejectsANewerFormatVersion()
    {
        var json = $$"""{"format":"{{RecordingExport.Format}}","version":{{RecordingExport.CurrentVersion + 1}},"recordedAt":"2026-01-01T00:00:00Z","steps":[]}""";
        Assert.That(() => RecordingExport.Import(json), Throws.InstanceOf<FormatException>());
    }
}
