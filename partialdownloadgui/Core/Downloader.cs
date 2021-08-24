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
        private static readonly HttpClient client;

        private DownloadSection downloadSection;
        private Thread downloadThread;

        private bool downloadStopFlag = false;

        public DownloadSection DownloadSection
        {
            get => downloadSection;
            set
            {
                downloadSection = value ?? throw new ArgumentNullException(nameof(value));
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
            DownloadSection = section;
            ResetDownloadStatus();
        }

        public void StopDownloading()
        {
            if (!IsBusy()) return;
            downloadStopFlag = true;
            if (downloadThread != null && downloadThread.IsAlive) downloadThread.Join();
            downloadStopFlag = false;
        }

        public void WaitForFinish()
        {
            if (downloadThread != null && downloadThread.IsAlive) downloadThread.Join();
        }

        public bool IsBusy()
        {
            DownloadStatus ds = downloadSection.DownloadStatus;
            return (ds == DownloadStatus.PrepareToDownload || ds == DownloadStatus.Downloading);
        }

        public bool ChangeDownloadSection(DownloadSection newSection)
        {
            if (IsBusy()) return false;
            downloadSection = newSection;
            ResetDownloadStatus();
            return true;
        }

        public void StartDownloading()
        {
            if (IsBusy()) return;
            downloadStopFlag = false;
            if (downloadSection.DownloadStatus == DownloadStatus.Finished) return;
            if (!Util.CheckDownloadSectionAgainstLogicalErrors(downloadSection)) return;
            if (downloadSection.DownloadStatus == DownloadStatus.DownloadError)
            {
                if (DateTime.Now.Subtract(downloadSection.LastStatusChange) < retryTimeSpan) return;
            }

            downloadSection.DownloadStatus = DownloadStatus.PrepareToDownload;
            downloadSection.HttpStatusCode = 0;
            downloadSection.Error = string.Empty;
            downloadThread = new(new ThreadStart(DownloadThreadProc));
            downloadThread.Start();
        }

        private void ResetDownloadStatus()
        {
            if (downloadSection.DownloadStatus == DownloadStatus.PrepareToDownload || downloadSection.DownloadStatus == DownloadStatus.Downloading)
            {
                downloadSection.DownloadStatus = DownloadStatus.Stopped;
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
            if (downloadSection.BytesDownloaded > 0)
            {
                if (!File.Exists(downloadSection.FileName) || (new FileInfo(downloadSection.FileName)).Length != downloadSection.BytesDownloaded)
                {
                    downloadSection.BytesDownloaded = 0;
                }
            }
            if (downloadSection.End >= 0 && downloadSection.BytesDownloaded >= downloadSection.Total)
            {
                downloadSection.DownloadStatus = DownloadStatus.Finished;
                return;
            }

            HttpRequestMessage request = Util.ConstructHttpRequest(downloadSection.Url, downloadSection.Start + downloadSection.BytesDownloaded, downloadSection.End, downloadSection.UserName, downloadSection.Password);

            int bufferSize = 1048576;
            byte[] buffer = new byte[bufferSize];
            HttpResponseMessage response = null;
            Stream streamHttp = null;
            Stream streamFile = null;

            try
            {
                response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                downloadSection.HttpStatusCode = response.StatusCode;
                if (!Util.SyncDownloadSectionAgainstHTTPResponse(downloadSection, response))
                {
                    response.Dispose();
                    return;
                }
                if (downloadStopFlag)
                {
                    response.Dispose();
                    downloadSection.DownloadStatus = DownloadStatus.Stopped;
                    return;
                }

                streamHttp = response.Content.ReadAsStream();
                if (downloadSection.BytesDownloaded > 0)
                {
                    streamFile = File.Open(downloadSection.FileName, FileMode.Append, FileAccess.Write);
                }
                else
                {
                    streamFile = File.Open(downloadSection.FileName, FileMode.Create, FileAccess.Write);
                }
                downloadSection.DownloadStatus = DownloadStatus.Downloading;
                int bytesRead = streamHttp.Read(buffer, 0, bufferSize);
                long currentEnd = downloadSection.End;
                while (bytesRead > 0)
                {
                    streamFile.Write(buffer, 0, bytesRead);
                    downloadSection.BytesDownloaded += bytesRead;
                    // End can be reduced by Scheduler thread.
                    currentEnd = downloadSection.End;
                    if (currentEnd >= 0 && downloadSection.BytesDownloaded >= (currentEnd - downloadSection.Start + 1)) break;
                    if (downloadStopFlag)
                    {
                        streamFile.Close();
                        streamHttp.Close();
                        response.Dispose();
                        downloadSection.DownloadStatus = DownloadStatus.Stopped;
                        return;
                    }
                    bytesRead = streamHttp.Read(buffer, 0, bufferSize);
                }
                streamFile.Close();
                streamHttp.Close();
                response.Dispose();
                currentEnd = downloadSection.End;
                if (currentEnd >= 0 && downloadSection.BytesDownloaded < (currentEnd - downloadSection.Start + 1))
                {
                    downloadSection.DownloadStatus = DownloadStatus.DownloadError;
                    downloadSection.Error = "Download stream reached the end, but not enough data transmitted.";
                    return;
                }
                if (downloadSection.HttpStatusCode == HttpStatusCode.OK && downloadSection.End < 0)
                {
                    downloadSection.End = downloadSection.BytesDownloaded - 1;
                }
                downloadSection.DownloadStatus = DownloadStatus.Finished;
            }
            catch (Exception ex)
            {
                if (streamFile != null) streamFile.Close();
                if (streamHttp != null) streamHttp.Close();
                if (response != null) response.Dispose();
                downloadSection.Error = ex.Message;
                downloadSection.DownloadStatus = DownloadStatus.DownloadError;
            }
        }
    }
}
