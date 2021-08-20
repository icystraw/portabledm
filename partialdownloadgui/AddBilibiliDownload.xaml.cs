using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace partialdownloadgui
{
    /// <summary>
    /// Interaction logic for AddBilibiliDownload.xaml
    /// </summary>
    public partial class AddBilibiliDownload : Window
    {
        public AddBilibiliDownload()
        {
            InitializeComponent();
        }

        private List<Download> downloads = new();
        private Guid downloadGroup = Guid.NewGuid();

        public List<Download> Downloads { get => downloads; set => downloads = value; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                btnBrowse.Content = App.AppSettings.DownloadFolder;
            }
            txtUrl.Focus();
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
            addDownload(spVideos);
            addDownload(spAudios);
            this.DialogResult = true;
            this.Close();
        }

        private void addDownload(StackPanel sp)
        {
            foreach (CheckBox box in sp.Children)
            {
                if (box.IsChecked == true)
                {
                    Download d = new();
                    d.DownloadFolder = App.AppSettings.DownloadFolder;
                    d.NoDownloader = cbThreads.SelectedIndex + 1;
                    d.SummarySection = box.Tag as DownloadSection;
                    try
                    {
                        Util.DownloadPreprocess(d.SummarySection);
                    }
                    catch { }
                    if (!CheckPreprocessedDownloadSection(d.SummarySection)) continue;
                    d.Sections.Add(d.SummarySection.Copy());
                    if (cbCombine.IsChecked == true)
                    {
                        d.DownloadGroup = downloadGroup;
                    }
                    downloads.Add(d);
                }
            }
        }

        private bool CheckPreprocessedDownloadSection(DownloadSection ds)
        {
            if (ds.DownloadStatus == DownloadStatus.DownloadError) return false;
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
            if (!urlText.Contains("bilibili.com/video/"))
            {
                MessageBox.Show("Does not appear to be a valid bilibili.com watch page URL.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            byte[] page = Downloader.SimpleDownloadToByteArray(urlText);
            if (page.Length == 0)
            {
                MessageBox.Show("Cannot access the page.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            BilibiliWatchPageParser wp = new(page);
            try
            {
                wp.Parse();
            }
            catch
            {
                MessageBox.Show("The page given is not a bilibili.com watch page.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            this.Title = wp.PageTitle;
            spVideos.Children.Clear();
            spAudios.Children.Clear();
            foreach (Video v in wp.Videos)
            {
                DownloadSection ds = new();
                ds.Url = v.url;
                ds.End = (-1);
                ds.SuggestedName = wp.PageTitle + ".video";
                CheckBox cb = new();
                cb.Tag = ds;
                cb.Content = v.width + "x" + v.height + ", " + v.mimeType + ", " + v.codecs;
                spVideos.Children.Add(cb);
            }
            foreach (Video v in wp.Audios)
            {
                DownloadSection ds = new();
                ds.Url = v.url;
                ds.End = (-1);
                ds.SuggestedName = wp.PageTitle + ".audio";
                CheckBox cb = new();
                cb.Tag = ds;
                cb.Content = "Bitrate " + v.bandwidth / 1000 + "K, " + v.mimeType + ", " + v.codecs;
                spAudios.Children.Add(cb);
            }
            spAV.Visibility = Visibility.Visible;
        }
    }
}
