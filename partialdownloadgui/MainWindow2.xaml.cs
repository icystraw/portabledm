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
            schedulers = new();
            downloadViews = new();
            progressViews = new();

            timer = new DispatcherTimer();
            timer.Tick += Timer_Tick;
            timer.Interval = new TimeSpan(0, 0, 1);
        }

        private List<Scheduler2> schedulers;
        private ObservableCollection<DownloadView> downloadViews;
        private List<ProgressView> progressViews;
        private DispatcherTimer timer;

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
                        if ((lstDownloads.SelectedItem as DownloadView) == dv)
                        {
                            ShowDownloadProgress(pv);
                        }
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
            txtUrl.Content = pv.DownloadView.Url;
            txtDownloadFolder.Content = pv.DownloadView.DownloadFolder;
            txtProgress.Content = pv.DownloadView.Progress.ToString() + "% completed.";
        }

        private void UpdateControlsStatus()
        {
            gbMoreDetails.Visibility = Visibility.Collapsed;

            btnEdit.IsEnabled = false;
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = false;
            btnDelete.IsEnabled = false;
            btnOpenFolder.IsEnabled = false;

            mnuEdit.IsEnabled = false;
            mnuStart.IsEnabled = false;
            mnuStop.IsEnabled = false;
            mnuDelete.IsEnabled = false;
            mnuOpenFolder.IsEnabled = false;

            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv) return;

            gbMoreDetails.Visibility = Visibility.Visible;

            btnDelete.IsEnabled = true;
            btnOpenFolder.IsEnabled = true;

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
                RenderOptions.SetEdgeMode(r, EdgeMode.Aliased);
                r.Height = 10;
                if (progress[i] == '\u2593')
                {
                    r.Fill = Brushes.Green;
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
                downloadViews.Add(pv.DownloadView);
                s.Start();
            }
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
            if (s != null) s.Stop(false);
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
            timer.Start();
            if (App.Args.Length == 3 && App.Args[1] == "/download")
            {
                Activate();
                this.Topmost = true;
                this.Topmost = false;
                AddDownload(Util.convertFromBase64(App.Args[2]));
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateDownloadsStatus();
            UpdateControlsStatus();
        }

        private void lstDownloads_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateControlsStatus();
            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv)
            {
                lstSections.ItemsSource = null;
                return;
            }
            foreach (ProgressView pv in progressViews)
            {
                if (pv.DownloadId == dv.Id)
                {
                    ShowDownloadProgress(pv);
                }
            }
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
    }
}
