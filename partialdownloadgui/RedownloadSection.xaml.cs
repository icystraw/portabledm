using partialdownloadgui.Components;
using System;
using System.IO;
using System.Windows;

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

        private void UpdateRangeFigures()
        {
            double total = (double)section.Total;
            double start = (double)section.Start;
            double actualViewWidth = wpPortionView.ActualWidth - gs1.ActualWidth - gs2.ActualWidth;
            double startPercentage = rectPV1.ActualWidth / actualViewWidth;
            double downloadPercentage = rectPV2.ActualWidth / actualViewWidth;
            txtReStart.Text = Math.Round(start + (total - 1) * startPercentage, MidpointRounding.AwayFromZero).ToString();
            txtReEnd.Text = Math.Round(start + (total - 1) * (startPercentage + downloadPercentage), MidpointRounding.AwayFromZero).ToString();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (null == section) return;
            txtFileName.Text = section.FileName;
            txtFileSize.Text = section.Total.ToString() + " bytes (" + section.Start + '-' + section.End + ')';
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            long start = Convert.ToInt64(txtReStart.Text);
            long end = Convert.ToInt64(txtReEnd.Text);
            if (end < start)
            {
                MessageBox.Show("Invalid download range.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!File.Exists(section.FileName) || (new FileInfo(section.FileName)).Length != section.Total)
            {
                MessageBox.Show("Original file does not exist or is not correct size.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DownloadSection newDs = new();
            newDs.Url = section.Url;
            newDs.Start = start;
            newDs.End = end;
            newDs.UserName = section.UserName;
            newDs.Password = section.Password;
            newDs.ParentFile = section.FileName;
            newDs.LastModified = section.LastModified;
            try
            {
                Util.DownloadPreprocess(newDs);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (newDs.DownloadStatus == DownloadStatus.DownloadError)
            {
                MessageBox.Show(newDs.Error, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            download = new();
            download.SummarySection = newDs;
            download.Sections.Add(download.SummarySection.Copy());
            download.NoDownloader = cbThreads.SelectedIndex + 1;
            download.DownloadFolder = System.IO.Path.GetDirectoryName(newDs.ParentFile);
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void rectPV2_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateRangeFigures();
        }
    }
}
