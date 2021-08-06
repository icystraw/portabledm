using partialdownloadgui.Components;
using System;
using System.IO;
using System.Windows;

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
        private string url;

        public Download Download
        {
            get => download;
            set
            {
                isNew = false;
                download = value;
            }
        }

        public string Url { get => url; set => url = value; }

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

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            BrowseForDownloadedFile();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!isNew)
            {
                download.NoDownloader = cbThreads.SelectedIndex - 1;
                download.SetCredentials(txtUsername.Text, txtPassword.Password);
                this.DialogResult = true;
                this.Close();
                return;
            }
            if (string.Empty == txtUrl.Text.Trim())
            {
                MessageBox.Show("URL is empty.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!txtUrl.Text.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("URL should be an HTTP address (starts with 'http').", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (string.IsNullOrEmpty(download.DownloadFolder))
            {
                MessageBox.Show("Please select a location for your downloaded file.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            long start = 0, end = 0;
            try
            {
                start = Convert.ToInt64(txtRangeFrom.Text.Trim());
                end = Convert.ToInt64(txtRangeTo.Text.Trim());
                if (start < 0 || end < 0)
                {
                    MessageBox.Show("Please specify positive numbers for download range.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (end > 0 && end < start)
                {
                    MessageBox.Show("Incorrect download range.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (end == 0) end = (-1);
            }
            catch
            {
                MessageBox.Show("Please specify numbers for download range.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DownloadSection ds = new();
            ds.Url = txtUrl.Text.Trim();
            ds.Start = start;
            ds.End = end;
            ds.UserName = txtUsername.Text;
            ds.Password = txtPassword.Password;
            try
            {
                Util.downloadPreprocess(ds);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
            if (ds.DownloadStatus == DownloadStatus.DownloadError)
            {
                MessageBox.Show(ds.Error);
                return;
            }
            download.SummarySection = ds;
            download.Sections.Add(download.SummarySection.Clone());
            this.DialogResult = true;
            this.Close();
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
                if (!string.IsNullOrEmpty(this.url))
                {
                    txtUrl.Text = this.url;
                }
                else if (Clipboard.ContainsText())
                {
                    string clipboardText = Clipboard.GetText();
                    if (clipboardText.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        txtUrl.Text = clipboardText;
                    }
                }
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

                txtUrl.IsReadOnly = true;
                txtRangeFrom.IsEnabled = false;
                txtRangeTo.IsEnabled = false;
            }
        }
    }
}
