using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Windows.Media;
using System.Windows.Shapes;
using partialdownloadgui.Components;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace partialdownloadgui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            timer = new DispatcherTimer();
            timer.Tick += Timer_Tick;
            timer.Interval = new TimeSpan(0, 0, 1);

            __debug__();
        }

        [Conditional("DEBUG")]
        private void __debug__()
        {
            // code for testing
            txtUrl.Text = "http://192.168.1.46/1.bin";
        }

        private string configFile;
        private Scheduler scheduler;
        private DispatcherTimer timer;
        private Thread schedulerThread;

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
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
            if (string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
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
            Scheduler.Username = txtUsername.Text;
            Scheduler.Password = txtPassword.Password;

            DownloadSection ds = new();
            ds.Url = txtUrl.Text.Trim();
            ds.Start = start;
            ds.End = end;
            try
            {
                Util.downloadPreprocess(ds);
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.ToString();
                return;
            }
            if (ds.DownloadStatus == DownloadStatus.DownloadError)
            {
                txtStatus.Text = "Error occurred. Server response code was: " + ds.HttpStatusCode;
                return;
            }
            txtUrl.Text = ds.Url;

            scheduler = new(ds.Url, ds.Start, ds.End);
            StartDownloadThread();

            SetOptionControlsState(false);
        }

        private void SetOptionControlsState(bool inputEnabled)
        {
            txtUrl.IsEnabled = inputEnabled;
            txtRangeFrom.IsEnabled = inputEnabled;
            txtRangeTo.IsEnabled = inputEnabled;
            btnStart.IsEnabled = inputEnabled;
            btnPauseResume.IsEnabled = !inputEnabled;
        }

        private void ClearDownload()
        {
            SetOptionControlsState(true);
            txtUrl.Text = string.Empty;
            txtRangeFrom.Text = "0";
            txtRangeTo.Text = "0";
            TogglePauseResume(false);
            wpProgress.Children.Clear();
            lstProgress.ItemsSource = null;
            txtStatus.Text = "Ready.";
            chkShutdown.IsChecked = false;
            configFile = string.Empty;
            scheduler = null;
            schedulerThread = null;
        }

        private void TogglePauseResume(bool showResume)
        {
            if (showResume)
            {
                imgResume.Visibility = Visibility.Visible;
                imgPause.Visibility = Visibility.Hidden;
            }
            else
            {
                imgResume.Visibility = Visibility.Hidden;
                imgPause.Visibility = Visibility.Visible;
            }
        }

        private void StartDownloadThread()
        {
            schedulerThread = new(new ThreadStart(SchedulerThreadEntry));
            schedulerThread.Start();
            TogglePauseResume(false);
            timer.Start();
            txtStatus.Text = "Download started.";
        }

        private void StopAndWaitForScheduler()
        {
            scheduler.Stop();
            schedulerThread.Join();
            TogglePauseResume(true);
            txtStatus.Text = "Download stopped.";
        }

        private bool IsDownloading()
        {
            if (schedulerThread != null && schedulerThread.IsAlive) return true;
            else return false;
        }

        private bool IsDownloadingFinished()
        {
            if (scheduler != null && scheduler.IsDownloadHalted()) return true;
            else return false;
        }

        private bool HasConfigFile()
        {
            return !string.IsNullOrEmpty(configFile);
        }

        private bool SaveExistingConfigFile()
        {
            try
            {
                Util.saveSectionsToFile(this.scheduler.Sections, configFile);
                return true;
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.ToString();
                return false;
            }
        }

        private bool LoadConfigFile()
        {
            OpenFileDialog dlg = new();
            dlg.Filter = "Download Manager download config files|*.par";
            if (dlg.ShowDialog(this) == true)
            {
                try
                {
                    scheduler = new Scheduler(dlg.FileName);
                    configFile = dlg.FileName;
                    return true;
                }
                catch (Exception ex)
                {
                    scheduler = null;
                    configFile = string.Empty;
                    txtStatus.Text = ex.ToString();
                    return false;
                }
            }
            return false;
        }

        private bool HasJob()
        {
            return scheduler != null;
        }

        private bool SaveNewConfigFile()
        {
            SaveFileDialog dlg = new();
            dlg.Filter = "Download Manager download config files|*.par";
            if (dlg.ShowDialog(this) == true)
            {
                try
                {
                    Util.saveSectionsToFile(scheduler.Sections, dlg.FileName);
                    configFile = dlg.FileName;
                    return true;
                }
                catch (Exception ex)
                {
                    txtStatus.Text = ex.ToString();
                    return false;
                }
            }
            return false;
        }

        private bool BrowseForDownloadedFile()
        {
            System.Windows.Forms.FolderBrowserDialog dlg = new();
            if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                if (Directory.Exists(App.AppSettings.DownloadFolder)) dlg.SelectedPath = App.AppSettings.DownloadFolder;
            }
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                btnBrowse.Content = dlg.SelectedPath;
                App.AppSettings.DownloadFolder = dlg.SelectedPath;
                return true;
            }
            return false;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!IsDownloading())
            {
                timer.Stop();
            }
            ShowDownloadStatus();
        }

        private void ShowDownloadStatus()
        {
            if (null == scheduler) return;

            List<ProgressView> pvList;
            string status = scheduler.GetDownloadStatus(out pvList);
            lstProgress.ItemsSource = pvList;
            DrawProgress(status);
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

        private void SchedulerThreadEntry()
        {
            this.Dispatcher.Invoke(() =>
            {
                scheduler.NoDownloader = cbThreads.SelectedIndex + 1;
                Scheduler.Username = txtUsername.Text;
                Scheduler.Password = txtPassword.Password;
            });
            if (scheduler.Start())
            {
                this.Dispatcher.Invoke(() =>
                {
                    btnPauseResume.IsEnabled = false;
                });
                try
                {
                    scheduler.JoinSectionsToFile();
                    scheduler.CleanTempFiles();
                    this.Dispatcher.Invoke(() =>
                    {
                        txtStatus.Text = "Download finished.";
                        if (chkShutdown.IsChecked == true)
                        {
                            Process.Start("shutdown.exe", "/s");
                        }
                    });
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        txtStatus.Text = ex.ToString();
                        btnPauseResume.IsEnabled = true;
                        TogglePauseResume(true);
                    });
                }
            }
        }

        private void btnPauseResume_Click(object sender, RoutedEventArgs e)
        {
            if (IsDownloading())
            {
                StopAndWaitForScheduler();
            }
            else
            {
                if (string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
                {
                    MessageBox.Show("Please select a location for your downloaded file.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                StartDownloadThread();
            }
        }

        private void btnNew_Click(object sender, RoutedEventArgs e)
        {
            if (!HasJob()) return;

            if (IsDownloading())
            {
                StopAndWaitForScheduler();
            }
            if (HasConfigFile())
            {
                if (!SaveExistingConfigFile()) return;
            }
            else
            {
                if (!IsDownloadingFinished())
                {
                    MessageBoxResult result = MessageBox.Show("Download is not finished. Do you want to save current download to resume in future?", "Question", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        if (!SaveNewConfigFile()) return;
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        scheduler.CleanTempFiles();
                    }
                    else return;
                }
            }
            ClearDownload();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            BrowseForDownloadedFile();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!HasJob()) return;
            if (IsDownloading())
            {
                StopAndWaitForScheduler();
            }
            if (HasConfigFile())
            {
                if (!SaveExistingConfigFile()) e.Cancel = true;
            }
            else
            {
                if (!IsDownloadingFinished())
                {
                    MessageBoxResult result = MessageBox.Show("Download is not finished. Do you want to save current download to resume in future?", "Question", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        if (!SaveNewConfigFile()) e.Cancel = true;
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        scheduler.CleanTempFiles();
                    }
                }
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!HasJob())
            {
                MessageBox.Show("There is currently no download job.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (IsDownloading())
            {
                StopAndWaitForScheduler();
            }
            if (HasConfigFile())
            {
                SaveExistingConfigFile();
            }
            else
            {
                SaveNewConfigFile();
            }
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (IsDownloading())
            {
                StopAndWaitForScheduler();
            }
            if (HasJob())
            {
                if (HasConfigFile())
                {
                    if (!SaveExistingConfigFile()) return;
                }
                else
                {
                    if (!IsDownloadingFinished())
                    {
                        MessageBoxResult result = MessageBox.Show("Download is not finished. Do you want to save current download to resume in future?", "Question", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            if (!SaveNewConfigFile()) return;
                        }
                        else if (result == MessageBoxResult.No)
                        {
                            scheduler.CleanTempFiles();
                        }
                        else return;
                    }
                }
                ClearDownload();
            }
            if (!LoadConfigFile()) return;
            ShowDownloadStatus();
            txtUrl.Text = scheduler.Sections[0].Url;
            SetOptionControlsState(false);
            TogglePauseResume(true);
            if (IsDownloadingFinished())
            {
                btnPauseResume.IsEnabled = false;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length == 2)
            {
                txtUrl.Text = Util.convertFromBase64(args[1]);
                Activate();
                this.Topmost = true;
                this.Topmost = false;
            }
            if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                btnBrowse.Content = App.AppSettings.DownloadFolder;
            }
        }

        private void btnOpenDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                Process.Start("explorer.exe", App.AppSettings.DownloadFolder);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                Util.saveAppSettingsToFile();
            }
            catch { }
        }
    }
}
