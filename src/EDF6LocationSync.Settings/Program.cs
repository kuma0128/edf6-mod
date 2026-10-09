namespace EDF6LocationSync.Settings;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SettingsForm());
    }
}
