using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// Registers web file transformations with the optional File Transformation plugin, which lives in its own load context.
/// </summary>
public static class FileTransformationBridge
{
    /// <summary>
    /// Registers a transformation.
    /// </summary>
    /// <param name="id">Transformation id.</param>
    /// <param name="fileNamePattern">Regex for the web files to transform.</param>
    /// <param name="callbackType">Type with the static callback.</param>
    /// <param name="callbackMethod">Name of the static callback, taking an object with <c>Contents</c> and returning a string.</param>
    /// <param name="error">Why registration failed.</param>
    /// <returns>True if registered.</returns>
    public static bool TryRegister(Guid id, string fileNamePattern, Type callbackType, string callbackMethod, out string? error)
    {
        ArgumentNullException.ThrowIfNull(callbackType);
        var assembly = AssemblyLoadContext.All
            .SelectMany(c => c.Assemblies)
            .FirstOrDefault(a => a.GetName().Name == "Jellyfin.Plugin.FileTransformation");
        var register = assembly?.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface")?.GetMethod("RegisterTransformation");
        var jobject = assembly is null ? null : AssemblyLoadContext.GetLoadContext(assembly)?.Assemblies
            .FirstOrDefault(a => a.GetName().Name == "Newtonsoft.Json")?
            .GetType("Newtonsoft.Json.Linq.JObject");
        var parse = jobject?.GetMethod("Parse", [typeof(string)]);
        if (register is null || parse is null)
        {
            error = "The File Transformation plugin is not installed, so the web client cannot be linked to the enhanced Live TV page.";
            return false;
        }

        var payload = JsonSerializer.Serialize(new
        {
            id,
            fileNamePattern,
            callbackAssembly = callbackType.Assembly.FullName,
            callbackClass = callbackType.FullName,
            callbackMethod,
        });

        try
        {
            register.Invoke(null, [parse.Invoke(null, [payload])]);
            error = null;
            return true;
        }
        catch (TargetInvocationException ex)
        {
            error = "File Transformation rejected the registration: " + ex.InnerException?.Message;
            return false;
        }
    }
}
