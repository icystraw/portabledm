using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        private bool errorExist = false;
        private bool downloadStopFlag = false;
        private SpeedCalculator sc = new();

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

        private void SearchForError()
        {
            errorExist = false;
            for (int i = 0; i < download.Sections.Count; i++)
            {
                if (download.Sections[i].DownloadStatus == DownloadStatus.DownloadError)
                {
                    errorExist = true;
                    return;
                }
            }
        }

        private void SplitSection()
        {
            if (errorExist || FindFreeDownloader() == (-1)) return;
            int biggestBeingDownloadedSection = (-1);
            long biggestDownloadingSectionSize = 0;
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
            if (biggestDownloadingSectionSize / 2 > minSectionSize)
            {
                lock (sectionsLock)
                {
                    DownloadSection newSection = download.Sections[biggestBeingDownloadedSection].Split();
                    if (newSection != null) download.Sections.Add(newSection);
                }
            }
        }

        private void ProcessSections()
        {
            SearchForError();
            SplitSection();

            for (int i = 0; i < download.Sections.Count; i++)
            {
                DownloadStatus ds = download.Sections[i].DownloadStatus;
                if (ds == DownloadStatus.Stopped || ds == DownloadStatus.DownloadError)
                {
                    AutoDownloadSection(download.Sections[i]);
                }
            }
        }

        public bool IsDownloadHalted()
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

        public bool IsDownloadFinished()
        {
            if (download.SummarySection.DownloadStatus == DownloadStatus.Finished) return true;
            return false;
        }

        public void CleanTempFiles()
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

        public void Stop()
        {
            this.downloadStopFlag = true;
        }
    }
}
