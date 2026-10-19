using FakeXtreamServer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FakeProviderOptions>(builder.Configuration.GetSection("Fake"));

var app = builder.Build();
app.MapGet("/player_api.php", PlayerApi.Handle);
app.Run();

public partial class Program
{
}
