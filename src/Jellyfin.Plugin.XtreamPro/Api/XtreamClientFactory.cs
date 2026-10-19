using System;
using System.Net.Http;
using Jellyfin.Plugin.XtreamPro.Configuration;

namespace Jellyfin.Plugin.XtreamPro.Api;

/// <summary>
/// Creates <see cref="XtreamClient"/> instances for provider profiles.
/// </summary>
public class XtreamClientFactory
{
    /// <summary>
    /// Name of the HTTP client registered with resilience for provider calls.
    /// </summary>
    public const string HttpClientName = "XtreamPro";

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtreamClientFactory"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    public XtreamClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Creates a client for a provider profile.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The client.</returns>
    public XtreamClient Create(ProviderProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!Uri.TryCreate(profile.Url, UriKind.Absolute, out var url))
        {
            throw new ArgumentException($"Provider '{profile.Name}' has no valid URL.", nameof(profile));
        }

        var userAgent = string.IsNullOrWhiteSpace(profile.UserAgent) ? ProviderProfile.DefaultUserAgent : profile.UserAgent;
        return new XtreamClient(
            _httpClientFactory.CreateClient(HttpClientName),
            new XtreamCredentials(url, profile.Username, profile.Password),
            userAgent);
    }
}
