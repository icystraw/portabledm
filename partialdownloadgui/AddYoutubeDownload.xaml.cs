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
        private Guid downloadGroupRecent = Guid.NewGuid();
        private Guid downloadGroup = Guid.NewGuid();

        public List<Download> Downloads { get => downloads; set => downloads = value; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                btnBrowse.Content = App.AppSettings.DownloadFolder;
            }
            if (TcpServer.YoutubeVideos.Count == 0) return;
            YoutubeVideo[] videos = TcpServer.YoutubeVideos.ToArray();
            List<DownloadSection> dsPreprocess = new();
            List<Thread> threads = new();
            foreach (YoutubeVideo v in videos)
            {
                DownloadSection ds = new();
                ds.Url = v.Url;
                ds.End = (-1);
                ds.SuggestedName = v.Duration + " " + v.Title + " " + v.Mime;
                dsPreprocess.Add(ds);
                Thread t = new(downloadPreprocess);
                threads.Add(t);
                t.Start(ds);
            }
            foreach (Thread t in threads) t.Join();
            spRecentVideos.Children.Clear();
            spRecentAudios.Children.Clear();
            foreach (DownloadSection ds in dsPreprocess)
            {
                if (ds.DownloadStatus == DownloadStatus.DownloadError || ds.HttpStatusCode == System.Net.HttpStatusCode.OK) continue;
                if (!string.IsNullOrEmpty(ds.ContentType) && ds.ContentType.Contains("text")) continue;
                CheckBox cb = new();
                cb.Tag = ds;
                cb.Content = ds.SuggestedName + ", " + Util.getShortFileSize(ds.Total);
                if (ds.SuggestedName.Contains("video"))
                    spRecentVideos.Children.Add(cb);
                else
                    spRecentAudios.Children.Add(cb);
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
            addDownload(spRecentVideos, true);
            addDownload(spRecentAudios, true);
            this.DialogResult = true;
            this.Close();
        }

        private void addDownload(StackPanel sp, bool isRecent)
        {
            foreach (UIElement cb in sp.Children)
            {
                if (cb != null && cb is CheckBox box && box.IsChecked == true)
                {
                    Download d = new();
                    d.DownloadFolder = App.AppSettings.DownloadFolder;
                    d.NoDownloader = cbThreads.SelectedIndex + 1;
                    d.SummarySection = box.Tag as DownloadSection;
                    d.Sections.Add(d.SummarySection.Clone());
                    if (cbCombine.IsChecked == true)
                    {
                        if (isRecent)
                        {
                            d.DownloadGroup = downloadGroupRecent;
                        }
                        else
                        {
                            d.DownloadGroup = downloadGroup;
                        }
                    }
                    downloads.Add(d);
                }
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void btnAnalyse_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
