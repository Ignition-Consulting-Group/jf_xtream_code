using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Keeps each provider's guide in memory and on disk so it survives restarts.
/// </summary>
public class GuideStore
{
    private readonly string _directory;
    private readonly ConcurrentDictionary<Guid, GuideResult> _guides = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GuideStore"/> class.
    /// </summary>
    /// <param name="directory">Directory for guide files.</param>
    public GuideStore(string directory)
    {
        _directory = directory;
    }

    /// <summary>
    /// Replaces a provider's guide.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="guide">The new guide.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveAsync(Guid providerId, GuideResult guide, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        var path = PathFor(providerId);
        var temp = path + ".tmp";
        var file = File.Create(temp);
        await using (file.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(file, guide, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, path, true);
        _guides[providerId] = guide;
    }

    /// <summary>
    /// Gets a provider's guide, loading it from disk if needed.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guide, or null if none has been built.</returns>
    public async Task<GuideResult?> LoadAsync(Guid providerId, CancellationToken cancellationToken)
    {
        if (_guides.TryGetValue(providerId, out var cached))
        {
            return cached;
        }

        var path = PathFor(providerId);
        if (!File.Exists(path))
        {
            return null;
        }

        var file = File.OpenRead(path);
        await using (file.ConfigureAwait(false))
        {
            var guide = await JsonSerializer.DeserializeAsync<GuideResult>(file, cancellationToken: cancellationToken).ConfigureAwait(false);
            return guide is null ? null : _guides.GetOrAdd(providerId, guide);
        }
    }

    /// <summary>
    /// Gets programmes for a channel that overlap a time range.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="channelId">Jellyfin channel id.</param>
    /// <param name="startUtc">Range start.</param>
    /// <param name="endUtc">Range end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The programmes in start order.</returns>
    public async Task<IReadOnlyList<XmltvProgramme>> GetAsync(Guid providerId, string channelId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var guide = await LoadAsync(providerId, cancellationToken).ConfigureAwait(false);
        if (guide is null || !guide.Programmes.TryGetValue(channelId, out var programmes))
        {
            return [];
        }

        return programmes.Where(p => p.Stop?.UtcDateTime > startUtc && p.Start.UtcDateTime < endUtc).ToList();
    }

    private string PathFor(Guid providerId) => Path.Combine(_directory, $"guide-{providerId:N}.json");
}
