using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.LiveTv;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace XtreamPro.UnitTests.LiveTv;

public class ListingsRegistrationTests
{
    private readonly IConfigurationManager _config = Substitute.For<IConfigurationManager>();

    private Task Start(LiveTvOptions options)
    {
        _config.GetConfiguration("livetv").Returns(options);
        return new ListingsRegistration(_config, NullLogger<ListingsRegistration>.Instance).StartAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Start_AddsTheGuideProviderForAllTuners()
    {
        var options = new LiveTvOptions { ListingProviders = [new ListingsProviderInfo { Type = "xmltv", Id = "other" }] };

        await Start(options);

        options.ListingProviders.Length.ShouldBe(2);
        options.ListingProviders[1].ShouldSatisfyAllConditions(
            p => p.Type.ShouldBe(ChannelId.TunerHostId),
            p => p.EnableAllTuners.ShouldBeTrue(),
            p => p.Id.ShouldNotBeNullOrEmpty());
        _config.Received(1).SaveConfiguration("livetv", options);
    }

    [Fact]
    public async Task Start_AddsATunerSoJellyfinEnablesLiveTvForUsers()
    {
        var options = new LiveTvOptions { TunerHosts = [new TunerHostInfo { Type = "m3u", Id = "other" }] };

        await Start(options);

        options.TunerHosts.Length.ShouldBe(2);
        options.TunerHosts[1].ShouldSatisfyAllConditions(
            t => t.Type.ShouldBe(ChannelId.TunerHostId),
            t => t.FriendlyName.ShouldBe("Xtream Pro"),
            t => t.Id.ShouldNotBeNullOrEmpty());
    }

    [Fact]
    public async Task Start_WhenAlreadyRegistered_DoesNothing()
    {
        await Start(new LiveTvOptions
        {
            ListingProviders = [new ListingsProviderInfo { Type = ChannelId.TunerHostId, Id = "x" }],
            TunerHosts = [new TunerHostInfo { Type = ChannelId.TunerHostId, Id = "y" }],
        });

        _config.DidNotReceiveWithAnyArgs().SaveConfiguration(default!, default!);
    }

    [Fact]
    public async Task Start_NeverFailsJellyfinStartup()
    {
        _config.GetConfiguration("livetv").Throws(new InvalidOperationException("boom"));

        await new ListingsRegistration(_config, NullLogger<ListingsRegistration>.Instance).StartAsync(TestContext.Current.CancellationToken);
    }
}
