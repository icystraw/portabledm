using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
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
            if (Environment.GetCommandLineArgs().Length == 3)
            {
                Util.startTcpServer();
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
            MainWindow mw = new();
            mw.Show();
        }
    }
}
