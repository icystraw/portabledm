using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace partialdownloadgui.Components
{
    public class Scheduler2
    {
        private static readonly int maxNoDownloader = 10;
        private static readonly long minSectionSize = 10485760;
        private static readonly int bufferSize = 1048576;

        private readonly Downloader[] downloaders = new Downloader[maxNoDownloader];
        private readonly Download download;
        private readonly object sectionsLock = new();

        private bool downloadStopFlag = false;
        private SpeedCalculator sc = new();

        private Thread downloadThread;
        private Exception exMessage;

        public Download Download => download;

        public int NoDownloader
        {
            get
            {
                return download.NoDownloader;
            }
            set
            {
                if (value <= 0 || value > maxNoDownloader) return;
                download.NoDownloader = value;
            }
        }

        public Scheduler2(Download d)
        {
            if (null == d || null == d.SummarySection || null == d.Sections || d.Sections.Count == 0)
            {
                throw new ArgumentNullException(nameof(d));
            }
            download = d;
            if (download.SummarySection.DownloadStatus == DownloadStatus.Downloading)
            {
                download.SummarySection.DownloadStatus = DownloadStatus.Stopped;
            }
        }

        private int FindFreeDownloader()
        {
            for (int i = 0; i < NoDownloader; i++)
            {
                if (downloaders[i] == null || !downloaders[i].IsBusy()) return i;
            }
            return (-1);
        }

        private void StopDownload()
        {
            for (int i = 0; i < NoDownloader; i++)
            {
                if (downloaders[i] != null)
                {
                    downloaders[i].StopDownloading();
                }
            }
        }

        private int FindDownloaderBySection(DownloadSection ds)
        {
            for (int i = 0; i < NoDownloader; i++)
            {
                if (downloaders[i] != null && downloaders[i].DownloadSection == ds) return i;
            }
            return (-1);
        }

        private void DownloadSectionWithFreeDownloaderIfPossible(DownloadSection ds)
        {
            int freeDownloaderIndex = FindFreeDownloader();
            if (freeDownloaderIndex >= 0)
            {
                if (downloaders[freeDownloaderIndex] == null)
                {
                    downloaders[freeDownloaderIndex] = new Downloader(ds);
                }
                else
                {
                    downloaders[freeDownloaderIndex].ChangeDownloadSection(ds);
                }
                downloaders[freeDownloaderIndex].StartDownloading();
            }
        }

        private void AutoDownloadSection(DownloadSection ds)
        {
            int downloaderIndex = FindDownloaderBySection(ds);
            if (downloaderIndex >= 0)
            {
                downloaders[downloaderIndex].StartDownloading();
            }
            else
            {
                DownloadSectionWithFreeDownloaderIfPossible(ds);
            }
        }

        private bool ErrorSectionExists()
        {
            bool ret = false;
            for (int i = 0; i < download.Sections.Count; i++)
            {
                if (download.Sections[i].DownloadStatus == DownloadStatus.DownloadError)
                {
                    ret = true;
                    break;
                }
            }
            return ret;
        }

        private void CreateNewSectionIfFeasible()
        {
            if (ErrorSectionExists() || FindFreeDownloader() == (-1)) return;
            int biggestBeingDownloadedSection = (-1);
            long biggestDownloadingSectionSize = 0;
            // find current biggest downloading section
            for (int i = 0; i < download.Sections.Count; i++)
            {
                DownloadStatus ds = download.Sections[i].DownloadStatus;
                if (ds == DownloadStatus.Downloading && download.Sections[i].HttpStatusCode == System.Net.HttpStatusCode.PartialContent)
                {
                    long bytesDownloaded = download.Sections[i].BytesDownloaded;
                    if (bytesDownloaded > 0 && download.Sections[i].Total - bytesDownloaded > biggestDownloadingSectionSize)
                    {
                        biggestDownloadingSectionSize = download.Sections[i].Total - bytesDownloaded;
                        biggestBeingDownloadedSection = i;
                    }
                }
            }
            if (biggestBeingDownloadedSection < 0) return;
            // if section size is big enough, split the section to two(creating a new download section)
            if (biggestDownloadingSectionSize / 2 > minSectionSize)
            {
                lock (sectionsLock)
                {
                    DownloadSection newSection = download.Sections[biggestBeingDownloadedSection].Split();
                    if (newSection != null) download.Sections.Add(newSection);
                }
            }
        }

        private bool DealWithSectionAnormalies()
        {
            int http200Secs = 0;
            int http206Secs = 0;
            bool anormalyFound = false;
            foreach (DownloadSection ds in download.Sections)
            {
                if (ds.DownloadStatus == DownloadStatus.LogicalErrorOrCancelled)
                {
                    anormalyFound = true;
                    break;
                }
                HttpStatusCode code = ds.HttpStatusCode;
                if (code == HttpStatusCode.OK) http200Secs++;
                if (code == HttpStatusCode.PartialContent) http206Secs++;
            }
            if (http200Secs > 1) anormalyFound = true;
            if (http206Secs > 0 && http200Secs > 0) anormalyFound = true;
            if (anormalyFound)
            {
                StopDownload();
                foreach (DownloadSection ds in download.Sections)
                {
                    ds.DownloadStatus = DownloadStatus.LogicalErrorOrCancelled;
                }
            }
            return anormalyFound;
        }

        private void TryDownloadingAllUnfinishedSections()
        {
            for (int i = 0; i < download.Sections.Count; i++)
            {
                DownloadStatus ds = download.Sections[i].DownloadStatus;
                if (ds == DownloadStatus.Stopped || ds == DownloadStatus.DownloadError)
                {
                    AutoDownloadSection(download.Sections[i]);
                }
            }
        }

        private bool ProcessSections()
        {
            if (DealWithSectionAnormalies()) return false;
            CreateNewSectionIfFeasible();
            TryDownloadingAllUnfinishedSections();
            return true;
        }

        private DownloadStatus GetDownloadStatus()
        {
            return download.SummarySection.DownloadStatus;
        }

        public ProgressData GetDownloadStatusData()
        {
            ProgressData pd = new();
            pd.DownloadId = download.SummarySection.Id;
            pd.DownloadView.Url = download.SummarySection.Url;
            pd.DownloadView.DownloadFolder = download.DownloadFolder;
            pd.DownloadView.Id = download.SummarySection.Id;
            if (IsDownloadFinished())
            {
                pd.DownloadView.FileName = download.SummarySection.FileName;
            }
            else
            {
                pd.DownloadView.FileName = Util.getDownloadFileNameFromDownloadSection(download.SummarySection);
            }
            pd.DownloadView.Size = Util.getShortFileSize(download.SummarySection.Total);
            pd.DownloadView.Status = GetDownloadStatus();
            pd.DownloadView.Error = this.exMessage == null ? string.Empty : this.exMessage.Message;
            pd.DownloadView.DownloadGroup = download.DownloadGroup;

            long total = download.SummarySection.Total;
            long totalDownloaded = 0;
            StringBuilder sb = new();
            lock (sectionsLock)
            {
                DownloadSection ds = download.Sections[0];
                do
                {
                    SectionView sv = new();
                    sv.HttpStatusCode = ds.HttpStatusCode;
                    sv.Status = ds.DownloadStatus;
                    long secTotal = ds.Total, secDownloaded = ds.BytesDownloaded;
                    // if a 206 section has been splitted before, downloader could download a little more than needed.
                    if (secTotal > 0 && secDownloaded > secTotal) secDownloaded = secTotal;
                    totalDownloaded += secDownloaded;
                    sv.Size = Util.getShortFileSize(secTotal);
                    sv.Progress = Util.getProgress(secDownloaded, secTotal);
                    sv.Error = ds.Error;
                    pd.SectionViews.Add(sv);
                    if (total > 0)
                    {
                        // make sure there are set number of squares for each section
                        decimal totalSectionSquares = Math.Round((decimal)secTotal * 200m / (decimal)total, MidpointRounding.AwayFromZero);
                        decimal downloadedSquares = Math.Round((decimal)secDownloaded * 200m / (decimal)total, MidpointRounding.AwayFromZero);
                        decimal pendingSquares = totalSectionSquares - downloadedSquares;
                        for (long i = 0; i < downloadedSquares; i++) sb.Append('\u2593');
                        for (long i = 0; i < pendingSquares; i++) sb.Append('\u2591');
                    }
                    ds = ds.NextSection;
                }
                while (ds != null);
            }
            download.SummarySection.BytesDownloaded = totalDownloaded;
            pd.ProgressBar = sb.ToString();
            sc.RegisterBytes(totalDownloaded);
            pd.DownloadView.Speed = Util.getShortFileSize(sc.GetSpeed()) + "/sec";
            pd.DownloadView.Progress = Util.getProgress(totalDownloaded, total);

            return pd;
        }

        private bool IsDownloadHalted()
        {
            for (int i = 0; i < download.Sections.Count; i++)
            {
                DownloadStatus ds = download.Sections[i].DownloadStatus;
                if (ds == DownloadStatus.Stopped || ds == DownloadStatus.DownloadError ||
                    ds == DownloadStatus.PrepareToDownload || ds == DownloadStatus.Downloading)
                    return false;
            }
            return true;
        }

        private void JoinSectionsToFile()
        {
            DownloadSection ds = download.Sections[0];
            Stream streamDest = null, streamSection = null;
            byte[] buffer = new byte[bufferSize];
            string fileNameWithPath;
            try
            {
                if (!string.IsNullOrEmpty(download.DownloadFolder) && Directory.Exists(download.DownloadFolder))
                {
                    string fileNameOnly = Util.getDownloadFileNameFromDownloadSection(ds);
                    fileNameWithPath = Path.Combine(download.DownloadFolder, fileNameOnly);
                    if (File.Exists(fileNameWithPath))
                    {
                        fileNameOnly = DateTime.Now.ToString("MMMdd-HHmmss.fff") + " " + fileNameOnly;
                        fileNameWithPath = Path.Combine(download.DownloadFolder, fileNameOnly);
                    }
                }
                else
                {
                    throw new DirectoryNotFoundException("Download folder is not present.");
                }
                streamDest = File.OpenWrite(fileNameWithPath);
                long totalFileSize = 0;
                while (true)
                {
                    if (ds.DownloadStatus == DownloadStatus.Finished)
                    {
                        totalFileSize += ds.Total;
                        streamSection = File.OpenRead(ds.FileName);
                        long bytesRead = 0;
                        while (true)
                        {
                            long bytesToReadThisTime = bufferSize;
                            if (ds.Total > 0) bytesToReadThisTime = (ds.Total - bytesRead >= bufferSize) ? bufferSize : (ds.Total - bytesRead);
                            long bytesReadThisTime = streamSection.Read(buffer, 0, (int)bytesToReadThisTime);
                            // stream reached the end
                            if (bytesReadThisTime == 0) break;
                            bytesRead += bytesReadThisTime;
                            streamDest.Write(buffer, 0, (int)bytesReadThisTime);
                            if (ds.Total > 0 && bytesRead >= ds.Total) break;
                        }
                        streamSection.Close();
                    }
                    if (ds.NextSection != null) ds = ds.NextSection;
                    else break;
                }
                streamDest.Close();
                download.SummarySection.FileName = fileNameWithPath;
                download.SummarySection.Start = download.Sections[0].Start;
                download.SummarySection.End = download.SummarySection.Start + totalFileSize - 1;
            }
            finally
            {
                if (streamDest != null) streamDest.Close();
                if (streamSection != null) streamSection.Close();
            }
        }

        private void CleanTempFiles()
        {
            try
            {
                foreach (DownloadSection ds in download.Sections)
                {
                    if (File.Exists(ds.FileName)) File.Delete(ds.FileName);
                }
            }
            catch
            {
            }
        }

        public bool IsDownloadFinished()
        {
            return download.SummarySection.DownloadStatus == DownloadStatus.Finished;
        }

        public bool IsDownloading()
        {
            return download.SummarySection.DownloadStatus == DownloadStatus.Downloading;
        }

        public bool IsDownloadResumable()
        {
            lock (sectionsLock)
            {
                foreach (DownloadSection ds in download.Sections)
                {
                    if (ds.HttpStatusCode != 0 && ds.HttpStatusCode != System.Net.HttpStatusCode.PartialContent)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public void Stop(bool cancel, bool wait)
        {
            if (IsDownloading())
            {
                this.downloadStopFlag = true;
                if (cancel)
                {
                    if (downloadThread != null && downloadThread.IsAlive) downloadThread.Join();
                    CleanTempFiles();
                }
                else
                {
                    if (wait)
                    {
                        if (downloadThread != null && downloadThread.IsAlive) downloadThread.Join();
                    }
                }
            }
            else if (cancel) CleanTempFiles();
        }

        public void Start()
        {
            if (IsDownloadFinished() || IsDownloading()) return;
            download.SummarySection.DownloadStatus = DownloadStatus.Downloading;
            this.exMessage = null;
            sc = new();
            this.downloadStopFlag = false;
            downloadThread = new(new ThreadStart(DownloadThreadProc));
            downloadThread.Start();
        }

        private void DownloadThreadProc()
        {
            while (true)
            {
                // if there is download stop request from other thread
                if (this.downloadStopFlag)
                {
                    StopDownload();
                    download.SummarySection.DownloadStatus = DownloadStatus.Stopped;
                    return;
                }
                if (!ProcessSections())
                {
                    this.exMessage = new Exception("Section anomalies exist. Try to re-download with single thread.");
                    download.SummarySection.DownloadStatus = DownloadStatus.DownloadError;
                    return;
                }
                Thread.Sleep(500);
                if (IsDownloadHalted()) break;
            }
            try
            {
                JoinSectionsToFile();
            }
            catch (Exception ex)
            {
                this.exMessage = ex;
                download.SummarySection.DownloadStatus = DownloadStatus.DownloadError;
                return;
            }
            CleanTempFiles();
            this.exMessage = null;
            download.SummarySection.DownloadStatus = DownloadStatus.Finished;
        }
    }
}
