using System;
using System.IO;
using System.Linq;
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
    public void GetPages_UsesPluginNameSoTheDashboardLinksToIt()
    {
        _plugin.GetPages().Single().Name.ShouldBe(_plugin.Name);
    }
}
