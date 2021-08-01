using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    /// Interaction logic for AddEditDownload.xaml
    /// </summary>
    public partial class AddEditDownload : Window
    {
        public AddEditDownload()
        {
            InitializeComponent();
        }

        private Download download;
        private bool isNew = true;

        public Download Download
        {
            get => download;
            set
            {
                isNew = false;
                download = value;
            }
        }

        private void BrowseForDownloadedFile()
        {
            System.Windows.Forms.FolderBrowserDialog dlg = new();
            if (!string.IsNullOrEmpty(download.DownloadFolder))
            {
                if (Directory.Exists(download.DownloadFolder)) dlg.SelectedPath = download.DownloadFolder;
            }
            else if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                if (Directory.Exists(App.AppSettings.DownloadFolder)) dlg.SelectedPath = App.AppSettings.DownloadFolder;
            }
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                btnBrowse.Content = dlg.SelectedPath;
                download.DownloadFolder = dlg.SelectedPath;
                App.AppSettings.DownloadFolder = dlg.SelectedPath;
            }
        }

        private void btnOpenDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(download.DownloadFolder))
            {
                Process.Start("explorer.exe", download.DownloadFolder);
                return;
            }
            if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                Process.Start("explorer.exe", App.AppSettings.DownloadFolder);
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            BrowseForDownloadedFile();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (isNew)
            {
                download = new();
                if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
                {
                    download.DownloadFolder = App.AppSettings.DownloadFolder;
                    btnBrowse.Content = download.DownloadFolder;
                }
                download.NoDownloader = cbThreads.SelectedIndex + 1;
                download.SummarySection = new();
            }
            else
            {
                txtUrl.Text = download.SummarySection.Url;
                btnBrowse.Content = download.DownloadFolder;
                cbThreads.SelectedIndex = download.NoDownloader - 1;
                txtRangeFrom.Text = download.SummarySection.Start.ToString();
                txtRangeTo.Text = download.SummarySection.End.ToString();
                txtUsername.Text = download.SummarySection.UserName;
                txtPassword.Password = download.SummarySection.Password;

                txtUrl.IsEnabled = false;
                txtRangeFrom.IsEnabled = false;
                txtRangeTo.IsEnabled = false;
            }
        }
    }
}
