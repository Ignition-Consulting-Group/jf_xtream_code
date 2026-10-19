using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// An open live channel. Jellyfin reads the provider URL itself; this holds the connection slot until closed.
/// </summary>
public sealed class XtreamLiveSession : ILiveStream
{
    private readonly IDisposable _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtreamLiveSession"/> class.
    /// </summary>
    /// <param name="mediaSource">Media source for the channel.</param>
    /// <param name="connection">Connection slot, released on close.</param>
    public XtreamLiveSession(MediaSourceInfo mediaSource, IDisposable connection)
    {
        MediaSource = mediaSource;
        _connection = connection;
    }

    /// <inheritdoc />
    public int ConsumerCount { get; set; } = 1;

    /// <inheritdoc />
    public string OriginalStreamId { get; set; } = string.Empty;

    /// <inheritdoc />
    public string TunerHostId => ChannelId.TunerHostId;

    /// <inheritdoc />
    public bool EnableStreamSharing => false;

    /// <inheritdoc />
    public MediaSourceInfo MediaSource { get; set; }

    /// <inheritdoc />
    public string UniqueId { get; } = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public Task Open(CancellationToken openCancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task Close()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Stream GetStream() => throw new NotSupportedException("Xtream live streams are read from the provider URL.");

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();
}
