using Velopack;

namespace ChestCounter;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => AfterInstallOrUpdate())
            .OnAfterUpdateFastCallback(_ => AfterInstallOrUpdate())
            .OnBeforeUninstallFastCallback(_ => StartWithFortnite.Set(false))
            .Run();

        if (args is [IsolatedAnalyzer.Argument, var replay])
        {
            NativeMethods.RunInBackground();
            IsolatedAnalyzer.Run(replay, Console.Out);
            return 0;
        }

        if (args is [StartWithFortnite.Argument])
        {
            return StartWithFortnite.RunStandby();
        }

        var background = args.Contains("--background");
        var paths = args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var demos = paths.Length > 0 ? paths[0] : Path.Combine(localAppData, "FortniteGame", "Saved", "Demos");
        var stateFile = paths.Length > 1 ? paths[1] : Path.Combine(localAppData, "ChestCounter", "state.json");
        using var instance = SingleInstance.TryClaim(showRunning: !background);
        if (instance is null)
        {
            return 0;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (!Directory.Exists(demos))
        {
            MessageBox.Show(
                Text.Pick(
                    $"Aucun replay Fortnite trouvé. Active l'enregistrement des replays dans les réglages de Fortnite, joue une partie, puis relance l'application.\n\n{demos}",
                    $"No Fortnite replays found. Turn on replay recording in the Fortnite settings, play a match, then open the app again.\n\n{demos}"),
                Text.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        ChestTracker tracker;
        try
        {
            var self = Environment.ProcessPath!;
            tracker = new ChestTracker(stateFile, replay => IsolatedAnalyzer.Analyze(self, replay));
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException or IOException)
        {
            MessageBox.Show(
                Text.Pick(
                    $"Les données enregistrées sont illisibles et n'ont pas été modifiées :\n{stateFile}",
                    $"The saved data cannot be read and was left unchanged:\n{stateFile}"),
                Text.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        StartWithFortnite.Refresh();
        var window = new MainForm(tracker, demos, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(stateFile))!, "WebView2"), background);
        instance.OnShowRequested(window.ShowFromTray);
        Application.Run(window);
        return 0;
    }

    private static void AfterInstallOrUpdate()
    {
        LegacyInstall.Remove();
        ShellIcons.Refresh();
    }
}
