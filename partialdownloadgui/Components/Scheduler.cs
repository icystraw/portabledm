using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace partialdownloadgui.Components
{
    public class Scheduler
    {
        private static readonly int maxNoDownloader = 10;
        private static readonly long minSectionSize = 10485760;
        private static readonly int bufferSize = 1048576;
        private string userName;
        private string password;

        private readonly Downloader[] downloaders = new Downloader[maxNoDownloader];
        private readonly List<DownloadSection> sections;
        private readonly object sectionsLock = new();

        private int noDownloader = 5;
        private bool errorExist = false;
        private bool downloadStopFlag = false;
        private SpeedCalculator sc = new();

        public string UserName { get => userName; set => userName = value; }

        public string Password { get => password; set => password = value; }

        public List<DownloadSection> Sections => sections;

        public int NoDownloader
        {
            get => noDownloader;
            set
            {
                if (value <= 0 || value > maxNoDownloader) return;
                noDownloader = value;
            }
        }

        public Scheduler(DownloadSection ds)
        {
            sections = new();
            sections.Add(ds);
        }

        public Scheduler(List<DownloadSection> sections)
        {
            if (null == sections || sections.Count == 0) throw new ArgumentNullException(nameof(sections));
            this.sections = sections;
        }

        public Scheduler(string savedFileName)
        {
            this.sections = Util.retriveSectionsFromFile(savedFileName);
            if (null == sections || sections.Count == 0) throw new ArgumentException("Saved file invalid or is empty", nameof(savedFileName));
        }

        private int FindFreeDownloader()
        {
            for (int i = 0; i < noDownloader; i++)
            {
                if (downloaders[i] == null || !downloaders[i].IsBusy()) return i;
            }
            return (-1);
        }

        private void StopDownload()
        {
            for (int i = 0; i < noDownloader; i++)
            {
                if (downloaders[i] != null)
                {
                    downloaders[i].StopDownloading();
                }
            }
        }

        private void StopDownloadExcept(DownloadSection ds)
        {
            for (int i = 0; i < noDownloader; i++)
            {
                if (downloaders[i] != null && downloaders[i].DownloadSection != ds)
                {
                    downloaders[i].StopDownloading();
                }
            }
        }

        private void CancelSectionsExcept(DownloadSection ds)
        {
            foreach (DownloadSection section in sections)
            {
                if (section != ds) section.DownloadStatus = DownloadStatus.ParameterError;
            }
        }

        private int FindDownloaderBySectionId(Guid id)
        {
            for (int i = 0; i < noDownloader; i++)
            {
                if (downloaders[i] != null && downloaders[i].DownloadSection.Id == id) return i;
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
            int downloaderIndex = FindDownloaderBySectionId(ds.Id);
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
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].DownloadStatus == DownloadStatus.DownloadError)
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
            for (int i = 0; i < sections.Count; i++)
            {
                DownloadStatus ds = sections[i].DownloadStatus;
                // if there is a downloading section with HTTP 200, don't make more sections and cancel other sections
                if (ds == DownloadStatus.Downloading && sections[i].HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    StopDownloadExcept(sections[i]);
                    CancelSectionsExcept(sections[i]);
                    return;
                }
                if (ds == DownloadStatus.Downloading && sections[i].HttpStatusCode == System.Net.HttpStatusCode.PartialContent)
                {
                    long bytesDownloaded = sections[i].BytesDownloaded;
                    if (bytesDownloaded > 0 && sections[i].Total - bytesDownloaded > biggestDownloadingSectionSize)
                    {
                        biggestDownloadingSectionSize = sections[i].Total - bytesDownloaded;
                        biggestBeingDownloadedSection = i;
                    }
                }
            }
            if (biggestBeingDownloadedSection < 0) return;
            if (biggestDownloadingSectionSize / 2 > minSectionSize)
            {
                lock (sectionsLock)
                {
                    DownloadSection newSection = sections[biggestBeingDownloadedSection].Split();
                    if (newSection != null) sections.Add(newSection);
                }
            }
        }

        private void ProcessSections()
        {
            SearchForError();
            SplitSection();

            for (int i = 0; i < sections.Count; i++)
            {
                DownloadStatus ds = sections[i].DownloadStatus;
                if (ds == DownloadStatus.Stopped || ds == DownloadStatus.DownloadError)
                {
                    AutoDownloadSection(sections[i]);
                }
            }
        }

        // this method will be called by a different thread
        public string GetDownloadStatus(out List<ProgressView> view)
        {
            List<ProgressView> progressViewItems = new();
            view = progressViewItems;
            lock (sectionsLock)
            {
                if (sections.Count == 0) return string.Empty;

                DownloadSection ds = sections[0];
                long total = 0;
                long totalDownloaded = 0;
                int sectionIndex = 1;
                do
                {
                    long secTotal = ds.Total;
                    long secDownloaded = ds.BytesDownloaded;
                    DownloadStatus status = ds.DownloadStatus;
                    int httpStatusCode = (int)ds.HttpStatusCode;
                    total += secTotal;
                    totalDownloaded += secDownloaded;
                    ProgressView pv = new();
                    pv.Total = secTotal;
                    pv.BytesDownloaded = secDownloaded;
                    if (status == DownloadStatus.Downloading || status == DownloadStatus.PrepareToDownload) pv.StatusImage = "downloading";
                    else if (status == DownloadStatus.DownloadError) pv.StatusImage = "error";
                    else if (status == DownloadStatus.Finished) pv.StatusImage = "finished";
                    else pv.StatusImage = string.Empty;
                    pv.Section = "Section " + sectionIndex.ToString() + "(" + httpStatusCode + ")";
                    sectionIndex++;
                    pv.Size = Util.getShortFileSize(secTotal);
                    if (secTotal > 0)
                    {
                        pv.Progress = (secDownloaded * 100 / secTotal > 100 ? 100 : secDownloaded * 100 / secTotal);
                    }
                    else
                    {
                        pv.Progress = 0;
                    }
                    progressViewItems.Add(pv);
                    ds = ds.NextSection;
                }
                while (ds != null);

                sc.RegisterBytes(totalDownloaded);
                ProgressView pvTotal = new();
                pvTotal.Total = total;
                pvTotal.BytesDownloaded = totalDownloaded;
                pvTotal.StatusImage = "downarrow";
                pvTotal.Section = "Overall " + Util.getShortFileSize(sc.GetSpeed()) + "/sec";
                pvTotal.Size = Util.getShortFileSize(total);
                if (total > 0)
                {
                    pvTotal.Progress = (totalDownloaded * 100 / total > 100 ? 100 : totalDownloaded * 100 / total);
                }
                else
                {
                    pvTotal.Progress = 0;
                }
                progressViewItems.Insert(0, pvTotal);

                if (total <= 0) return string.Empty;

                StringBuilder sb = new();
                for (int i = 1; i < progressViewItems.Count; i++)
                {
                    long downloadedSquares = progressViewItems[i].BytesDownloaded * 200 / total;
                    long pendingSquares = progressViewItems[i].Total * 200 / total - downloadedSquares;
                    for (long j = 0; j < downloadedSquares; j++)
                    {
                        sb.Append('\u2593');
                    }
                    for (long j = 0; j < pendingSquares; j++)
                    {
                        sb.Append('\u2591');
                    }
                }
                return sb.ToString();
            }
        }

        public bool IsDownloadHalted()
        {
            for (int i = 0; i < sections.Count; i++)
            {
                DownloadStatus ds = sections[i].DownloadStatus;
                if (ds == DownloadStatus.Stopped || ds == DownloadStatus.DownloadError ||
                    ds == DownloadStatus.PrepareToDownload || ds == DownloadStatus.Downloading)
                    return false;
            }
            return true;
        }

        // this method will be called by a different thread
        public void JoinSectionsToFile()
        {
            if (sections.Count == 0) return;
            // make sure the download cannot continue
            if (!IsDownloadHalted()) return;

            DownloadSection ds = sections[0];
            Stream streamDest = null, streamSection = null;
            byte[] buffer = new byte[bufferSize];
            string fileNameWithPath;
            try
            {
                if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder) && Directory.Exists(App.AppSettings.DownloadFolder))
                {
                    string fileNameOnly = Util.getFileName(ds.Url);
                    if (!string.IsNullOrEmpty(ds.SuggestedName))
                    {
                        fileNameOnly = Util.removeInvalidCharFromFileName(ds.SuggestedName);
                    }
                    fileNameWithPath = Path.Combine(App.AppSettings.DownloadFolder, fileNameOnly);
                    if (File.Exists(fileNameWithPath))
                    {
                        fileNameOnly = DateTime.Now.ToString("yyyy-MMM-dd-HH-mm-ss") + " " + fileNameOnly;
                        fileNameWithPath = Path.Combine(App.AppSettings.DownloadFolder, fileNameOnly);
                    }
                }
                else
                {
                    throw new NullReferenceException("Download folder is not present.");
                }
                streamDest = File.OpenWrite(fileNameWithPath);
                while (true)
                {
                    if (ds.DownloadStatus != DownloadStatus.Finished) continue;
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
                    if (ds.NextSection != null) ds = ds.NextSection;
                    else break;
                }
                streamDest.Close();
            }
            finally
            {
                if (streamDest != null) streamDest.Close();
                if (streamSection != null) streamSection.Close();
            }
        }

        public void CleanTempFiles()
        {
            try
            {
                foreach (DownloadSection ds in sections)
                {
                    if (File.Exists(ds.FileName)) File.Delete(ds.FileName);
                }
            }
            catch
            {
            }
        }

        // this method will be called by a different thread
        public void SaveDownloadProgressToFile(string fileName)
        {
            Util.saveSectionsToFile(this.sections, fileName);
        }

        // this method will be called by a different thread
        public void Stop()
        {
            this.downloadStopFlag = true;
        }

        public bool Start()
        {
            sc = new();
            this.downloadStopFlag = false;
            foreach (DownloadSection ds in this.sections)
            {
                ds.UserName = this.userName;
                ds.Password = this.password;
            }
            while (true)
            {
                // if there is download stop request from other thread
                if (this.downloadStopFlag)
                {
                    this.downloadStopFlag = false;
                    StopDownload();
                    return false;
                }
                ProcessSections();
                if (IsDownloadHalted()) break;
                Thread.Sleep(800);
            }
            return true;
        }
    }
}
