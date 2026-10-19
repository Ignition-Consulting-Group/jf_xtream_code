using System.Globalization;
using System.Text;

namespace FakeXtreamServer;

public static class PlayerApi
{
    public static IResult Handle(string? username, string? password, string? action, string? category_id, int? stream_id, int? limit, Catalogue catalogue)
    {
        if (!catalogue.IsValid(username, password))
        {
            return Results.Json(new { user_info = new { auth = 0 } });
        }

        return action switch
        {
            null => Results.Json(AccountInfo(catalogue)),
            "get_live_categories" => Results.Json(catalogue.Categories.Select(c => new { category_id = c.Id, category_name = c.Name, parent_id = 0 })),
            "get_live_streams" => Results.Json(catalogue.Channels.Where(c => category_id is null || c.CategoryId == category_id).Select(c => LiveStream(c, catalogue))),
            "get_short_epg" => Results.Json(new { epg_listings = ShortEpg(stream_id ?? 0, limit ?? 4) }),
            _ => Results.Json(Array.Empty<object>()),
        };
    }

    private static object LiveStream(FakeChannel c, Catalogue catalogue) => new
    {
        num = catalogue.Number(c.Num),
        name = c.Name,
        stream_type = "live",
        stream_id = catalogue.Number(c.StreamId),
        stream_icon = c.Num % 3 == 0 ? string.Empty : $"http://{catalogue.Options.Host}/logos/{c.StreamId}.png",
        epg_channel_id = c.EpgId,
        added = catalogue.Number(1735689600),
        category_id = c.CategoryId,
        tv_archive = catalogue.Number(c.ArchiveDays > 0 ? 1 : 0),
        tv_archive_duration = catalogue.Number(c.ArchiveDays),
    };

    private static IEnumerable<object> ShortEpg(int streamId, int limit)
    {
        var start = DateTimeOffset.UtcNow.Date;
        return Enumerable.Range(0, limit).Select(i =>
        {
            var from = new DateTimeOffset(start).AddHours(DateTimeOffset.UtcNow.Hour + i);
            return new
            {
                id = i.ToString(CultureInfo.InvariantCulture),
                title = Convert.ToBase64String(Encoding.UTF8.GetBytes($"Short EPG {streamId} #{i}")),
                description = Convert.ToBase64String(Encoding.UTF8.GetBytes("From get_short_epg.")),
                start_timestamp = from.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                stop_timestamp = from.AddHours(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            };
        });
    }

    private static object AccountInfo(Catalogue catalogue)
    {
        var fake = catalogue.Options;
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
