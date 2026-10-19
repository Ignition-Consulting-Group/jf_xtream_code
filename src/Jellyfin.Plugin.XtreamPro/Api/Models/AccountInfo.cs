namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// Response of <c>player_api.php</c> without an action.
/// </summary>
public sealed record AccountInfo
{
    /// <summary>Gets the account details.</summary>
    public UserInfo UserInfo { get; init; } = new();

    /// <summary>Gets the server details. Missing when authentication fails.</summary>
    public ServerInfo? ServerInfo { get; init; }
}
