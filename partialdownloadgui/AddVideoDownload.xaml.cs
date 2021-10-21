using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace partialdownloadgui
{
    /// <summary>
    /// Interaction logic for AddVideoDownload.xaml
    /// </summary>
    public partial class AddVideoDownload : Window
    {
        public AddVideoDownload()
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
            DialogResult = true;
            Close();
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

        private static bool CheckPreprocessedDownloadSection(DownloadSection ds)
        {
            if (ds.DownloadStatus == DownloadStatus.DownloadError || ds.HttpStatusCode == System.Net.HttpStatusCode.OK) return false;
            if (!string.IsNullOrEmpty(ds.ContentType) && ds.ContentType.Contains("text")) return false;
            return true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnAnalyse_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
