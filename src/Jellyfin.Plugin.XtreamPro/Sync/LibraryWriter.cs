using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// Applies planned items to a library folder, writing only what changed since the last sync.
/// </summary>
public class LibraryWriter
{
    private readonly int _batchSize;
    private readonly bool _emptyIsValid;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryWriter"/> class.
    /// </summary>
    /// <param name="batchSize">Items per batch. Progress is reported and the thread yielded after each batch.</param>
    /// <param name="emptyIsValid">Whether an empty plan should remove everything. Off by default, so a provider outage cannot wipe a library.</param>
    public LibraryWriter(int batchSize = 500, bool emptyIsValid = false)
    {
        _batchSize = batchSize;
        _emptyIsValid = emptyIsValid;
    }

    /// <summary>
    /// Syncs a library folder to the planned items.
    /// </summary>
    /// <param name="root">Library folder.</param>
    /// <param name="statePath">File that records what was written last time.</param>
    /// <param name="items">Planned items.</param>
    /// <param name="progress">Progress from 0 to 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="keep">Keys of items that could not be planned this time and must be left as they are.</param>
    /// <returns>What changed.</returns>
    public async Task<SyncReport> ApplyAsync(
        string root,
        string statePath,
        IReadOnlyCollection<SyncItem> items,
        IProgress<double> progress,
        CancellationToken cancellationToken,
        IReadOnlySet<string>? keep = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(progress);
        root = Path.GetFullPath(root);
        var state = await LoadAsync(statePath, cancellationToken).ConfigureAwait(false);
        var next = state
            .Where(s => keep?.Contains(s.Key) == true)
            .ToDictionary(s => s.Key, s => s.Value);
        int added = 0, updated = 0, unchanged = 0, done = 0;

        // State is saved once at the end. If a sync is interrupted, files written since the last save are
        // simply seen as new next time and rewritten with the same content.
        foreach (var batch in items.Chunk(_batchSize))
        {
            foreach (var item in batch)
            {
                var hashes = item.Files.ToDictionary(f => f.Key, f => Hash(f.Value));
                var previous = state.GetValueOrDefault(item.Key);
                next.Remove(item.Key);
                var changed = false;

                foreach (var (path, content) in item.Files)
                {
                    var full = Resolve(root, path);
                    if (previous?.GetValueOrDefault(path) != hashes[path] || !File.Exists(full))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                        await File.WriteAllTextAsync(full, content, cancellationToken).ConfigureAwait(false);
                        changed = true;
                    }
                }

                foreach (var stale in previous?.Keys.Except(hashes.Keys) ?? [])
                {
                    Delete(root, stale);
                    changed = true;
                }

                next[item.Key] = hashes;
                if (previous is null)
                {
                    added++;
                }
                else if (changed)
                {
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }

            done += batch.Length;
            progress.Report(100.0 * done / Math.Max(1, items.Count));
            await Task.Yield();
        }

        var removed = 0;
        if (items.Count > 0 || _emptyIsValid)
        {
            var kept = next.Values.SelectMany(f => f.Keys).ToHashSet();
            foreach (var (_, files) in state.Where(s => !next.ContainsKey(s.Key)))
            {
                foreach (var path in files.Keys.Where(p => !kept.Contains(p)))
                {
                    Delete(root, path);
                }

                removed++;
            }

            await SaveAsync(statePath, next, cancellationToken).ConfigureAwait(false);
        }

        progress.Report(100);
        return new SyncReport(added, updated, removed, unchanged);
    }

    private static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static string Resolve(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to write outside the library: {relative}");
        }

        return full;
    }

    private static void Delete(string root, string relative)
    {
        var full = Resolve(root, relative);
        File.Delete(full);
        for (var dir = Path.GetDirectoryName(full); dir is not null && dir.Length > root.Length; dir = Path.GetDirectoryName(dir))
        {
            if (!Directory.Exists(dir) || Directory.EnumerateFileSystemEntries(dir).Any())
            {
                break;
            }

            Directory.Delete(dir);
        }
    }

    private static async Task<Dictionary<string, Dictionary<string, string>>> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var file = File.OpenRead(path);
        await using (file.ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, string>>>(file, cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
        }
    }

    private static async Task SaveAsync(string path, IEnumerable<KeyValuePair<string, Dictionary<string, string>>> state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        var file = File.Create(temp);
        await using (file.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(file, state.ToDictionary(), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, path, true);
    }
}
