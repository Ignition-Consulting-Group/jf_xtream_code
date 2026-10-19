using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace XtreamPro.UnitTests.Web;

public class EnhancedUiTests
{
    private const string Index = "<html><head></head><body><div id=\"app\"></div></body></html>";

    [Fact]
    public void IndexTransformer_AddsTheLoaderScriptOnce()
    {
        var once = IndexTransformer.Transform(new TransformPayload { Contents = Index });
        var twice = IndexTransformer.Transform(new TransformPayload { Contents = once });

        once.ShouldContain("<script src=\"/XtreamPro/Web/inject.js\" defer></script></body>");
        twice.ShouldBe(once);
    }

    [Fact]
    public void IndexTransformer_WithoutABody_LeavesTheFileAlone()
    {
        IndexTransformer.Transform(new TransformPayload { Contents = "<p>odd</p>" }).ShouldBe("<p>odd</p>");
        IndexTransformer.Transform(new TransformPayload()).ShouldBe(string.Empty);
    }

    [Fact]
    public void Bridge_WithoutFileTransformation_ReportsWhy()
    {
        FileTransformationBridge.TryRegister(Guid.NewGuid(), "index.html", typeof(IndexTransformer), nameof(IndexTransformer.Transform), out var error).ShouldBeFalse();

        error.ShouldNotBeNull().ShouldContain("File Transformation");
    }

    [Fact]
    public async Task RegistrationTask_TurnsTheFeatureOffWithAWarningWhenInjectionFails()
    {
        var provider = new FakeProvider();
        provider.Configuration.EnableEnhancedLiveTv = true;
        var status = new EnhancedUiStatus();

        await new RegisterEnhancedUiTask(provider.Settings, status, NullLogger<RegisterEnhancedUiTask>.Instance)
            .ExecuteAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        status.Injected.ShouldBeFalse();
        status.Message.ShouldNotBeNull().ShouldContain("File Transformation");
    }

    [Fact]
    public async Task RegistrationTask_DoesNothingWhileTheFlagIsOff()
    {
        var status = new EnhancedUiStatus();

        await new RegisterEnhancedUiTask(new FakeProvider().Settings, status, NullLogger<RegisterEnhancedUiTask>.Instance)
            .ExecuteAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        status.Message.ShouldBe("The enhanced Live TV page is turned off.");
    }

    [Theory]
    [InlineData("", "text/html")]
    [InlineData("livetv.js", "text/javascript")]
    [InlineData("livetv.css", "text/css")]
    [InlineData("inject.js", "text/javascript")]
    [InlineData("hls.light.min.js", "text/javascript")]
    public void Assets_AreServedWhenTheFlagIsOn(string file, string contentType)
    {
        var provider = new FakeProvider();
        provider.Configuration.EnableEnhancedLiveTv = true;

        var result = new EnhancedUiController(provider.Settings).Asset(file).ShouldBeOfType<FileStreamResult>();

        result.ContentType.ShouldStartWith(contentType);
    }

    [Theory]
    [InlineData("missing.js")]
    [InlineData("../Plugin.cs")]
    public void Assets_UnknownFiles_AreNotFound(string file)
    {
        var provider = new FakeProvider();
        provider.Configuration.EnableEnhancedLiveTv = true;

        new EnhancedUiController(provider.Settings).Asset(file).ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public void Assets_WhileTheFlagIsOff_AreNotFound()
    {
        new EnhancedUiController(new FakeProvider().Settings).Asset("livetv.js").ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public void InjectScript_StaysQuietWhenTheFlagIsOff()
    {
        var result = new EnhancedUiController(new FakeProvider().Settings).Asset("inject.js");

        result.ShouldBeOfType<ContentResult>().Content.ShouldBe(string.Empty);
    }
}
