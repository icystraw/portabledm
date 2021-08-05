using partialdownloadgui.Components;
using System.Threading;
using System.Windows;

namespace partialdownloadgui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static ApplicationSettings appSettings;

        public static ApplicationSettings AppSettings { get => appSettings; set => appSettings = value; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Mutex mutex = new(true, "{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}");
            if (!mutex.WaitOne(0, true))
            {
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
