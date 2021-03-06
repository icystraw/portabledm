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
        private readonly object sectionsLock = new();

        private DownloadSection sectionBeingEvaluated;
        private ProgressData pd;

        private bool downloadStopFlag = false;
        private SpeedCalculator sc = new();

        private Thread downloadThread;

        public Download Download => download;

        public ProgressData ProgressData { get => pd; }

        public Scheduler2(Download d)
        {
            if (null == d || null == d.SummarySection || null == d.Sections || d.Sections.Count == 0)
            {
                throw new ArgumentNullException(nameof(d));
            }
            if (d.NoDownloader <= 0 || d.NoDownloader > maxNoDownloader) throw new ArgumentOutOfRangeException(nameof(d), "Number of download threads is out of range.");
            download = d;
            if (download.SummarySection.DownloadStatus == DownloadStatus.Downloading)
            {
                download.SummarySection.DownloadStatus = DownloadStatus.Stopped;
            }
            InitProgressData();
        }

        private void InitProgressData()
        {
            pd = new();
            pd.DownloadView.Id = download.SummarySection.Id;
            pd.DownloadView.Tag = this;
            pd.DownloadView.Url = download.SummarySection.Url;
            pd.DownloadView.DownloadGroup = download.DownloadGroup;
            pd.DownloadView.LastModified = download.SummarySection.LastModified == DateTimeOffset.MaxValue ? "Not available" : download.SummarySection.LastModified.ToLocalTime().ToString();
            pd.DownloadView.Total = download.SummarySection.Total;
            pd.DownloadView.Size = Util.GetEasyToUnderstandFileSize(pd.DownloadView.Total);
            pd.DownloadView.Status = download.SummarySection.DownloadStatus;
            if (pd.DownloadView.Status != DownloadStatus.Finished)
            {
                pd.DownloadView.FileName = Util.GetDownloadFileNameFromDownloadSection(download.SummarySection);
            }
            RefreshDownloadStatusData(true);
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
            for (int i = 0; i < download.NoDownloader; i++)
            {
                if (downloaders[i] != null)
                {
                    downloaders[i].WaitForFinish();
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
            if (sectionBeingEvaluated != null) return true;
            return false;
        }

        private void EvaluateStatusOfJustCreatedSectionIfExists()
        {
            if (null == sectionBeingEvaluated) return;

            DownloadStatus ds = sectionBeingEvaluated.DownloadStatus;
            if (ds == DownloadStatus.DownloadError || ds == DownloadStatus.LogicalError)
            {
                // fail to create new section. Throw this section away.
                sectionBeingEvaluated = null;
                return;
            }
            // section creation successful
            if (ds == DownloadStatus.Downloading || ds == DownloadStatus.Finished)
            {
                // add the new section to section chain
                DownloadSection parent = sectionBeingEvaluated.Tag as DownloadSection;
                sectionBeingEvaluated.NextSection = parent.NextSection;
                if (parent.NextSection != null) sectionBeingEvaluated.NextSectionId = parent.NextSection.Id;
                sectionBeingEvaluated.Tag = null;
                lock (sectionsLock)
                {
                    // Downloader class has been designed in a way which won't cause havoc if Scheduler class does this
                    parent.NextSection = sectionBeingEvaluated;
                    parent.NextSectionId = sectionBeingEvaluated.Id;
                    parent.End = sectionBeingEvaluated.Start - 1;
                    download.Sections.Add(sectionBeingEvaluated);
                }
                sectionBeingEvaluated = null;
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
                sectionBeingEvaluated = download.Sections[biggestBeingDownloadedSection].Split();
            }
        }

        private void TryDownloadingAllUnfinishedSections()
        {
            if (sectionBeingEvaluated != null && sectionBeingEvaluated.DownloadStatus == DownloadStatus.Stopped)
            {
                AutoDownloadSection(sectionBeingEvaluated);
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

        public void RefreshDownloadStatusData(bool forced)
        {
            DownloadStatus newStatus = download.SummarySection.DownloadStatus;
            DownloadStatus oldStatus = pd.DownloadView.Status;
            pd.DownloadView.StayedInactive = (oldStatus == DownloadStatus.Stopped && newStatus == DownloadStatus.Stopped) || oldStatus == DownloadStatus.Finished;
            if (!forced && pd.DownloadView.StayedInactive) return;

            pd.SectionViews.Clear();
            pd.DownloadView.Status = newStatus;
            pd.DownloadView.DownloadFolder = download.DownloadFolder;
            pd.DownloadView.Error = download.SummarySection.Error;
            if (pd.DownloadView.Status == DownloadStatus.Finished)
            {
                pd.DownloadView.Total = download.SummarySection.Total;
                pd.DownloadView.Size = Util.GetEasyToUnderstandFileSize(pd.DownloadView.Total);
                pd.DownloadView.FileName = download.SummarySection.FileName;
                pd.DownloadView.Speed = string.Empty;
                pd.DownloadView.Progress = 100;
                pd.DownloadView.Eta = string.Empty;
                return;
            }

            long total = pd.DownloadView.Total;
            long totalDownloaded = 0;
            lock (sectionsLock)
            {
                DownloadSection ds = download.Sections[0];
                do
                {
                    SectionView sv = new();
                    sv.HttpStatusCode = ds.HttpStatusCode;
                    sv.Status = ds.DownloadStatus;
                    sv.Total = ds.Total;
                    sv.BytesDownloaded = ds.BytesDownloaded;
                    // if a 206 section has been splitted before, downloader could download a little more than needed.
                    if (sv.Total > 0 && sv.BytesDownloaded > sv.Total) sv.BytesDownloaded = sv.Total;
                    totalDownloaded += sv.BytesDownloaded;
                    sv.Progress = Util.CalculateProgress(sv.BytesDownloaded, sv.Total);
                    sv.Error = ds.Error;
                    pd.SectionViews.Add(sv);
                    ds = ds.NextSection;
                }
                while (ds != null);
            }
            download.SummarySection.BytesDownloaded = totalDownloaded;
            pd.DownloadView.Progress = Util.CalculateProgress(totalDownloaded, total);
            if (pd.DownloadView.Status == DownloadStatus.Stopped || pd.DownloadView.Status == DownloadStatus.DownloadError)
            {
                pd.DownloadView.Speed = string.Empty;
                pd.DownloadView.Eta = string.Empty;
                return;
            }
            sc.RegisterBytes(totalDownloaded);
            long speed = sc.GetSpeed();
            pd.DownloadView.Speed = Util.GetEasyToUnderstandFileSize(speed) + "/sec";
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
            if (sectionBeingEvaluated != null) return false;
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
                            long bytesToReadThisTime = (ds.Total - bytesRead >= bufferSize) ? bufferSize : (ds.Total - bytesRead);
                            // stream reached the end
                            if (bytesToReadThisTime == 0) break;
                            long bytesReadThisTime = streamSection.Read(buffer, 0, (int)bytesToReadThisTime);
                            bytesRead += bytesReadThisTime;
                            streamDest.Write(buffer, 0, (int)bytesReadThisTime);
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
                if (sectionBeingEvaluated != null)
                {
                    if (File.Exists(sectionBeingEvaluated.FileName)) File.Delete(sectionBeingEvaluated.FileName);
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
                downloadStopFlag = true;
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
            downloadStopFlag = false;
            download.SummarySection.DownloadStatus = DownloadStatus.Downloading;
            download.SummarySection.Error = string.Empty;
            sc = new();
            downloadThread = new(new ThreadStart(DownloadThreadProc));
            downloadThread.Start();
        }

        private void DownloadThreadProc()
        {
            while (true)
            {
                // if there is download stop request from other thread
                if (downloadStopFlag)
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
                download.SummarySection.Error = "There are sections that are in invalid states. Download cannot continue. Try re-download this file.";
                download.SummarySection.DownloadStatus = DownloadStatus.DownloadError;
                return;
            }
            try
            {
                JoinSectionsToFile();
            }
            catch (Exception ex)
            {
                download.SummarySection.Error = ex.Message;
                download.SummarySection.DownloadStatus = DownloadStatus.DownloadError;
                return;
            }
            CleanTempFiles();
            download.SummarySection.DownloadStatus = DownloadStatus.Finished;
        }
    }
}
