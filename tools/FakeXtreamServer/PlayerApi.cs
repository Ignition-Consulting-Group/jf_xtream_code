using System.Globalization;
using Microsoft.Extensions.Options;

namespace FakeXtreamServer;

public static class PlayerApi
{
    public static IResult Handle(string? username, string? password, IOptions<FakeProviderOptions> options)
    {
        var fake = options.Value;
        if (username != fake.Username || password != fake.Password)
        {
            return Results.Json(new { user_info = new { auth = 0 } });
        }

        return Results.Json(AccountInfo(fake));
    }

    private static object AccountInfo(FakeProviderOptions fake)
    {
        var now = DateTimeOffset.UtcNow;
        return new
        {
            user_info = new
            {
                username = fake.Username,
                password = fake.Password,
                auth = 1,
                status = "Active",
                exp_date = now.AddYears(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                is_trial = "0",
                active_cons = "0",
                created_at = now.AddYears(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                max_connections = fake.MaxConnections.ToString(CultureInfo.InvariantCulture),
                allowed_output_formats = new[] { "m3u8", "ts" },
            },
            server_info = new
            {
                url = fake.Host,
                port = "80",
                https_port = "443",
                server_protocol = "http",
                timezone = "UTC",
                timestamp_now = now.ToUnixTimeSeconds(),
                time_now = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            },
        };
    }
}
