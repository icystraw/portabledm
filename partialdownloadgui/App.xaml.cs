using partialdownloadgui.Components;
using System;
using System.Windows;

namespace partialdownloadgui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static ApplicationSettings appSettings;
        private static string[] args;

        public static ApplicationSettings AppSettings { get => appSettings; set => appSettings = value; }
        public static string[] Args { get => args; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            args = Environment.GetCommandLineArgs();
            if (args.Length == 2 && args[1] == "/startserver")
            {
                Util.startTcpServer();
                Application.Current.Shutdown();
                return;
            }
            try
            {
                Util.loadAppSettingsFromFile();
            }
            catch
            {
                appSettings = new();
            }
            MainWindow2 mw = new();
            mw.Show();
        }
    }
}
