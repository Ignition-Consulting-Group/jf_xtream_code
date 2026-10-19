namespace FakeXtreamServer;

public class MediaStreams
{
    private readonly byte[] _loop = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Media", "loop.ts"));
    private int _active;

    public async Task Handle(HttpContext context, string username, string password, string file, Catalogue catalogue)
    {
        if (!catalogue.IsValid(username, password))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!int.TryParse(Path.GetFileNameWithoutExtension(file), out var id) || (catalogue.Channels.All(c => c.StreamId != id) && !catalogue.IsMedia(id)))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (Interlocked.Increment(ref _active) > catalogue.Options.MaxConnections)
        {
            Interlocked.Decrement(ref _active);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        try
        {
            context.Response.ContentType = "video/mp2t";
            var aborted = context.RequestAborted;
            while (!aborted.IsCancellationRequested)
            {
                await context.Response.Body.WriteAsync(_loop, aborted);
                await context.Response.Body.FlushAsync(aborted);
                await Task.Delay(TimeSpan.FromSeconds(1), aborted);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }
}
