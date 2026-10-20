using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.XtreamPro;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using NSubstitute;

namespace XtreamPro.UnitTests;

public class PluginTests
{
    private readonly Plugin _plugin;

    public PluginTests()
    {
        var paths = Substitute.For<IApplicationPaths>();
        paths.PluginConfigurationsPath.Returns(Path.GetTempPath());
        _plugin = new Plugin(paths, Substitute.For<IXmlSerializer>());
    }

    [Fact]
    public void Id_MatchesManifestGuid()
    {
        _plugin.Id.ShouldBe(Guid.Parse("d7920906-a306-4216-911b-39474ca7596d"));
    }

    [Fact]
    public void GetPages_PointsAtAnEmbeddedConfigPage()
    {
        var page = _plugin.GetPages().ShouldHaveSingleItem();

        typeof(Plugin).Assembly.GetManifestResourceNames().ShouldContain(page.EmbeddedResourcePath);
    }

    [Fact]
    public void ConfigPage_HasNoTranslationPlaceholders()
    {
        // jellyfin-web replaces ${...} in plugin pages with translations, which breaks JavaScript template literals.
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream(_plugin.GetPages().Single().EmbeddedResourcePath)!;
        using var reader = new StreamReader(stream);

        reader.ReadToEnd().ShouldNotContain("${");
    }

    [Fact]
    public void GetPages_UsesPluginNameSoTheDashboardLinksToIt()
    {
        _plugin.GetPages().Single().Name.ShouldBe(_plugin.Name);
    }

    [Fact]
    public void AssemblyVersion_MatchesBuildYaml()
    {
        // Jellyfin reports the assembly version as the installed version and compares it when checking for updates.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Jellyfin.Plugin.XtreamPro.slnx")))
        {
            dir = dir.Parent;
        }

        var buildYaml = File.ReadAllText(Path.Combine(dir.FullName, "src", "Jellyfin.Plugin.XtreamPro", "build.yaml"));
        var version = Regex.Match(buildYaml, "^version: \"(?<v>[0-9.]+)\"", RegexOptions.Multiline).Groups["v"].Value;

        typeof(Plugin).Assembly.GetName().Version.ShouldBe(Version.Parse(version));
    }
}
