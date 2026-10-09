namespace EDF6WeaponUnlocker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) => ShowFatal(eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                ShowFatal(exception);
            }
        };
        Application.Run(new MainForm());
    }

    private static void ShowFatal(Exception exception)
    {
        MessageBox.Show(
            EDF6WeaponUnlocker.Core.UiText.T($"予期しないエラーが発生しました。\n\n{exception.Message}", $"An unexpected error occurred.\n\n{exception.Message}"),
            "EDF6 Weapon Unlocker",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
