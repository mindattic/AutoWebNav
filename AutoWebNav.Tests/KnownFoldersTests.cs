using NUnit.Framework;

namespace AutoWebNav.Tests;

[TestFixture]
public class KnownFoldersTests
{
    [Test]
    public void Downloads_IsAnAbsolutePath()
    {
        var path = KnownFolders.Downloads;
        Assert.That(path, Is.Not.Empty);
        Assert.That(Path.IsPathRooted(path), Is.True);
    }
}
