namespace FakeXtreamServer;

public class FakeProviderOptions
{
    public string Username { get; set; } = "demo";

    public string Password { get; set; } = "demo";

    public int MaxConnections { get; set; } = 1;

    public string Host { get; set; } = "fake.xtream.test";
}
