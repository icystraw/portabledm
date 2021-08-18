using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace partialdownloadgui
{
    /// <summary>
    /// Interaction logic for RedownloadSection.xaml
    /// </summary>
    public partial class RedownloadSection : Window
    {
        public RedownloadSection()
        {
            InitializeComponent();
        }

        private DownloadSection section;
        private Download download;

        public DownloadSection Section { get => section; set => section = value; }
        public Download Download { get => download; set => download = value; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
