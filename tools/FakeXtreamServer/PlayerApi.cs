using System.Globalization;
using System.Text;

namespace FakeXtreamServer;

public static class PlayerApi
{
    public static IResult Handle(string? username, string? password, string? action, string? category_id, int? stream_id, int? limit, int? vod_id, int? series_id, Catalogue catalogue)
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
            "get_vod_categories" => Results.Json(catalogue.VodCategories.Select(c => new { category_id = c.Id, category_name = c.Name, parent_id = 0 })),
            "get_vod_streams" => Results.Json(catalogue.Movies.Where(m => category_id is null || m.CategoryId == category_id).Select(m => VodStream(m, catalogue))),
            "get_vod_info" => VodInfo(catalogue, vod_id),
            "get_series_categories" => Results.Json(catalogue.SeriesCategories.Select(c => new { category_id = c.Id, category_name = c.Name, parent_id = 0 })),
            "get_series" => Results.Json(catalogue.Series.Where(s => category_id is null || s.CategoryId == category_id).Select(s => SeriesItem(s, catalogue))),
            "get_series_info" => SeriesInfo(catalogue, series_id),
            _ => Results.Json(Array.Empty<object>()),
        };
    }

    private static object VodStream(FakeMovie m, Catalogue catalogue) => new
    {
        num = catalogue.Number(m.StreamId - 4999),
        name = m.Name,
        stream_type = "movie",
        stream_id = catalogue.Number(m.StreamId),
        stream_icon = $"http://{catalogue.Options.Host}/posters/{m.StreamId}.jpg",
        rating = m.StreamId % 4 == 0 ? string.Empty : "7.1",
        added = catalogue.Options.BadDates && m.StreamId % 5 == 0 ? string.Empty : "1735689600",
        category_id = m.CategoryId,
        container_extension = m.Extension,
        tmdb = m.Tmdb ?? string.Empty,
    };

    private static IResult VodInfo(Catalogue catalogue, int? id)
    {
        var movie = catalogue.Movies.FirstOrDefault(m => m.StreamId == id);
        if (movie is null)
        {
            return Results.Json(Array.Empty<object>());
        }

        object info = catalogue.Options.EmptyArrays && movie.StreamId % 2 == 1
            ? Array.Empty<object>()
            : new { tmdb_id = movie.Tmdb ?? string.Empty, name = movie.Name, plot = "A fake plot.", releasedate = $"{movie.Year}-01-01" };
        return Results.Json(new { info, movie_data = VodStream(movie, catalogue) });
    }

    private static object SeriesItem(FakeSeries s, Catalogue catalogue) => new
    {
        num = catalogue.Number(s.SeriesId - 6999),
        name = s.Name,
        series_id = catalogue.Number(s.SeriesId),
        cover = $"http://{catalogue.Options.Host}/covers/{s.SeriesId}.jpg",
        plot = "A fake show.",
        genre = "Comedy, Drama",
        releaseDate = catalogue.Options.BadDates && s.SeriesId % 2 == 1 ? string.Empty : $"{s.Year}-01-01",
        last_modified = catalogue.Number(1735689600),
        rating = "8",
        backdrop_path = catalogue.Options.EmptyArrays ? (object)Array.Empty<string>() : $"http://{catalogue.Options.Host}/backdrops/{s.SeriesId}.jpg",
        category_id = s.CategoryId,
        tmdb = string.Empty,
    };

    private static IResult SeriesInfo(Catalogue catalogue, int? id)
    {
        var series = catalogue.Series.FirstOrDefault(s => s.SeriesId == id);
        if (series is null)
        {
            return Results.Json(new { seasons = Array.Empty<object>(), info = Array.Empty<object>(), episodes = Array.Empty<object>() });
        }

        var episodes = Enumerable.Range(1, catalogue.Options.Seasons).ToDictionary(
            season => season.ToString(CultureInfo.InvariantCulture),
            season => Enumerable.Range(1, catalogue.Options.EpisodesPerSeason).Select(e => new
            {
                id = catalogue.EpisodeId(series, season, e).ToString(CultureInfo.InvariantCulture),
                episode_num = catalogue.Number(e),
                title = $"{series.Name} - S{season:00}E{e:00} - Episode {e}",
                container_extension = "mkv",
                season = catalogue.Number(season),
                info = catalogue.Options.EmptyArrays && e == 1 ? (object)Array.Empty<object>() : new { plot = "An episode.", releasedate = $"{series.Year}-0{season}-0{e}" },
            }).ToList());

        return Results.Json(new { seasons = Array.Empty<object>(), info = SeriesItem(series, catalogue), episodes });
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
