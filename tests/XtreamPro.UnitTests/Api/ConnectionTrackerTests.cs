using System;
using Jellyfin.Plugin.XtreamPro.Api;

namespace XtreamPro.UnitTests.Api;

public class ConnectionTrackerTests
{
    private readonly ConnectionTracker _tracker = new();
    private readonly Guid _provider = Guid.NewGuid();

    [Fact]
    public void TryAcquire_StopsAtTheLimit()
    {
        _tracker.TryAcquire(_provider, 2, out var first).ShouldBeTrue();
        _tracker.TryAcquire(_provider, 2, out var second).ShouldBeTrue();

        _tracker.TryAcquire(_provider, 2, out var third).ShouldBeFalse();
        third.ShouldBeNull();
        _tracker.Active(_provider).ShouldBe(2);
        first!.Dispose();
        second!.Dispose();
    }

    [Fact]
    public void DisposingALease_FreesTheSlotOnce()
    {
        _tracker.TryAcquire(_provider, 1, out var lease).ShouldBeTrue();

        lease!.Dispose();
        lease.Dispose();

        _tracker.Active(_provider).ShouldBe(0);
        _tracker.TryAcquire(_provider, 1, out _).ShouldBeTrue();
    }

    [Fact]
    public void NullLimit_IsUnlimited()
    {
        for (var i = 0; i < 10; i++)
        {
            _tracker.TryAcquire(_provider, null, out _).ShouldBeTrue();
        }

        _tracker.Active(_provider).ShouldBe(10);
    }

    [Fact]
    public void Providers_AreCountedSeparately()
    {
        _tracker.TryAcquire(_provider, 1, out _).ShouldBeTrue();

        _tracker.TryAcquire(Guid.NewGuid(), 1, out _).ShouldBeTrue();
        _tracker.Active(_provider).ShouldBe(1);
    }
}
