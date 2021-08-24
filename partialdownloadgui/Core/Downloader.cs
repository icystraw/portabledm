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
                this.downloadSection = value ?? throw new ArgumentNullException(nameof(value));
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
            if (!Util.CheckDownloadSectionAgainstLogicalErrors(this.downloadSection)) return;
            if (this.downloadSection.DownloadStatus == DownloadStatus.DownloadError || this.downloadSection.DownloadStatus == DownloadStatus.LogicalError)
            {
                if (DateTime.Now.Subtract(this.downloadSection.LastStatusChange) < retryTimeSpan) return;
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

            HttpRequestMessage request = Util.ConstructHttpRequest(this.downloadSection.Url, this.downloadSection.Start + this.downloadSection.BytesDownloaded, this.downloadSection.End, this.downloadSection.UserName, this.downloadSection.Password);

            int bufferSize = 1048576;
            byte[] buffer = new byte[bufferSize];
            HttpResponseMessage response = null;
            Stream streamHttp = null;
            Stream streamFile = null;

            try
            {
                response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                this.downloadSection.HttpStatusCode = response.StatusCode;
                if (!Util.SyncDownloadSectionAgainstHTTPResponse(this.downloadSection, response))
                {
                    response.Dispose();
                    return;
                }
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
