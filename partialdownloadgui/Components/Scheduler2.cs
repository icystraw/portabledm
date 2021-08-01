using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        public Exception ExMessage { get => exMessage; }

        public Scheduler2(Download d)
        {
            if (null == d || null == d.SummarySection || null == d.Sections || d.Sections.Count == 0)
            {
                throw new ArgumentNullException(nameof(d));
            }
            download = d;
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

        private void StopDownloadExcept(DownloadSection ds)
        {
            for (int i = 0; i < NoDownloader; i++)
            {
                if (downloaders[i] != null && downloaders[i].DownloadSection != ds)
                {
                    downloaders[i].StopDownloading();
                }
            }
        }

        private void CancelSectionsExcept(DownloadSection ds)
        {
            foreach (DownloadSection section in download.Sections)
            {
                if (section != ds) section.DownloadStatus = DownloadStatus.ParameterError;
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

        private bool CancelOtherSectionsIf200SectionExists()
        {
            for (int i = 0; i < download.Sections.Count; i++)
            {
                DownloadStatus ds = download.Sections[i].DownloadStatus;
                // if there is a downloading section with HTTP 200, don't make more sections and cancel other sections
                if (ds == DownloadStatus.Downloading && download.Sections[i].HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    StopDownloadExcept(download.Sections[i]);
                    CancelSectionsExcept(download.Sections[i]);
                    return true;
                }
            }
            return false;
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

        private void ProcessSections()
        {
            if (download.SummarySection.DownloadStatus == DownloadStatus.Finished) return;
            if (CancelOtherSectionsIf200SectionExists()) return;
            CreateNewSectionIfFeasible();
            TryDownloadingAllUnfinishedSections();
        }

        private DownloadStatus GetDownloadStatus()
        {
            if (exMessage != null) return DownloadStatus.DownloadError;
            if (IsDownloading()) return DownloadStatus.Downloading;
            if (IsDownloadFinished()) return DownloadStatus.Finished;
            return DownloadStatus.Stopped;
        }

        public ProgressView GetDownloadStatusView()
        {
            ProgressView pv = new();
            pv.DownloadId = download.SummarySection.Id;
            pv.DownloadView.Id = download.SummarySection.Id;
            pv.DownloadView.FileName = Util.getDownloadFileNameFromDownloadSection(download.SummarySection);
            pv.DownloadView.Size = Util.getShortFileSize(download.SummarySection.Total);
            pv.DownloadView.Status = GetDownloadStatus().ToString();

            long total = download.SummarySection.Total;
            long totalDownloaded = 0;
            StringBuilder sb = new();
            lock (sectionsLock)
            {
                DownloadSection ds = download.Sections[0];
                do
                {
                    SectionView sv = new();
                    sv.Description = ds.HttpStatusCode.ToString();
                    sv.Status = ds.DownloadStatus.ToString();
                    long secTotal = ds.Total, secDownloaded = ds.BytesDownloaded;
                    totalDownloaded += secDownloaded;
                    sv.Size = Util.getShortFileSize(secTotal);
                    sv.Progress = Util.getProgress(secDownloaded, secTotal);
                    pv.SectionViews.Add(sv);
                    if (total > 0)
                    {
                        long downloadedSquares = secDownloaded * 200 / total;
                        long pendingSquares = secTotal * 200 / total - downloadedSquares;
                        for (long i = 0; i < downloadedSquares; i++) sb.Append('\u2593');
                        for (long i = 0; i < pendingSquares; i++) sb.Append('\u2591');
                    }
                    ds = ds.NextSection;
                }
                while (ds != null);
            }
            download.SummarySection.BytesDownloaded = totalDownloaded;
            pv.ProgressBar = sb.ToString();
            sc.RegisterBytes(totalDownloaded);
            pv.DownloadView.Speed = Util.getShortFileSize(sc.GetSpeed()) + "/sec";
            pv.DownloadView.Progress = Util.getProgress(totalDownloaded, total);

            return pv;
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
            if (download.Sections.Count == 0) return;
            // make sure the download cannot continue
            if (!IsDownloadHalted()) return;
            // is download already finished?
            if (download.SummarySection.DownloadStatus == DownloadStatus.Finished) return;

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
                        fileNameOnly = DateTime.Now.ToString("yyyy-MMM-dd-HH-mm-ss") + " " + fileNameOnly;
                        fileNameWithPath = Path.Combine(download.DownloadFolder, fileNameOnly);
                    }
                }
                else
                {
                    throw new DirectoryNotFoundException("Download folder is not present.");
                }
                streamDest = File.OpenWrite(fileNameWithPath);
                while (true)
                {
                    if (ds.DownloadStatus == DownloadStatus.Finished)
                    {
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
                download.SummarySection.DownloadStatus = DownloadStatus.Finished;
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
            if (download.SummarySection.DownloadStatus == DownloadStatus.Finished) return true;
            return false;
        }

        public bool IsDownloading()
        {
            if (downloadThread != null && downloadThread.IsAlive) return true;
            return false;
        }

        public void Stop(bool cancel)
        {
            if (IsDownloading())
            {
                this.downloadStopFlag = true;
                downloadThread.Join();
                this.downloadStopFlag = false;
            }
            if (cancel) CleanTempFiles();
        }

        public void Start()
        {
            if (download.SummarySection.DownloadStatus == DownloadStatus.Finished) return;
            if (IsDownloading()) return;
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
                    return;
                }
                ProcessSections();
                Thread.Sleep(500);
                if (IsDownloadHalted()) break;
            }
            try
            {
                JoinSectionsToFile();
                CleanTempFiles();
                this.exMessage = null;
            }
            catch (Exception ex)
            {
                this.exMessage = ex;
            }
        }
    }
}
