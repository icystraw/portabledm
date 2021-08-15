using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Data;
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
            progressData = new();

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
        private List<ProgressData> progressData;
        private DispatcherTimer timer;
        private System.Windows.Forms.NotifyIcon notifyIcon;

        private void LoadSchedulers()
        {
            List<Download> downloads = Util.RetrieveDownloadsFromFile();
            foreach (Download d in downloads)
            {
                AddDownloadWorker(d);
            }
        }

        private void StopAllDownloads(bool wait)
        {
            foreach (Scheduler2 s in schedulers)
            {
                s.Stop(false, wait);
            }
        }

        private void SaveDownloadsToFile()
        {
            List<Download> downloads = new();
            foreach (Scheduler2 s in schedulers)
            {
                downloads.Add(s.Download);
            }
            Util.SaveDownloadsToFile(downloads);
        }

        private void UpdateDownloads()
        {
            bool bJustFinished = false;
            List<Guid> downloadGroupsWithJustFinishedDownloads = new();
            progressData = new();
            foreach (Scheduler2 s in schedulers)
            {
                ProgressData pd = s.GetDownloadStatusData();
                progressData.Add(pd);
                foreach (DownloadView dv in downloadViews)
                {
                    if (dv.Id == pd.DownloadId)
                    {
                        dv.Size = pd.DownloadView.Size;
                        dv.Progress = pd.DownloadView.Progress;
                        dv.Speed = pd.DownloadView.Speed;
                        dv.FileName = pd.DownloadView.FileName;
                        // if there is a download that has just completed
                        if (dv.Status == DownloadStatus.Downloading && pd.DownloadView.Status == DownloadStatus.Finished)
                        {
                            bJustFinished = true;
                            // does the finished download belong to any download group
                            if (dv.DownloadGroup != Guid.Empty && !downloadGroupsWithJustFinishedDownloads.Contains(dv.DownloadGroup))
                            {
                                downloadGroupsWithJustFinishedDownloads.Add(dv.DownloadGroup);
                            }
                        }
                        dv.Status = pd.DownloadView.Status;
                        break;
                    }
                }
            }
            if (downloadGroupsWithJustFinishedDownloads.Count > 0)
            {
                foreach (Guid g in downloadGroupsWithJustFinishedDownloads)
                {
                    List<string> files = new();
                    // see if all files in this group have finished downloading
                    foreach (DownloadView dv in this.downloadViews)
                    {
                        if (dv.DownloadGroup == g)
                        {
                            if (dv.Status != DownloadStatus.Finished)
                            {
                                files.Clear();
                                break;
                            }
                            else
                            {
                                files.Add(dv.FileName);
                            }
                        }
                    }
                    // merge to one mkv file
                    if (files.Count > 0)
                    {
                        StringBuilder sb = new();
                        string outputFile = string.Empty;
                        foreach (string f in files)
                        {
                            sb.Append('\"');
                            sb.Append(f);
                            sb.Append("\" ");
                            if (f.Contains(".video"))
                            {
                                outputFile = "-o \"" + f + ".mkv\" ";
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(outputFile)) outputFile = "-o \"" + f + ".mkv\" ";
                            }
                        }
                        try
                        {
                            Process.Start("mkvmerge.exe", outputFile + sb.ToString()).WaitForExit();
                        }
                        catch { }
                    }
                }
            }
            if (bJustFinished && chkShutdown.IsChecked == true && !IsBusy())
            {
                try
                {
                    Process.Start("shutdown.exe", "/s");
                }
                catch { }
            }
        }

        private void ShowDownloadProgress(ProgressData pd)
        {
            txtUrl.Text = pd.DownloadView.Url;
            txtDownloadFolder.Text = pd.DownloadView.DownloadFolder;
            if (pd.DownloadView.Status == DownloadStatus.Finished)
            {
                lstSections.ItemsSource = null;
                wpProgress.Children.Clear();
                txtResumability.Text = string.Empty;
                return;
            }
            lstSections.ItemsSource = pd.SectionViews;
            DrawProgress(pd.ProgressBar);
            foreach (SectionView sv in pd.SectionViews)
            {
                if (sv.HttpStatusCode == System.Net.HttpStatusCode.OK)
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
                foreach (ProgressData pv in progressData)
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

        private void AddDownload(string url)
        {
            AddEditDownload ad = new();
            ad.Owner = this;
            ad.Url = url;
            if (ad.ShowDialog() == true)
            {
                AddDownloadWorker(ad.Download).Start();
            }
        }

        private Scheduler2 AddDownloadWorker(Download d)
        {
            Scheduler2 s = new(d);
            ProgressData pd = s.GetDownloadStatusData();
            schedulers.Add(s);
            progressData.Add(pd);
            pd.DownloadView.Tag = s;
            downloadViews.Add(pd.DownloadView);
            return s;
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
            Scheduler2 s = dv.Tag as Scheduler2;
            if (s != null)
            {
                if (s.IsDownloadFinished()) return;
                if (s.IsDownloading()) s.Stop(false, true);

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
            Scheduler2 s = dv.Tag as Scheduler2;
            if (s != null) s.Start();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv) return;
            Scheduler2 s = dv.Tag as Scheduler2;
            if (s != null)
            {
                if (!s.IsDownloadResumable())
                {
                    if (MessageBox.Show("This download is not resumable. Do you still want to stop it?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        s.Stop(false, false);
                    }
                }
                else
                {
                    s.Stop(false, false);
                }
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv) return;
            Scheduler2 s = dv.Tag as Scheduler2;
            if (s != null)
            {
                if (!s.IsDownloadFinished())
                {
                    if (MessageBox.Show("Download is not finished. Do you want to delete?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No) return;
                }
                s.Stop(true, true);
                schedulers.Remove(s);
                downloadViews.Remove(dv);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            timer.Stop();
            TcpServer.Stop();
            StopAllDownloads(true);
            try
            {
                SaveDownloadsToFile();
                Util.SaveAppSettingsToFile();
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
            timer.Stop();
            UpdateDownloads();
            UpdateControlsStatus();
            SeeIfThereIsDownloadFromBrowser();
            timer.Start();
        }

        private void lstDownloads_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateControlsStatus();
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            DownloadView dv = lstDownloads.SelectedItem as DownloadView;
            if (null == dv) return;
            Scheduler2 s = dv.Tag as Scheduler2;
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

        private void lstDownloads_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            btnOpenFolder_Click(this, null);
        }

        private void btnAddYoutube_Click(object sender, RoutedEventArgs e)
        {
            AddYoutubeDownload ad = new();
            ad.Owner = this;
            if (ad.ShowDialog() == true)
            {
                foreach (Download d in ad.Downloads)
                {
                    AddDownloadWorker(d).Start();
                }
            }
        }

        private void btnSortFileName_Click(object sender, RoutedEventArgs e)
        {
            SortUsing("FileName");
        }

        private void btnSortProgress_Click(object sender, RoutedEventArgs e)
        {
            SortUsing("Progress");
        }

        private void SortUsing(string property)
        {
            ListCollectionView view = (ListCollectionView)CollectionViewSource.GetDefaultView(lstDownloads.ItemsSource);
            if (view.SortDescriptions.Count > 0)
            {
                if (view.SortDescriptions[0].PropertyName == property)
                {
                    if (view.SortDescriptions[0].Direction == System.ComponentModel.ListSortDirection.Ascending)
                    {
                        view.SortDescriptions.Clear();
                        view.SortDescriptions.Add(new System.ComponentModel.SortDescription(property, System.ComponentModel.ListSortDirection.Descending));
                    }
                    else
                    {
                        view.SortDescriptions.Clear();
                        view.SortDescriptions.Add(new System.ComponentModel.SortDescription(property, System.ComponentModel.ListSortDirection.Ascending));
                    }
                }
                else
                {
                    view.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new System.ComponentModel.SortDescription(property, System.ComponentModel.ListSortDirection.Ascending));
                }
            }
            else
            {
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription(property, System.ComponentModel.ListSortDirection.Ascending));
            }

        }

        private void btnAddBilibili_Click(object sender, RoutedEventArgs e)
        {
            AddBilibiliDownload ad = new();
            ad.Owner = this;
            if (ad.ShowDialog() == true)
            {
                foreach (Download d in ad.Downloads)
                {
                    AddDownloadWorker(d).Start();
                }
            }
        }
    }
}
