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

            timer = new DispatcherTimer();
            timer.Tick += Timer_Tick;
            timer.Interval = new TimeSpan(0, 0, 1);
        }

        private List<Scheduler2> schedulers;
        private ObservableCollection<DownloadView> downloadViews;
        private DispatcherTimer timer;

        private void LoadSchedulers()
        {
            List<Download> downloads = Util.retrieveDownloadsFromFile();
            foreach (Download d in downloads)
            {
                Scheduler2 s = new(d);
                ProgressView pv = s.GetDownloadStatusView();
                downloadViews.Add(pv.DownloadView);
                schedulers.Add(s);
            }
        }

        private void ResetDownloadsListView()
        {
            lstDownloads.ItemsSource = downloadViews;
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
            foreach (Scheduler2 s in schedulers)
            {
                ProgressView pv = s.GetDownloadStatusView();
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
                            lstSections.ItemsSource = pv.SectionViews;
                            DrawProgress(pv.ProgressBar);
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
            if (s.IsDownloadFinished()) return;
            if (s != null)
            {
                if (s.IsDownloading()) s.Stop(false);

                AddEditDownload ad = new();
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
            if (!s.IsDownloadFinished())
            {
                if (MessageBox.Show("Download is not finished. Do you want to delete?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No) return;
            }
            s.Stop(true);
            schedulers.Remove(s);
            downloadViews.Remove(dv);
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
            ResetDownloadsListView();
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
        }

        private void lstDownloads_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (null == lstDownloads.SelectedItem)
            {
                lstSections.ItemsSource = null;
                return;
            }
            UpdateDownloadsStatus();
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv) return;
            Scheduler2 s = FindSchedulerById(dv.Id);
            Process.Start("explorer.exe", s.Download.DownloadFolder);
        }
    }
}
