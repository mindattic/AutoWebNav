using NUnit.Framework;

namespace AutoWebNav.Tests;

[TestFixture]
public class PageDeclutterTests
{
    [Test]
    public void NothingToHide_IsAnEmptyScript() =>
        Assert.That(PageDeclutter.Script(["", "  "]), Is.Empty);

    [Test]
    public void Selectors_BecomeOneDisplayNoneRule_SafelyQuoted()
    {
        var script = PageDeclutter.Script(["#msg-overlay", "div:has(> [data-x=\"y\"])"]);
        // The CSS travels as one JSON string literal; decoding it must give back the exact rule,
        // so a quote or ">" in a selector can never break out of the JS string.
        var literal = System.Text.RegularExpressions.Regex.Match(script, @"var css = (""(?:[^""\\]|\\.)*"");").Groups[1].Value;
        var css = System.Text.Json.JsonSerializer.Deserialize<string>(literal);
        Assert.That(css, Is.EqualTo("#msg-overlay,\ndiv:has(> [data-x=\"y\"]) { display: none !important; }"));
    }
}
