using Velopack;
using Velopack.Sources;

namespace ChestCounter;

internal sealed class AppUpdater
{
    private const string Repository = "https://github.com/Palawizard/chest-counter";

    private readonly UpdateManager _manager = new(new GithubSource(Repository, accessToken: null, prerelease: false));
    private VelopackAsset? _ready;

    public string? ReadyVersion => _ready?.Version.ToString();

    // Velopack applies a downloaded update on the next launch by itself; this only prepares it.
    public async Task<bool> PrepareAsync()
    {
        if (!_manager.IsInstalled)
        {
            return false;
        }

        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
            {
                return false;
            }

            await _manager.DownloadUpdatesAsync(update);
            _ready = update.TargetFullRelease;
            return true;
        }
        catch (Exception)
        {
            // Offline, rate-limited or a broken release: the app keeps working on its current version.
            return false;
        }
    }

    public void ApplyAndRestart()
    {
        if (_ready is not null)
        {
            _manager.ApplyUpdatesAndRestart(_ready);
        }
    }
}
