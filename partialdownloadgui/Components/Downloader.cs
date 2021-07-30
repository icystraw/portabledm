using System;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace partialdownloadgui.Components
{
    public class Downloader
    {
        private static HttpClient client;

        private DownloadSection downloadSection;
        private Thread downloadThread;

        private bool downloadStopFlag;
        private Exception exDownload;
        public Exception ExDownload { get => exDownload; }
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
        }

        public Downloader(DownloadSection section)
        {
            this.downloadStopFlag = false;
            this.DownloadSection = section;
        }

        public void StopDownloading()
        {
            if (!IsBusy()) return;
            this.downloadStopFlag = true;
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
            return true;
        }

        public void StartDownloading()
        {
            this.downloadStopFlag = false;
            if (this.downloadSection.DownloadStatus == DownloadStatus.Finished) return;
            if (this.downloadSection.Start < 0 && this.downloadSection.End >= 0)
            {
                this.downloadSection.DownloadStatus = DownloadStatus.ParameterError;
                return;
            }
            if (this.downloadSection.Start >= 0 && this.downloadSection.End >= 0 && this.downloadSection.Start > this.downloadSection.End)
            {
                this.downloadSection.DownloadStatus = DownloadStatus.ParameterError;
                return;
            }
            if (string.IsNullOrEmpty(this.downloadSection.Url) || string.IsNullOrEmpty(this.downloadSection.FileName))
            {
                this.downloadSection.DownloadStatus = DownloadStatus.ParameterError;
                return;
            }

            this.downloadSection.DownloadStatus = DownloadStatus.PrepareToDownload;
            this.downloadSection.HttpStatusCode = 0;
            this.exDownload = null;
            if (this.downloadSection.Start < 0 && this.downloadSection.End < 0)
            {
                this.downloadSection.Start = 0;
            }
            downloadThread = new(new ThreadStart(DownloadThreadProc));
            downloadThread.Start();
        }

        private void DownloadThreadProc()
        {
            HttpRequestMessage request = new(HttpMethod.Get, this.downloadSection.Url);

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
                        this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
                        return;
                    }
                }
                if (response.Content.Headers.ContentDisposition != null && !string.IsNullOrEmpty(response.Content.Headers.ContentDisposition.FileName))
                {
                    this.downloadSection.SuggestedName = response.Content.Headers.ContentDisposition.FileName;
                }
                if (this.downloadStopFlag)
                {
                    response.Dispose();
                    this.downloadStopFlag = false;
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
                while (bytesRead > 0)
                {
                    streamFile.Write(buffer, 0, bytesRead);
                    this.downloadSection.BytesDownloaded += bytesRead;
                    // End can be reduced by Scheduler thread. Keep a copy of current value.
                    long currentEnd = this.downloadSection.End;
                    if (this.downloadStopFlag)
                    {
                        streamFile.Close();
                        streamHttp.Close();
                        response.Dispose();
                        this.downloadStopFlag = false;
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
                this.downloadSection.DownloadStatus = DownloadStatus.Finished;
            }
            catch (Exception ex)
            {
                if (streamFile != null) streamFile.Close();
                if (streamHttp != null) streamHttp.Close();
                if (response != null) response.Dispose();
                this.exDownload = ex;
                this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
            }
        }
    }
}
