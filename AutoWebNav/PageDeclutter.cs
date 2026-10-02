using System.Text.Json;

namespace AutoWebNav;

/// <summary>
/// Hides page clutter (chat overlays, upsell asides, alert prompts) by CSS selector. Injected as a
/// document-created script, so it applies to every page the pane loads, before the page draws.
/// <para>
/// Hides with <c>display:none !important</c> rather than removing nodes: the page's own framework
/// keeps references to its elements, and deleting them out from under it can throw on its next
/// update. A stylesheet also covers anything the page re-renders later.
/// </para>
/// <para>
/// Applied inside shadow roots too — a document stylesheet can't reach into one, and sites embed
/// whole widgets that way (LinkedIn renders its messaging overlay inside an open root on
/// #interop-outlet). attachShadow is wrapped so every root gets the rule the moment it's created;
/// a periodic sweep re-adds it if the page replaces a root's sheets.
/// </para>
/// </summary>
public static class PageDeclutter
{
    /// <summary>The script to register with <c>AddScriptToExecuteOnDocumentCreatedAsync</c>; empty
    /// when there's nothing to hide.</summary>
    public static string Script(IEnumerable<string> selectors)
    {
        var list = selectors.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (list.Count == 0) return "";
        var css = string.Join(",\n", list) + " { display: none !important; }";
        return $$"""
            (function () {
              if (window.__autowebnavDeclutter) return;
              window.__autowebnavDeclutter = true;
              var css = {{JsonSerializer.Serialize(css)}};
              var sheet = null;
              try { sheet = new CSSStyleSheet(); sheet.replaceSync(css); } catch (e) { sheet = null; }
              var roots = [];
              function addTo(root) {
                if (!root) return;
                if (sheet && root.adoptedStyleSheets) {
                  if (root.adoptedStyleSheets.indexOf(sheet) < 0) root.adoptedStyleSheets = root.adoptedStyleSheets.concat([sheet]);
                  return;
                }
                var host = root === document ? (document.head || document.documentElement) : root;
                if (!host || host.querySelector('style[data-autowebnav-declutter]')) return;
                var s = document.createElement('style');
                s.setAttribute('data-autowebnav-declutter', '');
                s.textContent = css;
                host.appendChild(s);
              }
              var attach = Element.prototype.attachShadow;
              Element.prototype.attachShadow = function () {
                var root = attach.apply(this, arguments);
                roots.push(root);
                try { addTo(root); } catch (e) { /* never break the page's own attach */ }
                return root;
              };
              function sweep() { addTo(document); roots.forEach(addTo); }
              // Open roots attached before this ran (only when injected late, never at document start).
              function discover() {
                var all = document.querySelectorAll('*');
                for (var i = 0; i < all.length; i++) {
                  var r = all[i].shadowRoot;
                  if (r && roots.indexOf(r) < 0) { roots.push(r); addTo(r); }
                }
              }
              if (document.documentElement) { sweep(); discover(); }
              document.addEventListener('DOMContentLoaded', function () { sweep(); discover(); });
              window.addEventListener('load', discover);
              setInterval(sweep, 2000);
            })();
            """;
    }
}
