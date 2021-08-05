using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace partialdownloadgui
{
    /// <summary>
    /// Interaction logic for YTDownload.xaml
    /// </summary>
    public partial class YTDownload : Window
    {
        public YTDownload()
        {
            InitializeComponent();
            downloads = new();
        }

        private List<Download> downloads;

        public List<Download> Downloads { get => downloads; set => downloads = value; }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DownloadSection ytSection = YoutubeUtil.DownloadYoutubePage(txtUrl.Text.Trim());
            if (ytSection.DownloadStatus == DownloadStatus.DownloadError)
            {
                MessageBox.Show(ytSection.Error);
                return;
            }
            string videoInfoJson = YoutubeUtil.GetVideoInfoJson(ytSection);
            if (string.IsNullOrEmpty(videoInfoJson))
            {
                MessageBox.Show("Invalid information returned.", "Message", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            List<YoutubeVideo> videos;
            try
            {
                videos = YoutubeUtil.GetVideoObjectsFromJson(videoInfoJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
            if (null == videos)
            {
                MessageBox.Show("No video information available.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            this.Title = YoutubeUtil.GetTitleFromJson(ytSection);
            spChkContainer.Children.Clear();
            foreach (YoutubeVideo video in videos)
            {
                CheckBox cb = new();
                cb.Tag = video;
                cb.Name = "chk" + video.itag;
                cb.Content = video.GetDescription();
                spChkContainer.Children.Add(cb);
            }
            if (videos.Count > 0) btnDownload.IsEnabled = true;
            else btnDownload.IsEnabled = false;
            //File.Delete(ytSection.FileName);
        }

        private void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox cb in spChkContainer.Children)
            {
                if (cb != null && cb.IsChecked == true)
                {
                    YoutubeVideo video = cb.Tag as YoutubeVideo;
                    DownloadSection ds = new();
                    ds.Url = video.url;
                    ds.End = (-1);
                    ds.SuggestedName = Util.removeInvalidCharFromFileName(this.Title + YoutubeUtil.GetFileName(video));
                    try
                    {
                        Util.downloadPreprocess(ds);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                        continue;
                    }
                    if (ds.DownloadStatus == DownloadStatus.DownloadError)
                    {
                        MessageBox.Show("Error occurred. Server response code was: " + ds.HttpStatusCode);
                        continue;
                    }
                    Download d = new();
                    d.DownloadFolder = App.AppSettings.DownloadFolder;
                    d.NoDownloader = 5;
                    d.SummarySection = ds;
                    d.Sections.Add(d.SummarySection.Clone());
                    downloads.Add(d);
                }
            }
            this.Close();
        }
    }
}
