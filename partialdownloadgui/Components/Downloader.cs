using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace partialdownloadgui.Components
{
    public class Downloader
    {
        private static readonly string userAgentString = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.131 Safari/537.36";
        private static HttpClient client;

        private DownloadSection downloadSection;
        private Thread downloadThread;

        private bool downloadStopFlag = false;

        public DownloadSection DownloadSection
        {
            get => downloadSection;
            set
            {
                this.downloadSection = value ?? throw new ArgumentException("Argument cannot be null.", nameof(value));
            }
        }

        public static HttpClient Client { get => client; }

        static Downloader()
        {
            HttpClientHandler h = new();
            h.AllowAutoRedirect = false;
            h.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
            {
                return true;
            };
            client = new(h);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
            client.DefaultRequestHeaders.Add("user-agent", userAgentString);
        }

        public Downloader(DownloadSection section)
        {
            this.DownloadSection = section;
            ResetDownloadStatus();
        }

        public void StopDownloading()
        {
            if (!IsBusy()) return;
            this.downloadStopFlag = true;
            if (downloadThread != null && downloadThread.IsAlive) downloadThread.Join();
            this.downloadStopFlag = false;
        }

        public void WaitForFinish()
        {
            if (downloadThread != null && downloadThread.IsAlive) downloadThread.Join();
        }

        public bool IsBusy()
        {
            DownloadStatus ds = this.downloadSection.DownloadStatus;
            return (ds == DownloadStatus.PrepareToDownload || ds == DownloadStatus.Downloading);
        }

        public bool ChangeDownloadSection(DownloadSection newSection)
        {
            if (IsBusy()) return false;
            this.downloadSection = newSection;
            ResetDownloadStatus();
            return true;
        }

        public void StartDownloading()
        {
            this.downloadStopFlag = false;
            if (this.downloadSection.DownloadStatus == DownloadStatus.Finished) return;
            if (this.downloadSection.Start < 0 && this.downloadSection.End >= 0)
            {
                this.downloadSection.DownloadStatus = DownloadStatus.LogicalErrorOrCancelled;
                return;
            }
            if (this.downloadSection.Start >= 0 && this.downloadSection.End >= 0 && this.downloadSection.Start > this.downloadSection.End)
            {
                this.downloadSection.DownloadStatus = DownloadStatus.LogicalErrorOrCancelled;
                return;
            }
            if (string.IsNullOrEmpty(this.downloadSection.Url) || string.IsNullOrEmpty(this.downloadSection.FileName))
            {
                this.downloadSection.DownloadStatus = DownloadStatus.LogicalErrorOrCancelled;
                return;
            }

            this.downloadSection.DownloadStatus = DownloadStatus.PrepareToDownload;
            this.downloadSection.HttpStatusCode = 0;
            this.downloadSection.Error = string.Empty;
            if (this.downloadSection.Start < 0 && this.downloadSection.End < 0)
            {
                this.downloadSection.Start = 0;
            }
            downloadThread = new(new ThreadStart(DownloadThreadProc));
            downloadThread.Start();
        }

        private void ResetDownloadStatus()
        {
            if (this.downloadSection.DownloadStatus == DownloadStatus.PrepareToDownload || this.downloadSection.DownloadStatus == DownloadStatus.Downloading)
            {
                this.downloadSection.DownloadStatus = DownloadStatus.Stopped;
            }
        }

        private void DownloadThreadProc()
        {
            HttpRequestMessage request = new(HttpMethod.Get, this.downloadSection.Url);
            request.Headers.Referrer = request.RequestUri;
            long? endParam = (this.downloadSection.End >= 0 ? this.downloadSection.End : null);
            if (this.downloadSection.BytesDownloaded > 0)
            {
                if (!File.Exists(this.downloadSection.FileName) || (new FileInfo(this.downloadSection.FileName)).Length != this.downloadSection.BytesDownloaded)
                {
                    this.downloadSection.BytesDownloaded = 0;
                }
            }
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(this.downloadSection.Start + this.downloadSection.BytesDownloaded, endParam);
            if (!string.IsNullOrEmpty(this.downloadSection.UserName) && !string.IsNullOrEmpty(this.downloadSection.Password))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(this.downloadSection.UserName + ':' + this.downloadSection.Password)));
            }

            byte[] buffer = new byte[1048576];
            HttpResponseMessage response = null;
            Stream streamHttp = null;
            Stream streamFile = null;

            try
            {
                response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                this.downloadSection.HttpStatusCode = response.StatusCode;
                if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent)
                {
                    response.Dispose();
                    this.downloadSection.Error = "HTTP status is not 200 or 206. Check your link and try again.";
                    this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
                    return;
                }
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    this.downloadSection.BytesDownloaded = 0;
                }
                if (response.Content.Headers.ContentLength != null)
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        this.downloadSection.Start = 0;
                        this.downloadSection.End = (response.Content.Headers.ContentLength ?? 0) - 1;
                    }
                    else
                    {
                        this.downloadSection.End = this.downloadSection.Start + this.downloadSection.BytesDownloaded + (response.Content.Headers.ContentLength ?? 0) - 1;
                    }
                }
                else
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        this.downloadSection.Start = 0;
                        this.downloadSection.End = (-1);
                    }
                    else
                    {
                        response.Dispose();
                        this.downloadSection.Error = "HTTP ContentLength missing.";
                        this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
                        return;
                    }
                }
                if (response.Content.Headers.ContentDisposition != null && !string.IsNullOrEmpty(response.Content.Headers.ContentDisposition.FileName))
                {
                    if (string.IsNullOrEmpty(this.downloadSection.SuggestedName)) this.downloadSection.SuggestedName = response.Content.Headers.ContentDisposition.FileName;
                }
                if (response.Content.Headers.ContentType != null && response.Content.Headers.ContentType.MediaType != null)
                    this.downloadSection.ContentType = response.Content.Headers.ContentType.MediaType;
                if (this.downloadStopFlag)
                {
                    response.Dispose();
                    this.downloadSection.DownloadStatus = DownloadStatus.Stopped;
                    return;
                }

                streamHttp = response.Content.ReadAsStream();
                if (this.downloadSection.BytesDownloaded > 0)
                {
                    streamFile = File.Open(this.downloadSection.FileName, FileMode.Append, FileAccess.Write);
                }
                else
                {
                    streamFile = File.Open(this.downloadSection.FileName, FileMode.Create, FileAccess.Write);
                }
                this.downloadSection.DownloadStatus = DownloadStatus.Downloading;
                int bytesRead = streamHttp.Read(buffer, 0, 1048576);
                long currentEnd = this.downloadSection.End;
                while (bytesRead > 0)
                {
                    streamFile.Write(buffer, 0, bytesRead);
                    this.downloadSection.BytesDownloaded += bytesRead;
                    // End can be reduced by Scheduler thread.
                    currentEnd = this.downloadSection.End;
                    if (this.downloadStopFlag)
                    {
                        streamFile.Close();
                        streamHttp.Close();
                        response.Dispose();
                        if (currentEnd >= 0 && this.downloadSection.BytesDownloaded >= (currentEnd - this.downloadSection.Start + 1))
                        {
                            this.downloadSection.DownloadStatus = DownloadStatus.Finished;
                        }
                        else
                        {
                            this.downloadSection.DownloadStatus = DownloadStatus.Stopped;
                        }
                        return;
                    }
                    if (currentEnd >= 0 && this.downloadSection.BytesDownloaded >= (currentEnd - this.downloadSection.Start + 1)) break;
                    bytesRead = streamHttp.Read(buffer, 0, 1048576);
                }
                streamFile.Close();
                streamHttp.Close();
                response.Dispose();
                if (this.downloadSection.HttpStatusCode == HttpStatusCode.OK && currentEnd < 0)
                {
                    this.downloadSection.End = this.downloadSection.BytesDownloaded - 1;
                }
                this.downloadSection.DownloadStatus = DownloadStatus.Finished;
            }
            catch (Exception ex)
            {
                if (streamFile != null) streamFile.Close();
                if (streamHttp != null) streamHttp.Close();
                if (response != null) response.Dispose();
                this.downloadSection.Error = ex.Message;
                this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
            }
        }
    }
}
