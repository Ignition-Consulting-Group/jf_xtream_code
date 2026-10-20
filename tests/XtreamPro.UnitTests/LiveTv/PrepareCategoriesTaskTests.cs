using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Jellyfin.Plugin.XtreamPro.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Querying;
using NSubstitute;

namespace XtreamPro.UnitTests.LiveTv;

public class PrepareCategoriesTaskTests
{
    private static readonly Guid ChannelGuid = Guid.NewGuid();

    private readonly IChannelManager _channels = Substitute.For<IChannelManager>();
    private readonly Folder[] _folders = [new Folder { Id = Guid.NewGuid() }, new Folder { Id = Guid.NewGuid() }];

    public PrepareCategoriesTaskTests()
    {
        _channels.GetChannelsInternalAsync(Arg.Any<ChannelQuery>())
            .Returns(new QueryResult<Channel>([new Channel { Id = ChannelGuid, Name = CategoryChannel.ChannelName }, new Channel { Id = Guid.NewGuid(), Name = "Other" }]));
        _channels.GetChannelItemsInternal(Arg.Is<InternalItemsQuery>(q => q.ParentId == Guid.Empty), Arg.Any<IProgress<double>>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult<BaseItem>([.. _folders]));
        _channels.GetChannelItemsInternal(Arg.Is<InternalItemsQuery>(q => q.ParentId != Guid.Empty), Arg.Any<IProgress<double>>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult<BaseItem>([]));
    }

    [Fact]
    public async Task Execute_OpensEveryCategoryAheadOfTheUser()
    {
        await new PrepareCategoriesTask(_channels).ExecuteAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        foreach (var folder in _folders)
        {
            await _channels.Received(1).GetChannelItemsInternal(
                Arg.Is<InternalItemsQuery>(q => q.ParentId == folder.Id && q.ChannelIds.SequenceEqual(new[] { ChannelGuid })),
                Arg.Any<IProgress<double>>(),
                Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task Execute_WithoutTheChannel_DoesNothing()
    {
        _channels.GetChannelsInternalAsync(Arg.Any<ChannelQuery>()).Returns(new QueryResult<Channel>([]));

        await new PrepareCategoriesTask(_channels).ExecuteAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        await _channels.DidNotReceiveWithAnyArgs().GetChannelItemsInternal(default!, default!, TestContext.Current.CancellationToken);
    }
}
