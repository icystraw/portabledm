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
            try
            {
                Util.loadAppSettingsFromFile();
            }
            catch
            {
                appSettings = new();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            try
            {
                Util.saveAppSettingsToFile();
            }
            catch { }
        }
    }
}
