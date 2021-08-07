using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using System.Web;
using System.Windows;
using System.Windows.Controls;

namespace partialdownloadgui
{
    /// <summary>
    /// Interaction logic for AddYoutubeDownload.xaml
    /// </summary>
    public partial class AddYoutubeDownload : Window
    {
        public AddYoutubeDownload()
        {
            InitializeComponent();
        }

        private List<Download> downloads = new();

        public List<Download> Downloads { get => downloads; set => downloads = value; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                btnBrowse.Content = App.AppSettings.DownloadFolder;
            }
            if (TcpServer.YoutubeUrls.Count == 0) return;
            string[] urls = TcpServer.YoutubeUrls.ToArray();
            List<DownloadSection> dsPreprocess = new();
            List<Thread> threads = new();
            foreach (string url in urls)
            {
                DownloadSection ds = new();
                ds.Url = url;
                ds.End = (-1);
                NameValueCollection parameters = HttpUtility.ParseQueryString(new Uri(url).Query);
                ds.SuggestedName = parameters.Get("itag") ?? string.Empty;
                ds.SuggestedName += parameters.Get("mime") ?? string.Empty;
                ds.SuggestedName = ds.SuggestedName.Replace('/', '.');
                dsPreprocess.Add(ds);
                Thread t = new(downloadPreprocess);
                threads.Add(t);
                t.Start(ds);
            }
            foreach (Thread t in threads) t.Join();
            foreach (DownloadSection ds in dsPreprocess)
            {
                if (ds.DownloadStatus == DownloadStatus.DownloadError || ds.HttpStatusCode == System.Net.HttpStatusCode.OK) continue;
                if (!string.IsNullOrEmpty(ds.ContentType) && ds.ContentType.Contains("text")) continue;
                CheckBox cb = new();
                cb.Tag = ds;
                cb.Content = ds.SuggestedName + ", " + Util.getShortFileSize(ds.Total);
                if (ds.SuggestedName.Contains("video"))
                    spVideos.Children.Add(cb);
                else
                    spAudios.Children.Add(cb);
            }
        }

        private void downloadPreprocess(object obj)
        {
            DownloadSection ds = obj as DownloadSection;
            try
            {
                Util.downloadPreprocess(ds);
            }
            catch { }
        }

        private void BrowseForDownloadedFiles()
        {
            System.Windows.Forms.FolderBrowserDialog dlg = new();
            if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                if (Directory.Exists(App.AppSettings.DownloadFolder)) dlg.SelectedPath = App.AppSettings.DownloadFolder;
            }
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                btnBrowse.Content = dlg.SelectedPath;
                App.AppSettings.DownloadFolder = dlg.SelectedPath;
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            BrowseForDownloadedFiles();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                MessageBox.Show("You need to specify a folder for downloaded files.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            addDownload(spVideos);
            addDownload(spAudios);
            this.DialogResult = true;
            this.Close();
        }

        private void addDownload(StackPanel sp)
        {
            foreach (CheckBox cb in sp.Children)
            {
                if (cb != null && cb.IsChecked == true)
                {
                    Download d = new();
                    d.DownloadFolder = App.AppSettings.DownloadFolder;
                    d.NoDownloader = cbThreads.SelectedIndex + 1;
                    d.SummarySection = cb.Tag as DownloadSection;
                    d.Sections.Add(d.SummarySection.Clone());
                    downloads.Add(d);
                }
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
