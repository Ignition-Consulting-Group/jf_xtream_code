using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Jellyfin binds plugin configuration from JSON and XML, which needs setters.", Scope = "namespaceanddescendants", Target = "~N:Jellyfin.Plugin.XtreamPro.Configuration")]
