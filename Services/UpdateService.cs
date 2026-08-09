using Velopack;
using Velopack.Sources;

namespace NishizumiPitLink.Services;

/// <summary>
/// Checks GitHub Releases for a newer Velopack-packaged build, downloads it in the background, and
/// applies it on request. No-ops safely when the app isn't running from a Velopack install (e.g. F5
/// from the IDE), since <see cref="UpdateManager.IsInstalled"/> covers that case.
/// </summary>
public sealed class UpdateService
{
    private const string RepoUrl = "https://github.com/nishizumi-maho/Nishizumi-PitLink";

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, accessToken: null, prerelease: false));

    private UpdateInfo? _pendingUpdate;

    /// <summary>True once an update has been downloaded and is ready to install via <see cref="ApplyAndRestart"/>.</summary>
    public bool UpdateReady => _pendingUpdate is not null;

    /// <summary>
    /// Checks for, and downloads, an update if one is available. Returns a short status string for
    /// the UI/log; never throws (network and "not installed" failures are reported, not raised).
    /// </summary>
    public async Task<string> CheckAndDownloadAsync()
    {
        if (!_manager.IsInstalled)
            return "Update check skipped — not running from an installed copy.";

        UpdateInfo? info;
        try
        {
            info = await _manager.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            return $"Could not check for updates: {ex.Message}";
        }

        if (info is null)
            return "You're on the latest version.";

        try
        {
            await _manager.DownloadUpdatesAsync(info);
        }
        catch (Exception ex)
        {
            return $"Found version {info.TargetFullRelease.Version} but the download failed: {ex.Message}";
        }

        _pendingUpdate = info;
        return $"Version {info.TargetFullRelease.Version} downloaded — restart to install.";
    }

    /// <summary>Applies the previously downloaded update and restarts the app. Does nothing if none is ready.</summary>
    public void ApplyAndRestart()
    {
        if (_pendingUpdate is not null)
            _manager.ApplyUpdatesAndRestart(_pendingUpdate);
    }
}
