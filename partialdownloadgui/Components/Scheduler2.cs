﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private void CancelOtherSectionsIf200SectionExists()
        {
            for (int i = 0; i < download.Sections.Count; i++)
            {
                DownloadStatus ds = download.Sections[i].DownloadStatus;
                // if there is a downloading section with HTTP 200, don't make more sections and cancel other sections
                if (ds == DownloadStatus.Downloading && download.Sections[i].HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    StopDownloadExcept(download.Sections[i]);
                    CancelSectionsExcept(download.Sections[i]);
                    return;
                }
            }
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
            CancelOtherSectionsIf200SectionExists();
            CreateNewSectionIfFeasible();
            TryDownloadingAllUnfinishedSections();
        }

        public string GetDownloadStatus(out List<ProgressView> view)
        {
            List<ProgressView> progressViewItems = new();
            view = progressViewItems;
            lock (sectionsLock)
            {
                if (download.Sections.Count == 0) return string.Empty;

                DownloadSection ds = download.Sections[0];
                long total = download.SummarySection.Total;
                long totalDownloaded = 0;
                StringBuilder sb = new();
                do
                {
                    long secTotal = ds.Total;
                    long secDownloaded = ds.BytesDownloaded;
                    DownloadStatus status = ds.DownloadStatus;
                    int httpStatusCode = (int)ds.HttpStatusCode;
                    totalDownloaded += secDownloaded;
                    ProgressView pv = new();
                    pv.Section = "HTTP Response: " + httpStatusCode;
                    pv.Size = Util.getShortFileSize(secTotal);
                    if (status == DownloadStatus.Downloading || status == DownloadStatus.PrepareToDownload) pv.StatusImage = "downloading";
                    else if (status == DownloadStatus.DownloadError) pv.StatusImage = "error";
                    else if (status == DownloadStatus.Finished) pv.StatusImage = "finished";
                    else pv.StatusImage = string.Empty;
                    if (secTotal > 0)
                    {
                        pv.Progress = (secDownloaded * 100 / secTotal > 100 ? 100 : secDownloaded * 100 / secTotal);
                    }
                    else
                    {
                        pv.Progress = 0;
                    }
                    progressViewItems.Add(pv);
                    if (total > 0)
                    {
                        long downloadedSquares = secDownloaded * 200 / total;
                        long pendingSquares = secTotal * 200 / total - downloadedSquares;
                        for (long i = 0; i < downloadedSquares; i++)
                        {
                            sb.Append('\u2593');
                        }
                        for (long i = 0; i < pendingSquares; i++)
                        {
                            sb.Append('\u2591');
                        }
                    }
                    ds = ds.NextSection;
                }
                while (ds != null);
                download.SummarySection.BytesDownloaded = totalDownloaded;
                return sb.ToString();
            }
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

            DownloadSection ds = download.Sections[0];
            Stream streamDest = null, streamSection = null;
            byte[] buffer = new byte[bufferSize];
            string fileNameWithPath;
            try
            {
                if (!string.IsNullOrEmpty(download.DownloadFolder) && Directory.Exists(download.DownloadFolder))
                {
                    string fileNameOnly;
                    if (!string.IsNullOrEmpty(ds.SuggestedName))
                    {
                        fileNameOnly = Util.removeInvalidCharFromFileName(ds.SuggestedName);
                    }
                    else
                    {
                        fileNameOnly = Util.getFileName(ds.Url);
                    }
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

        public void CleanTempFiles()
        {
            if (IsDownloading()) return;
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

        public void Stop(bool cancel)
        {
            if (!IsDownloading()) return;
            this.downloadStopFlag = true;
            downloadThread.Join();
            this.downloadStopFlag = false;
            if (cancel) CleanTempFiles();
        }

        public void Start()
        {
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
            }
            catch (Exception ex)
            {
                this.exMessage = ex;
            }
        }
    }
}
