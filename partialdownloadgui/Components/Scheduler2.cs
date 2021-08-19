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
            for (int i = 0; i < download.NoDownloader; i++)
            {
                if (downloaders[i] == null || !downloaders[i].IsBusy()) return i;
            }
            return (-1);
        }

        private void StopDownload()
        {
            for (int i = 0; i < download.NoDownloader; i++)
            {
                if (downloaders[i] != null)
                {
                    downloaders[i].StopDownloading();
                }
            }
        }

        private int FindDownloaderBySection(DownloadSection ds)
        {
            for (int i = 0; i < download.NoDownloader; i++)
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

        private bool ErrorAndUnstableSectionExists()
        {
            bool ret = false;
            foreach (DownloadSection ds in download.Sections)
            {
                DownloadStatus status = ds.DownloadStatus;
                if (status == DownloadStatus.DownloadError || status == DownloadStatus.LogicalError || status == DownloadStatus.PrepareToDownload)
                {
                    ret = true;
                    break;
                }
            }
            return ret;
        }

        private void CreateNewSectionIfFeasible()
        {
            if (ErrorAndUnstableSectionExists() || FindFreeDownloader() == (-1)) return;
            int biggestBeingDownloadedSection = (-1);
            long biggestDownloadingSectionSize = 0;
            // find current biggest downloading section
            for (int i = 0; i < download.Sections.Count; i++)
            {
                DownloadSection ds = download.Sections[i];
                if (ds.DownloadStatus == DownloadStatus.Downloading && ds.HttpStatusCode == System.Net.HttpStatusCode.PartialContent)
                {
                    long bytesDownloaded = ds.BytesDownloaded;
                    if (bytesDownloaded > 0 && ds.Total - bytesDownloaded > biggestDownloadingSectionSize)
                    {
                        biggestDownloadingSectionSize = ds.Total - bytesDownloaded;
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

        private void TryDownloadingAllUnfinishedSections()
        {
            foreach (DownloadSection ds in download.Sections)
            {
                DownloadStatus status = ds.DownloadStatus;
                if (status == DownloadStatus.Stopped || status == DownloadStatus.DownloadError)
                {
                    AutoDownloadSection(ds);
                }
            }
        }

        private void ProcessSections()
        {
            CreateNewSectionIfFeasible();
            TryDownloadingAllUnfinishedSections();
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
                pd.DownloadView.FileName = Util.GetDownloadFileNameFromDownloadSection(download.SummarySection);
            }
            pd.DownloadView.Size = Util.GetEasyToUnderstandFileSize(download.SummarySection.Total);
            pd.DownloadView.Status = download.SummarySection.DownloadStatus;
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
                    sv.Size = Util.GetEasyToUnderstandFileSize(secTotal);
                    sv.Progress = Util.CalculateProgress(secDownloaded, secTotal);
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
            pd.DownloadView.Speed = Util.GetEasyToUnderstandFileSize(sc.GetSpeed()) + "/sec";
            pd.DownloadView.Progress = Util.CalculateProgress(totalDownloaded, total);

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
                    string fileNameOnly = Util.GetDownloadFileNameFromDownloadSection(ds);
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
                    if (ds.HttpStatusCode == System.Net.HttpStatusCode.OK)
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
                ProcessSections();
                Thread.Sleep(500);
                if (IsDownloadHalted()) break;
            }
            if (ErrorAndUnstableSectionExists()) return;
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
