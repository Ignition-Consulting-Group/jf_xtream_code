namespace FakeXtreamServer;

public class FakeProviderOptions
{
    public string Username { get; set; } = "demo";

    public string Password { get; set; } = "demo";

    public int MaxConnections { get; set; } = 1;

    public string Host { get; set; } = "fake.xtream.test";

    public int LiveChannels { get; set; } = 6;

    public int Movies { get; set; } = 10;

    public int Series { get; set; } = 3;

    public int Seasons { get; set; } = 2;

    public int EpisodesPerSeason { get; set; } = 3;

    public bool StringNumbers { get; set; } = true;

    public bool EmptyArrays { get; set; } = true;

    public bool BadDates { get; set; } = true;
}
