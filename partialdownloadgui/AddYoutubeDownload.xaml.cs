using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
            txtUrl.Focus();
            if (TcpServer.YoutubeVideos.Count == 0) return;
            Video[] videos = TcpServer.YoutubeVideos.ToArray();
            List<DownloadSection> dsPreprocess = new();
            List<Thread> threads = new();
            foreach (Video v in videos)
            {
                DownloadSection ds = new();
                ds.Url = v.url;
                ds.End = (-1);
                if (v.mimeType.Contains("video"))
                {
                    ds.SuggestedName = v.title + ".video";
                }
                else
                {
                    ds.SuggestedName = v.title + ".audio";
                }
                ds.Tag = v;
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
                if (!CheckPreprocessedDownloadSection(ds)) continue;
                CheckBox cb = new();
                cb.Tag = ds;
                Video v = ds.Tag as Video;
                cb.Content = v.mimeType + ", " + Util.GetEasyToUnderstandFileSize(ds.Total) + ", " + v.duration + ", " + v.title;
                if (v.mimeType.Contains("video"))
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
                Util.DownloadPreprocess(ds);
            }
            catch { }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            Util.BrowseForDownloadedFiles(btnBrowse);
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                MessageBox.Show("You need to specify a folder for downloaded files.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            addDownload(spVideos, false);
            addDownload(spAudios, false);
            addDownload(spRecentVideos, true);
            addDownload(spRecentAudios, true);
            this.DialogResult = true;
            this.Close();
        }

        private void addDownload(StackPanel sp, bool isRecent)
        {
            foreach (UIElement cb in sp.Children)
            {
                if (cb is CheckBox box && box.IsChecked == true)
                {
                    Download d = new();
                    d.DownloadFolder = App.AppSettings.DownloadFolder;
                    d.NoDownloader = cbThreads.SelectedIndex + 1;
                    d.SummarySection = box.Tag as DownloadSection;
                    if (!isRecent)
                    {
                        try
                        {
                            Util.DownloadPreprocess(d.SummarySection);
                        }
                        catch { }
                        if (!CheckPreprocessedDownloadSection(d.SummarySection)) continue;
                    }
                    d.Sections.Add(d.SummarySection.Copy());
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

        private bool CheckPreprocessedDownloadSection(DownloadSection ds)
        {
            if (ds.DownloadStatus == DownloadStatus.DownloadError || ds.HttpStatusCode == System.Net.HttpStatusCode.OK) return false;
            if (!string.IsNullOrEmpty(ds.ContentType) && ds.ContentType.Contains("text")) return false;
            return true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void btnAnalyse_Click(object sender, RoutedEventArgs e)
        {
            string urlText = txtUrl.Text.Trim();
            if (!urlText.Contains("youtube.com/watch?"))
            {
                MessageBox.Show("Does not appear to be a valid Youtube watch page URL.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string page = Downloader.SimpleDownloadToString(urlText);
            if (string.IsNullOrEmpty(page))
            {
                MessageBox.Show("Cannot access the page.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            YoutubeWatchPageParser wp = new(page);
            try
            {
                wp.Parse();
            }
            catch
            {
                MessageBox.Show("The page given is not a Youtube watch page.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            this.Title = wp.PageTitle;
            string player = Downloader.SimpleDownloadToString("https://www.youtube.com" + wp.PlayerJsUrl);
            if (string.IsNullOrEmpty(player))
            {
                MessageBox.Show("Cannot get player script file.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            YoutubePlayerParser pp = new(player);
            try
            {
                pp.Parse();
            }
            catch
            {
                MessageBox.Show("Parsing player script file failed.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            spVideos.Children.Clear();
            spAudios.Children.Clear();
            foreach (Video v in wp.Videos)
            {
                if (!string.IsNullOrEmpty(v.signatureCipher) && !string.IsNullOrEmpty(v.paramS))
                {
                    pp.CalculateSignature(v.paramS);
                    v.signature = pp.Signature;
                }
                long fileSize = 0;
                try
                {
                    fileSize = Convert.ToInt64(v.contentLength);
                }
                catch { }
                DownloadSection ds = new();
                ds.Url = v.url;
                ds.End = (-1);
                if (v.mimeType.Contains("video"))
                {
                    ds.SuggestedName = wp.PageTitle + ".video";
                }
                else
                {
                    ds.SuggestedName = wp.PageTitle + ".audio";
                }
                CheckBox cb = new();
                cb.Tag = ds;
                cb.Content = (v.qualityLabel ?? v.audioQuality) + ", " + v.mimeType + ", " + Util.GetEasyToUnderstandFileSize(fileSize);
                if (v.mimeType.Contains("video"))
                    spVideos.Children.Add(cb);
                else
                    spAudios.Children.Add(cb);
            }
            spAV.Visibility = Visibility.Visible;
        }
    }
}
