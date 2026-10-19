using System;

namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// Adds the enhanced Live TV loader to jellyfin-web's <c>index.html</c>. Called by the File Transformation plugin.
/// </summary>
public static class IndexTransformer
{
    private const string Loader = "<script src=\"/XtreamPro/Web/inject.js\" defer></script>";

    /// <summary>
    /// Transforms <c>index.html</c>.
    /// </summary>
    /// <param name="payload">File contents from File Transformation.</param>
    /// <returns>The new contents.</returns>
    public static string Transform(TransformPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var html = payload.Contents ?? string.Empty;
        var body = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return body < 0 || html.Contains(Loader, StringComparison.Ordinal) ? html : html.Insert(body, Loader);
    }
}
