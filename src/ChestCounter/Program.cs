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
            MessageBox.Show($"Dossier des replays introuvable :\n{demos}", "Compteur de coffres", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        ChestTracker tracker;
        try
        {
            tracker = new ChestTracker(stateFile, ReplayAnalyzer.Analyze);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException or IOException)
        {
            MessageBox.Show($"Les données enregistrées sont illisibles et n'ont pas été modifiées :\n{stateFile}", "Compteur de coffres", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        Application.Run(new MainForm(tracker, demos, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(stateFile))!, "WebView2")));
        return 0;
    }
}
