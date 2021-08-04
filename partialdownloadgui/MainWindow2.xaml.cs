using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace partialdownloadgui
{
    /// <summary>
    /// Interaction logic for MainWindow2.xaml
    /// </summary>
    public partial class MainWindow2 : Window
    {
        public MainWindow2()
        {
            InitializeComponent();
            notifyIcon = new();
            notifyIcon.Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/Images/decrease.ico")).Stream);
            notifyIcon.Text = "Portable HTTP Download Manager";
            notifyIcon.Click += NotifyIcon_Click;

            schedulers = new();
            downloadViews = new();
            progressViews = new();

            timer = new DispatcherTimer();
            timer.Tick += Timer_Tick;
            timer.Interval = new TimeSpan(0, 0, 1);

            if (App.AppSettings.MainWindowWidth > 0 && App.AppSettings.MainWindowHeight > 0)
            {
                this.Width = App.AppSettings.MainWindowWidth;
                this.Height = App.AppSettings.MainWindowHeight;
            }
        }

        private List<Scheduler2> schedulers;
        private ObservableCollection<DownloadView> downloadViews;
        private List<ProgressView> progressViews;
        private DispatcherTimer timer;
        private System.Windows.Forms.NotifyIcon notifyIcon;

        private void LoadSchedulers()
        {
            List<Download> downloads = Util.retrieveDownloadsFromFile();
            foreach (Download d in downloads)
            {
                Scheduler2 s = new(d);
                ProgressView pv = s.GetDownloadStatusView();
                progressViews.Add(pv);
                downloadViews.Add(pv.DownloadView);
                schedulers.Add(s);
            }
        }

        private void StopAllDownloads()
        {
            foreach (Scheduler2 s in schedulers)
            {
                s.Stop(false);
            }
        }

        private void SaveDownloadsToFile()
        {
            List<Download> downloads = new();
            foreach (Scheduler2 s in schedulers)
            {
                downloads.Add(s.Download);
            }
            Util.saveDownloadsToFile(downloads);
        }

        private void UpdateDownloadsStatus()
        {
            bool bJustFinished = false;
            progressViews = new();
            foreach (Scheduler2 s in schedulers)
            {
                ProgressView pv = s.GetDownloadStatusView();
                progressViews.Add(pv);
                foreach (DownloadView dv in downloadViews)
                {
                    if (dv.Id == pv.DownloadId)
                    {
                        dv.Progress = pv.DownloadView.Progress;
                        dv.Speed = pv.DownloadView.Speed;
                        // if there is a download that has just completed
                        if (dv.Status == DownloadStatus.Downloading && pv.DownloadView.Status == DownloadStatus.Finished)
                        {
                            bJustFinished = true;
                        }
                        dv.Status = pv.DownloadView.Status;
                        break;
                    }
                }
            }
            if (bJustFinished && chkShutdown.IsChecked == true && !IsBusy())
            {
                Process.Start("shutdown.exe", "/s");
            }
        }

        private void ShowDownloadProgress(ProgressView pv)
        {
            lstSections.ItemsSource = pv.SectionViews;
            DrawProgress(pv.ProgressBar);
            txtUrl.Text = pv.DownloadView.Url;
            txtDownloadFolder.Text = pv.DownloadView.DownloadFolder;
            foreach (SectionView sv in pv.SectionViews)
            {
                if (sv.Description != 0 && sv.Description != System.Net.HttpStatusCode.PartialContent)
                {
                    txtResumability.Text = "NOT RESUMABLE!";
                    txtResumability.Foreground = Brushes.Red;
                    return;
                }
            }
            txtResumability.Text = "Yes";
            txtResumability.Foreground = Brushes.Blue;
        }

        private void UpdateControlsStatus()
        {
            btnEdit.IsEnabled = false;
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = false;
            btnDelete.IsEnabled = false;
            btnOpenFolder.IsEnabled = false;
            btnOpenFolder2.IsEnabled = false;

            mnuEdit.IsEnabled = false;
            mnuStart.IsEnabled = false;
            mnuStop.IsEnabled = false;
            mnuDelete.IsEnabled = false;
            mnuOpenFolder.IsEnabled = false;

            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv)
            {
                txtUrl.Text = string.Empty;
                txtDownloadFolder.Text = string.Empty;
                txtResumability.Text = string.Empty;
                lstSections.ItemsSource = null;
                wpProgress.Children.Clear();
                return;
            }
            else
            {
                foreach (ProgressView pv in progressViews)
                {
                    if (pv.DownloadId == dv.Id)
                    {
                        ShowDownloadProgress(pv);
                    }
                }
            }

            btnDelete.IsEnabled = true;
            btnOpenFolder.IsEnabled = true;
            btnOpenFolder2.IsEnabled = true;

            mnuDelete.IsEnabled = true;
            mnuOpenFolder.IsEnabled = true;
            if (dv.Status == DownloadStatus.DownloadError)
            {
                btnEdit.IsEnabled = true;
                btnStart.IsEnabled = true;

                mnuEdit.IsEnabled = true;
                mnuStart.IsEnabled = true;
            }
            if (dv.Status == DownloadStatus.Downloading)
            {
                btnStop.IsEnabled = true;

                mnuStop.IsEnabled = true;
            }
            if (dv.Status == DownloadStatus.Stopped)
            {
                btnEdit.IsEnabled = true;
                btnStart.IsEnabled = true;

                mnuEdit.IsEnabled = true;
                mnuStart.IsEnabled = true;
            }
        }

        private bool IsBusy()
        {
            foreach (Scheduler2 s in schedulers)
            {
                if (s.IsDownloading()) return true;
            }
            return false;
        }

        private void DrawProgress(string progress)
        {
            wpProgress.Children.Clear();
            for (int i = 0; i < progress.Length; i++)
            {
                Rectangle r = new();
                r.Height = 10;
                r.Width = 10;
                r.RadiusX = 3;
                r.RadiusY = 3;
                r.Margin = new Thickness(1);
                if (progress[i] == '\u2593')
                {
                    r.Fill = Brushes.LightSeaGreen;
                }
                else
                {
                    r.Fill = Brushes.LightGray;
                }
                wpProgress.Children.Add(r);
            }
        }

        private Scheduler2 FindSchedulerById(Guid id)
        {
            foreach (Scheduler2 s in schedulers)
            {
                if (s.Download.SummarySection.Id == id)
                {
                    return s;
                }
            }
            return null;
        }

        private void AddDownload(string url)
        {
            AddEditDownload ad = new();
            ad.Owner = this;
            ad.Url = url;
            if (ad.ShowDialog() == true)
            {
                Scheduler2 s = new(ad.Download);
                ProgressView pv = s.GetDownloadStatusView();
                schedulers.Add(s);
                progressViews.Add(pv);
                downloadViews.Add(pv.DownloadView);
                s.Start();
            }
        }

        private void SeeIfThereIsDownloadFromBrowser()
        {
            if (string.IsNullOrEmpty(TcpServer.DownloadUrl)) return;
            string downloadUrl = TcpServer.DownloadUrl;
            TcpServer.DownloadUrl = string.Empty;
            AddDownload(downloadUrl);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            AddDownload(string.Empty);
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv) return;
            Scheduler2 s = FindSchedulerById(dv.Id);
            if (s != null)
            {
                if (s.IsDownloadFinished()) return;
                if (s.IsDownloading()) s.Stop(false);

                AddEditDownload ad = new();
                ad.Owner = this;
                ad.Download = s.Download;
                ad.ShowDialog();
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv) return;
            Scheduler2 s = FindSchedulerById(dv.Id);
            if (s != null) s.Start();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv) return;
            Scheduler2 s = FindSchedulerById(dv.Id);
            if (s != null)
            {
                if (!s.IsDownloadResumable())
                {
                    if (MessageBox.Show("This download is not resumable. Do you still want to stop it?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        s.Stop(false);
                    }
                }
                else
                {
                    s.Stop(false);
                }
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv) return;
            Scheduler2 s = FindSchedulerById(dv.Id);
            if (s != null)
            {
                if (!s.IsDownloadFinished())
                {
                    if (MessageBox.Show("Download is not finished. Do you want to delete?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No) return;
                }
                s.Stop(true);
                schedulers.Remove(s);
                downloadViews.Remove(dv);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            timer.Stop();
            TcpServer.Stop();
            StopAllDownloads();
            try
            {
                SaveDownloadsToFile();
                Util.saveAppSettingsToFile();
            }
            catch { }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadSchedulers();
            }
            catch { }
            lstDownloads.ItemsSource = downloadViews;
            UpdateControlsStatus();
            if (App.AppSettings.StartTcpServer) chkBrowserDownload.IsChecked = true;
            if (App.AppSettings.MinimizeToSystemTray) chkMinimizeToTray.IsChecked = true;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateDownloadsStatus();
            UpdateControlsStatus();
            SeeIfThereIsDownloadFromBrowser();
        }

        private void lstDownloads_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateControlsStatus();
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv) return;
            Scheduler2 s = FindSchedulerById(dv.Id);
            if (s != null) Process.Start("explorer.exe", s.Download.DownloadFolder);
        }

        private void mnuOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            btnOpenFolder_Click(sender, e);
        }

        private void mnuStart_Click(object sender, RoutedEventArgs e)
        {
            btnStart_Click(sender, e);
        }

        private void mnuStop_Click(object sender, RoutedEventArgs e)
        {
            btnStop_Click(sender, e);
        }

        private void mnuEdit_Click(object sender, RoutedEventArgs e)
        {
            btnEdit_Click(sender, e);
        }

        private void mnuDelete_Click(object sender, RoutedEventArgs e)
        {
            btnDelete_Click(sender, e);
        }

        private void chkBrowserDownload_Unchecked(object sender, RoutedEventArgs e)
        {
            App.AppSettings.StartTcpServer = false;
            TcpServer.Stop();
        }

        private void chkBrowserDownload_Checked(object sender, RoutedEventArgs e)
        {
            App.AppSettings.StartTcpServer = true;
            TcpServer.Start();
        }

        private void btnCopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(txtUrl.Text);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            App.AppSettings.MainWindowWidth = this.Width;
            App.AppSettings.MainWindowHeight = this.Height;
            if (IsBusy())
            {
                if (MessageBox.Show("Downloads are running. Do you want to exit? Please note if there are downloads that are not resumable, you would have to start over again next time.", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
                return;
            }
        }

        private void chkMinimizeToTray_Unchecked(object sender, RoutedEventArgs e)
        {
            App.AppSettings.MinimizeToSystemTray = false;
        }

        private void chkMinimizeToTray_Checked(object sender, RoutedEventArgs e)
        {
            App.AppSettings.MinimizeToSystemTray = true;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                if (App.AppSettings.MinimizeToSystemTray)
                {
                    this.ShowInTaskbar = false;
                    notifyIcon.Visible = true;
                }
            }
        }

        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            WindowState = WindowState.Normal;
            notifyIcon.Visible = false;
            this.ShowInTaskbar = true;
        }
    }
}
