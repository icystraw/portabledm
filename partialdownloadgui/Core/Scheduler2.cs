using System;
using System.IO;
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
        private readonly ProgressData pd;
        private readonly object sectionsLock = new();

        private DownloadSection sectionBeingEvaluated;

        private bool downloadStopFlag = false;
        private SpeedCalculator sc = new();

        private Thread downloadThread;
        private Exception exMessage;

        public Download Download => download;

        public ProgressData ProgressData => pd;

        public Scheduler2(Download d)
        {
            if (null == d || null == d.SummarySection || null == d.Sections || d.Sections.Count == 0)
            {
                throw new ArgumentNullException(nameof(d));
            }
            if (d.NoDownloader == 0) throw new ArgumentOutOfRangeException(nameof(d), "Number of download threads cannot be zero.");
            download = d;
            if (download.SummarySection.DownloadStatus == DownloadStatus.Downloading)
            {
                download.SummarySection.DownloadStatus = DownloadStatus.Stopped;
            }
            pd = new();
            pd.DownloadView.Id = download.SummarySection.Id;
            pd.DownloadView.Tag = this;
            pd.DownloadView.Url = download.SummarySection.Url;
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

        private bool ErrorAndUnstableSectionsExist()
        {
            foreach (DownloadSection ds in download.Sections)
            {
                DownloadStatus status = ds.DownloadStatus;
                if (status == DownloadStatus.DownloadError || status == DownloadStatus.LogicalError || status == DownloadStatus.PrepareToDownload)
                {
                    return true;
                }
            }
            if (this.sectionBeingEvaluated != null) return true;
            return false;
        }

        private void EvaluateStatusOfJustCreatedSectionIfExists()
        {
            if (null == this.sectionBeingEvaluated) return;

            DownloadStatus ds = this.sectionBeingEvaluated.DownloadStatus;
            if (ds == DownloadStatus.DownloadError || ds == DownloadStatus.LogicalError)
            {
                // fail to create new section. Throw this section away.
                this.sectionBeingEvaluated = null;
                return;
            }
            // section creation successful
            if (ds == DownloadStatus.Downloading || ds == DownloadStatus.Finished)
            {
                // add the new section to section chain
                DownloadSection parent = this.sectionBeingEvaluated.Tag as DownloadSection;
                this.sectionBeingEvaluated.NextSection = parent.NextSection;
                if (parent.NextSection != null) this.sectionBeingEvaluated.NextSectionId = parent.NextSection.Id;
                this.sectionBeingEvaluated.Tag = null;
                lock (sectionsLock)
                {
                    // Downloader class has been designed in a way which won't cause havoc if Scheduler class does this
                    parent.NextSection = this.sectionBeingEvaluated;
                    parent.NextSectionId = this.sectionBeingEvaluated.Id;
                    parent.End = this.sectionBeingEvaluated.Start - 1;
                    download.Sections.Add(this.sectionBeingEvaluated);
                }
                this.sectionBeingEvaluated = null;
            }
        }

        private void CreateNewSectionIfFeasible()
        {
            if (ErrorAndUnstableSectionsExist() || FindFreeDownloader() == (-1)) return;
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
            // and start downloading the new section without adjusting the size of the old section.
            if (biggestDownloadingSectionSize / 2 > minSectionSize)
            {
                this.sectionBeingEvaluated = download.Sections[biggestBeingDownloadedSection].Split();
            }
        }

        private void TryDownloadingAllUnfinishedSections()
        {
            if (this.sectionBeingEvaluated != null && this.sectionBeingEvaluated.DownloadStatus == DownloadStatus.Stopped)
            {
                AutoDownloadSection(this.sectionBeingEvaluated);
            }
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
            EvaluateStatusOfJustCreatedSectionIfExists();
            CreateNewSectionIfFeasible();
            TryDownloadingAllUnfinishedSections();
        }

        public void RefreshDownloadStatusData()
        {
            pd.SectionViews.Clear();
            pd.ProgressBar = string.Empty;

            pd.DownloadView.LastModified = download.SummarySection.LastModified == DateTimeOffset.MaxValue ? "Not available" : download.SummarySection.LastModified.ToLocalTime().ToString();
            pd.DownloadView.DownloadFolder = download.DownloadFolder;
            pd.DownloadView.Size = Util.GetEasyToUnderstandFileSize(download.SummarySection.Total);
            pd.DownloadView.Status = download.SummarySection.DownloadStatus;
            pd.DownloadView.Error = this.exMessage == null ? string.Empty : this.exMessage.Message;
            pd.DownloadView.DownloadGroup = download.DownloadGroup;
            if (pd.DownloadView.Status == DownloadStatus.Finished)
            {
                pd.DownloadView.FileName = download.SummarySection.FileName;
                pd.DownloadView.Speed = string.Empty;
                pd.DownloadView.Progress = 100;
                pd.DownloadView.Eta = string.Empty;
                return;
            }
            else
            {
                pd.DownloadView.FileName = Util.GetDownloadFileNameFromDownloadSection(download.SummarySection);
            }

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
            long speed = sc.GetSpeed();
            pd.DownloadView.Speed = Util.GetEasyToUnderstandFileSize(speed) + "/sec";
            pd.DownloadView.Progress = Util.CalculateProgress(totalDownloaded, total);
            pd.DownloadView.Eta = Util.CalculateEta(total - totalDownloaded, speed);
        }

        private bool IsDownloadHalted()
        {
            foreach (DownloadSection ds in download.Sections)
            {
                DownloadStatus status = ds.DownloadStatus;
                if (status == DownloadStatus.Stopped || status == DownloadStatus.DownloadError ||
                    status == DownloadStatus.PrepareToDownload || status == DownloadStatus.Downloading)
                    return false;
            }
            if (this.sectionBeingEvaluated != null) return false;
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
                download.SummarySection.BytesDownloaded = totalFileSize;
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
            // if there is section with logical error
            if (ErrorAndUnstableSectionsExist())
            {
                this.exMessage = new InvalidOperationException("There are sections that are in invalid states. Download cannot continue. Try re-download this file.");
                download.SummarySection.DownloadStatus = DownloadStatus.DownloadError;
                return;
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
