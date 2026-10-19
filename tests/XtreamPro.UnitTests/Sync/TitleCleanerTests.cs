using Jellyfin.Plugin.XtreamPro.Sync;

namespace XtreamPro.UnitTests.Sync;

public class TitleCleanerTests
{
    private readonly TitleCleaner _cleaner = new(TitleCleaner.DefaultRules);

    [Theory]
    [InlineData("EN | Fake Movie (2020)", "Fake Movie", 2020)]
    [InlineData("4K - Another Fake", "Another Fake", null)]
    [InlineData("|UK| Some Show - 2019", "Some Show", 2019)]
    [InlineData("FR: Le Film [2018]", "Le Film", 2018)]
    [InlineData("Plain Title", "Plain Title", null)]
    [InlineData("Movie 1080p HEVC (2021)", "Movie", 2021)]
    [InlineData("  Spaced   Out  ", "Spaced Out", null)]
    [InlineData("2012 (2009)", "2012", 2009)]
    [InlineData("Blade Runner 2049", "Blade Runner 2049", null)]
    public void Clean_StripsPrefixesTagsAndYear(string raw, string title, int? year)
    {
        var cleaned = _cleaner.Clean(raw);

        cleaned.Title.ShouldBe(title);
        cleaned.Year.ShouldBe(year);
    }

    [Fact]
    public void Clean_AppliesUserRules()
    {
        var cleaner = new TitleCleaner([@"\s*\(Dubbed\)"]);

        cleaner.Clean("Show (Dubbed)").Title.ShouldBe("Show");
    }

    [Fact]
    public void Clean_InvalidUserRule_IsIgnored()
    {
        var cleaner = new TitleCleaner(["(unclosed"]);

        cleaner.Clean("Title").Title.ShouldBe("Title");
    }

    [Fact]
    public void Clean_NeverReturnsAnEmptyTitle()
    {
        _cleaner.Clean("EN | ").Title.ShouldBe("EN |");
    }
}
