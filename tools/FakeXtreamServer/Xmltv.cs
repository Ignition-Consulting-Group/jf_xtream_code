using System.Globalization;
using System.Xml;

namespace FakeXtreamServer;

public static class Xmltv
{
    public static async Task Handle(HttpContext context, string? username, string? password, Catalogue catalogue)
    {
        if (!catalogue.IsValid(username, password))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Response.ContentType = "application/xml";
        var settings = new XmlWriterSettings { Async = true, Indent = true };
        await using var writer = XmlWriter.Create(context.Response.Body, settings);
        await writer.WriteStartElementAsync(null, "tv", null);

        var guided = catalogue.Channels.Where(c => c.EpgId is not null).ToList();
        foreach (var channel in guided)
        {
            await writer.WriteStartElementAsync(null, "channel", null);
            await writer.WriteAttributeStringAsync(null, "id", null, channel.EpgId);
            await writer.WriteElementStringAsync(null, "display-name", null, channel.Name);
            await writer.WriteEndElementAsync();
        }

        var hour = DateTimeOffset.UtcNow;
        hour = new DateTimeOffset(hour.Year, hour.Month, hour.Day, hour.Hour, 0, 0, TimeSpan.Zero);
        foreach (var channel in guided)
        {
            for (var h = -24; h < 24; h++)
            {
                var start = hour.AddHours(h);
                await writer.WriteStartElementAsync(null, "programme", null);
                await writer.WriteAttributeStringAsync(null, "start", null, Format(start));
                await writer.WriteAttributeStringAsync(null, "stop", null, Format(start.AddHours(1)));
                await writer.WriteAttributeStringAsync(null, "channel", null, channel.EpgId);
                await writer.WriteElementStringAsync(null, "title", null, $"{channel.Name} at {start:HH}:00");
                await writer.WriteElementStringAsync(null, "desc", null, "A fake programme.");
                await writer.WriteElementStringAsync(null, "category", null, channel.CategoryId == "2" ? "Sports" : "News");
                await writer.WriteEndElementAsync();
            }
        }

        await writer.WriteEndElementAsync();
    }

    private static string Format(DateTimeOffset time) => time.ToString("yyyyMMddHHmmss +0000", CultureInfo.InvariantCulture);
}
