using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
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
            foreach (string url in urls)
            {
                NameValueCollection parameters = HttpUtility.ParseQueryString(new Uri(url).Query);
                YoutubeVideo v = new();
                if (parameters.Get("mime") != null) v.MimeType = parameters.Get("mime");
                else v.MimeType = string.Empty;
                try
                {
                    if (parameters.Get("clen") != null) v.ContentLength = Convert.ToInt64(parameters.Get("clen"));
                }
                catch { }
                v.Url = url;

                CheckBox cb = new();
                cb.Tag = v;
                cb.Content = v.MimeType + ", " + Util.getShortFileSize(v.ContentLength);
                spVideos.Children.Add(cb);
            }
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
            foreach (CheckBox cb in spVideos.Children)
            {
                if (cb != null && cb.IsChecked == true)
                {
                    YoutubeVideo v = cb.Tag as YoutubeVideo;
                    DownloadSection ds = new();
                    ds.Url = v.Url;
                    ds.End = (-1);
                    ds.SuggestedName = Util.removeInvalidCharFromFileName(v.MimeType);

                    try
                    {
                        Util.downloadPreprocess(ds);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        continue;
                    }
                    if (ds.DownloadStatus == DownloadStatus.DownloadError)
                    {
                        MessageBox.Show(ds.Error);
                        continue;
                    }

                    Download d = new();
                    d.DownloadFolder = App.AppSettings.DownloadFolder;
                    d.NoDownloader = cbThreads.SelectedIndex + 1;
                    d.SummarySection = ds;
                    d.Sections.Add(d.SummarySection.Clone());
                    downloads.Add(d);
                }
            }
            this.DialogResult = true;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
