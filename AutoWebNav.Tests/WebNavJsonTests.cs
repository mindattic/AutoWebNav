using System.Text.Json;
using NUnit.Framework;
using AutoWebNav;

namespace AutoWebNav.Tests;

[TestFixture]
public class WebNavJsonTests
{
    // fingerprint.js and resolver.js read these exact keys; a naming-policy change breaks every
    // resolve silently (the page just never finds anything), so pin the wire shape here.
    [Test]
    public void Fingerprint_SerializesWithTheKeysThePageScriptsRead()
    {
        var json = JsonSerializer.Serialize(new ElementFingerprint
        {
            Tag = "button", XPath = "/html/body/button", CssSelector = "#go", AriaLabel = "Go",
        }, WebNavJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"xPath\""));
            Assert.That(json, Does.Contain("\"cssSelector\""));
            Assert.That(json, Does.Contain("\"ariaLabel\""));
            Assert.That(json, Does.Not.Contain("\"nameAttr\""), "nulls are omitted");
        });
    }

    [Test]
    public void DocumentStartBundle_ContainsEveryToolkitScript()
    {
        Assert.Multiple(() =>
        {
            foreach (var part in new[] { ToolkitScripts.ClosedRootsJs, ToolkitScripts.StabilityJs,
                         ToolkitScripts.FingerprintJs, ToolkitScripts.ResolverJs, ToolkitScripts.FramesJs,
                         ToolkitScripts.HarvestJs })
            {
                Assert.That(part, Is.Not.Empty);
                Assert.That(ToolkitScripts.DocumentStartJs, Does.Contain(part));
            }
        });
    }
}
