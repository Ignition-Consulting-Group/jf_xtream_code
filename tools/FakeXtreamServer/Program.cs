using FakeXtreamServer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FakeProviderOptions>(builder.Configuration.GetSection("Fake"));
builder.Services.AddSingleton<Catalogue>();
builder.Services.AddSingleton<MediaStreams>();

var app = builder.Build();
app.MapGet("/player_api.php", PlayerApi.Handle);
app.MapGet("/xmltv.php", Xmltv.Handle);
foreach (var kind in new[] { "live", "movie", "series" })
{
    app.MapGet($"/{kind}/{{username}}/{{password}}/{{file}}", (HttpContext context, string username, string password, string file, MediaStreams streams, Catalogue catalogue)
        => streams.Handle(context, username, password, file, catalogue));
}
app.Run();

public partial class Program
{
}
