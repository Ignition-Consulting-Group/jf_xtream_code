using System.Globalization;
using Microsoft.Extensions.Options;

namespace FakeXtreamServer;

public sealed record FakeChannel(int StreamId, int Num, string Name, string CategoryId, string? EpgId, int ArchiveDays);

public class Catalogue
{
    private static readonly string[] CategoryNames = ["UK | News", "Sports", "Kids"];

    public Catalogue(IOptions<FakeProviderOptions> options)
    {
        Options = options.Value;
        Categories = CategoryNames.Select((name, i) => (Id: (i + 1).ToString(CultureInfo.InvariantCulture), Name: name)).ToList();
        Channels = Enumerable.Range(0, Options.LiveChannels)
            .Select(i => new FakeChannel(
                1000 + i,
                i + 1,
                $"Fake Channel {i + 1}",
                Categories[i % Categories.Count].Id,
                i % 5 == 4 ? null : $"ch{i + 1}.fake",
                i % 2 == 0 ? 3 : 0))
            .ToList();
    }

    public FakeProviderOptions Options { get; }

    public IReadOnlyList<(string Id, string Name)> Categories { get; }

    public IReadOnlyList<FakeChannel> Channels { get; }

    public bool IsValid(string? username, string? password)
        => username == Options.Username && password == Options.Password;

    public object Number(long value)
        => Options.StringNumbers ? value.ToString(CultureInfo.InvariantCulture) : value;
}
