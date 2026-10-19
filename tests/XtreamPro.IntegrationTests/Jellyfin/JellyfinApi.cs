using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XtreamPro.IntegrationTests.Jellyfin;

/// <summary>
/// Just enough of the Jellyfin REST API to set up a server and drive Live TV.
/// </summary>
public sealed class JellyfinApi : IDisposable
{
    private const string ClientHeader = "MediaBrowser Client=\"tests\", Device=\"tests\", DeviceId=\"xtreampro-tests\", Version=\"1.0\"";

    private readonly HttpClient _http;

    public JellyfinApi(Uri baseAddress)
    {
        _http = new HttpClient { BaseAddress = baseAddress };
        _http.DefaultRequestHeaders.Add("Authorization", ClientHeader);
    }

    public string UserId { get; private set; } = string.Empty;

    public async Task SetUpAsync(CancellationToken ct)
    {
        if (!(await GetAsync("/System/Info/Public", ct))["StartupWizardCompleted"]!.GetValue<bool>())
        {
            await RunStartupWizardAsync(ct);
        }

        var auth = await PostAsync("/Users/AuthenticateByName", new { Username = "admin", Pw = "admin" }, ct);
        UserId = auth["User"]!["Id"]!.GetValue<string>();
        _http.DefaultRequestHeaders.Remove("Authorization");
        _http.DefaultRequestHeaders.Add("Authorization", $"{ClientHeader}, Token=\"{auth["AccessToken"]!.GetValue<string>()}\"");
    }

    private async Task RunStartupWizardAsync(CancellationToken ct)
    {
        (await _http.PostAsJsonAsync("/Startup/Configuration", new { UICulture = "en-US", MetadataCountryCode = "US", PreferredMetadataLanguage = "en" }, ct)).EnsureSuccessStatusCode();
        (await _http.GetAsync("/Startup/User", ct)).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/Startup/User", new { Name = "admin", Password = "admin" }, ct)).EnsureSuccessStatusCode();
        (await _http.PostAsync("/Startup/Complete", null, ct)).EnsureSuccessStatusCode();
    }

    public async Task<JsonNode> GetAsync(string path, CancellationToken ct)
        => (await _http.GetFromJsonAsync<JsonNode>(path, ct))!;

    public async Task<JsonNode> PostAsync(string path, object body, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(path, body, ct);
        response.EnsureSuccessStatusCode();
        return response.Content.Headers.ContentLength == 0 ? new JsonObject() : (await response.Content.ReadFromJsonAsync<JsonNode>(ct))!;
    }

    public async Task PostAsync(string path, JsonNode body, CancellationToken ct)
    {
        using var content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        (await _http.PostAsync(path, content, ct)).EnsureSuccessStatusCode();
    }

    public async Task RunTaskAsync(string key, CancellationToken ct)
    {
        var tasks = (await GetAsync("/ScheduledTasks", ct)).AsArray();
        var id = tasks.Single(t => t!["Key"]!.GetValue<string>() == key)!["Id"]!.GetValue<string>();
        (await _http.PostAsync($"/ScheduledTasks/Running/{id}", null, ct)).EnsureSuccessStatusCode();
    }

    public static async Task<T> EventuallyAsync<T>(Func<Task<T>> probe, Func<T, bool> done, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (true)
        {
            var value = await probe();
            if (done(value) || DateTime.UtcNow > deadline)
            {
                return value;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }

    public void Dispose() => _http.Dispose();
}
