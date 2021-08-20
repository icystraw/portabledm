using System;
using System.Diagnostics;
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
        private static readonly TimeSpan retryTimeSpan = new(0, 0, 10);
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
            if (this.downloadSection.DownloadStatus == DownloadStatus.DownloadError || this.downloadSection.DownloadStatus == DownloadStatus.LogicalError)
            {
                if (DateTime.Now.Subtract(this.downloadSection.LastStatusChange) < retryTimeSpan) return;
            }
            if (this.downloadSection.Start < 0)
            {
                this.downloadSection.Error = "Download start position less than zero.";
                this.downloadSection.DownloadStatus = DownloadStatus.LogicalError;
                return;
            }
            if (this.downloadSection.End >= 0 && this.downloadSection.Start > this.downloadSection.End)
            {
                this.downloadSection.Error = "Download start position greater than end position.";
                this.downloadSection.DownloadStatus = DownloadStatus.LogicalError;
                return;
            }
            if (string.IsNullOrEmpty(this.downloadSection.Url) || string.IsNullOrEmpty(this.downloadSection.FileName))
            {
                this.downloadSection.Error = "Download URL missing.";
                this.downloadSection.DownloadStatus = DownloadStatus.LogicalError;
                return;
            }

            this.downloadSection.DownloadStatus = DownloadStatus.PrepareToDownload;
            this.downloadSection.HttpStatusCode = 0;
            this.downloadSection.Error = string.Empty;
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

        public static string SimpleDownloadToString(string url)
        {
            return Encoding.UTF8.GetString(SimpleDownloadToByteArray(url));
        }

        public static byte[] SimpleDownloadToByteArray(string url)
        {
            HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Referrer = request.RequestUri;
            request.Headers.Add("accept-encoding", "identity");

            HttpResponseMessage response = null;
            Stream streamHttp = null;
            MemoryStream ms = new();
            try
            {
                response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                Debug.WriteLine(response.Headers.ToString());
                Debug.WriteLine(response.Content.Headers.ToString());
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    response.Dispose();
                    return new byte[0];
                }
                streamHttp = response.Content.ReadAsStream();
                streamHttp.CopyTo(ms);
                return ms.ToArray();
            }
            catch
            {
                return new byte[0];
            }
            finally
            {
                if (streamHttp != null) streamHttp.Close();
                if (ms != null) ms.Close();
                if (response != null) response.Dispose();
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
            if (this.downloadSection.End >= 0 && this.downloadSection.BytesDownloaded >= this.downloadSection.Total)
            {
                this.downloadSection.DownloadStatus = DownloadStatus.Finished;
                return;
            }
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(this.downloadSection.Start + this.downloadSection.BytesDownloaded, endParam);
            if (!string.IsNullOrEmpty(this.downloadSection.UserName) && !string.IsNullOrEmpty(this.downloadSection.Password))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(this.downloadSection.UserName + ':' + this.downloadSection.Password)));
            }

            int bufferSize = 1048576;
            byte[] buffer = new byte[bufferSize];
            HttpResponseMessage response = null;
            Stream streamHttp = null;
            Stream streamFile = null;
            System.Net.Http.Headers.HttpContentHeaders headers = null;

            try
            {
                response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                this.downloadSection.HttpStatusCode = response.StatusCode;
                headers = response.Content.Headers;
                if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent)
                {
                    response.Dispose();
                    this.downloadSection.Error = "HTTP status is not 200 or 206. Maybe try again later.";
                    this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
                    return;
                }
                if (this.downloadSection.LastModified != DateTimeOffset.MaxValue && headers.LastModified != null)
                {
                    if (this.downloadSection.LastModified != headers.LastModified)
                    {
                        response.Dispose();
                        this.downloadSection.Error = "Content changed since last time you download it. Please re-download this file.";
                        this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
                        return;
                    }
                }
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (this.downloadSection.Start > 0)
                    {
                        response.Dispose();
                        this.downloadSection.Error = "Server does not support resuming, however requested section is not from the beginning of file.";
                        this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
                        return;
                    }
                    if (headers.ContentLength != null)
                    {
                        long contentLength = (headers.ContentLength ?? 0);
                        if (this.downloadSection.End < 0) this.downloadSection.End = contentLength - 1;
                        else if (contentLength < this.downloadSection.Total)
                        {
                            response.Dispose();
                            this.downloadSection.Error = "Content length returned from server is smaller than the section requested.";
                            this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
                            return;
                        }
                    }
                    this.downloadSection.BytesDownloaded = 0;
                }
                if (response.StatusCode == HttpStatusCode.PartialContent)
                {
                    if (headers.ContentLength == null)
                    {
                        response.Dispose();
                        this.downloadSection.Error = "HTTP ContentLength missing.";
                        this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
                        return;
                    }
                    long contentLength = (headers.ContentLength ?? 0);
                    if (this.downloadSection.End >= 0 && this.downloadSection.Start + this.downloadSection.BytesDownloaded + contentLength - 1 != this.downloadSection.End)
                    {
                        response.Dispose();
                        this.downloadSection.Error = "Content length from server does not match download section.";
                        this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
                        return;
                    }
                    // if it is a new download and all goes well
                    if (this.downloadSection.End < 0)
                    {
                        this.downloadSection.End = this.downloadSection.Start + contentLength - 1;
                    }
                }
                if (headers.ContentDisposition != null && !string.IsNullOrEmpty(headers.ContentDisposition.FileName))
                {
                    if (string.IsNullOrEmpty(this.downloadSection.SuggestedName)) this.downloadSection.SuggestedName = headers.ContentDisposition.FileName;
                }
                if (headers.ContentType != null && headers.ContentType.MediaType != null)
                    this.downloadSection.ContentType = headers.ContentType.MediaType;
                if (headers.LastModified != null)
                    this.downloadSection.LastModified = headers.LastModified ?? DateTimeOffset.MaxValue;
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
                int bytesRead = streamHttp.Read(buffer, 0, bufferSize);
                long currentEnd = this.downloadSection.End;
                while (bytesRead > 0)
                {
                    streamFile.Write(buffer, 0, bytesRead);
                    this.downloadSection.BytesDownloaded += bytesRead;
                    // End can be reduced by Scheduler thread.
                    currentEnd = this.downloadSection.End;
                    if (currentEnd >= 0 && this.downloadSection.BytesDownloaded >= (currentEnd - this.downloadSection.Start + 1)) break;
                    if (this.downloadStopFlag)
                    {
                        streamFile.Close();
                        streamHttp.Close();
                        response.Dispose();
                        this.downloadSection.DownloadStatus = DownloadStatus.Stopped;
                        return;
                    }
                    bytesRead = streamHttp.Read(buffer, 0, bufferSize);
                }
                streamFile.Close();
                streamHttp.Close();
                response.Dispose();
                currentEnd = this.downloadSection.End;
                if (currentEnd >= 0 && this.downloadSection.BytesDownloaded < (currentEnd - this.downloadSection.Start + 1))
                {
                    this.downloadSection.DownloadStatus = DownloadStatus.DownloadError;
                    this.downloadSection.Error = "Download stream reached the end, but not enough data transmitted.";
                    return;
                }
                if (this.downloadSection.HttpStatusCode == HttpStatusCode.OK && this.downloadSection.End < 0)
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
