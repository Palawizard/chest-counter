namespace ChestCounter;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var demos = args.Length > 0 ? args[0] : Path.Combine(localAppData, "FortniteGame", "Saved", "Demos");
        var stateFile = args.Length > 1 ? args[1] : Path.Combine(localAppData, "ChestCounter", "state.json");

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
            tracker = new ChestTracker(stateFile, ReplayAnalyzer.Analyze);
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

        Application.Run(new MainForm(tracker, demos, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(stateFile))!, "WebView2")));
        return 0;
    }
}
