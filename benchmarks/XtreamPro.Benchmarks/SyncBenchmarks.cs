using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.Sync;

namespace XtreamPro.Benchmarks;

/// <summary>
/// Planning and writing a 50k movie catalogue, first sync and an unchanged resync.
/// </summary>
[MemoryDiagnoser]
public class SyncBenchmarks
{
    private const int Count = 50_000;

    private readonly Category[] _categories = Enumerable.Range(1, 50)
        .Select(i => new Category { CategoryId = i.ToString(CultureInfo.InvariantCulture), CategoryName = $"EN | Category {i}" })
        .ToArray();

    private readonly ProviderProfile _profile = new() { Id = Guid.NewGuid() };
    private Planner _planner = null!;
    private VodItem[] _movies = [];
    private IReadOnlyList<SyncItem> _planned = [];
    private string _root = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _planner = new Planner(new TitleCleaner(TitleCleaner.DefaultRules), new StreamUrls("http://jellyfin:8096", StreamUrls.NewSecret()));
        _movies = Enumerable.Range(1, Count)
            .Select(i => new VodItem
            {
                StreamId = i,
                Name = $"EN | Benchmark Movie {i} ({1950 + (i % 75)})",
                CategoryId = ((i % 50) + 1).ToString(CultureInfo.InvariantCulture),
                ContainerExtension = "mkv",
                Tmdb = i.ToString(CultureInfo.InvariantCulture),
                Rating = 7.5,
            })
            .ToArray();
        _planned = _planner.Movies(_profile, _categories, _movies);
    }

    [IterationSetup(Targets = [nameof(FirstSync)])]
    public void FreshFolder() => _root = Path.Combine(Path.GetTempPath(), "xtreampro-bench", Guid.NewGuid().ToString("N"));

    [IterationCleanup(Targets = [nameof(FirstSync)])]
    public void DeleteFolder() => Directory.Delete(_root, true);

    [GlobalSetup(Target = nameof(UnchangedResync))]
    public void SyncedFolder()
    {
        Setup();
        FreshFolder();
        FirstSync().GetAwaiter().GetResult();
    }

    [Benchmark]
    public IReadOnlyList<SyncItem> Plan() => _planner.Movies(_profile, _categories, _movies);

    [Benchmark]
    public Task<SyncReport> FirstSync() => Apply();

    [Benchmark]
    public Task<SyncReport> UnchangedResync() => Apply();

    private Task<SyncReport> Apply()
        => new LibraryWriter().ApplyAsync(Path.Combine(_root, "library"), Path.Combine(_root, "state.json"), _planned, new Progress<double>(), default);
}
