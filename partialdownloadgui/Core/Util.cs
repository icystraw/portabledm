using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;

namespace partialdownloadgui.Components
{
    public class Util
    {
        public static readonly string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\partialdownloadgui\\";
        public static readonly string settingsFileName = "settings.json";
        public static readonly string downloadsFileName = "downloads.json";

        public static decimal CalculateProgress(long downloaded, long total)
        {
            if (total > 0)
            {
                decimal ret = Math.Round((decimal)downloaded * 100m / (decimal)total, 1, MidpointRounding.AwayFromZero);
                if (ret > 100m) ret = 100m;
                return ret;
            }
            return 0m;
        }

        public static string RemoveInvalidCharsFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return string.Empty;
            StringBuilder sb = new();
            for (int i = 0; i < fileName.Length; i++)
            {
                if (fileName[i] == '\\' || fileName[i] == '/' ||
                    fileName[i] == ':' || fileName[i] == '*' ||
                    fileName[i] == '?' || fileName[i] == '"' ||
                    fileName[i] == '<' || fileName[i] == '>' ||
                    fileName[i] == '|')
                {
                    continue;
                }
                sb.Append(fileName[i]);
            }
            if (sb.Length == 0) return "download.bin";
            return sb.ToString();
        }

        public static string GetEasyToUnderstandFileSize(long fileSize)
        {
            decimal size = fileSize;
            if (size <= 0) return "0B";
            if (size > 1073741824)
            {
                size = size / 1073741824m;
                return size.ToString("0.00") + "GB";
            }
            else if (size > 1048576)
            {
                size = size / 1048576m;
                return size.ToString("0.00") + "MB";
            }
            else if (size > 1024)
            {
                size = size / 1024m;
                return size.ToString("0.00") + "KB";
            }
            else
            {
                return size.ToString() + " bytes";
            }
        }

        public static string GetFileNameFromUrl(string url)
        {
            try
            {
                Uri uri = new(url);
                string fileName = Path.GetFileName(uri.AbsolutePath);
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = "download.bin";
                }
                return fileName;
            }
            catch
            {
                return "download.bin";
            }
        }

        public static void SaveAppSettingsToFile()
        {
            string jsonString = JsonSerializer.Serialize(App.AppSettings);
            Directory.CreateDirectory(appDataDirectory);
            File.WriteAllText(appDataDirectory + settingsFileName, jsonString);
        }

        public static void LoadAppSettingsFromFile()
        {
            Directory.CreateDirectory(appDataDirectory);
            string jsonString = File.ReadAllText(appDataDirectory + settingsFileName);
            App.AppSettings = JsonSerializer.Deserialize<ApplicationSettings>(jsonString);
        }

        public static void SaveDownloadsToFile(List<Download> downloads)
        {
            if (null == downloads) return;
            string jsonString = JsonSerializer.Serialize(downloads);
            File.WriteAllText(appDataDirectory + downloadsFileName, jsonString);
        }

        public static List<Download> RetrieveDownloadsFromFile()
        {
            string jsonString = File.ReadAllText(appDataDirectory + downloadsFileName);
            List<Download> ret = JsonSerializer.Deserialize<List<Download>>(jsonString);
            if (null == ret) return null;
            // rebuild section chain
            foreach (Download d in ret)
            {
                for (int i = 0; i < d.Sections.Count; i++)
                {
                    for (int j = 0; j < d.Sections.Count; j++)
                    {
                        if (d.Sections[j].Id == d.Sections[i].NextSectionId)
                        {
                            d.Sections[i].NextSection = d.Sections[j];
                            break;
                        }
                    }
                }
            }
            return ret;
        }

        public static HttpRequestMessage ConstructHttpRequest(string url, long start, long end, string userName, string password)
        {
            HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Referrer = request.RequestUri;
            long? endParam = (end >= 0 ? end : null);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, endParam);
            if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(userName + ':' + password)));
            }
            return request;
        }

        public static bool CheckDownloadSectionAgainstLogicalErrors(DownloadSection ds)
        {
            if (ds.Start < 0)
            {
                ds.Error = "Download start position less than zero.";
                ds.DownloadStatus = DownloadStatus.LogicalError;
                return false;
            }
            if (ds.End >= 0 && ds.Start > ds.End)
            {
                ds.Error = "Download start position greater than end position.";
                ds.DownloadStatus = DownloadStatus.LogicalError;
                return false;
            }
            if (string.IsNullOrEmpty(ds.Url) || string.IsNullOrEmpty(ds.FileName))
            {
                ds.Error = "Download URL or target file name missing.";
                ds.DownloadStatus = DownloadStatus.LogicalError;
                return false;
            }
            return true;
        }

        public static bool SyncDownloadSectionAgainstHTTPResponse(DownloadSection ds, HttpResponseMessage response)
        {
            System.Net.Http.Headers.HttpContentHeaders headers = response.Content.Headers;

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent)
            {
                ds.Error = "HTTP request not successful. Maybe try again later. Status: " + response.StatusCode;
                ds.DownloadStatus = DownloadStatus.DownloadError;
                return false;
            }
            if (ds.LastModified != DateTimeOffset.MaxValue && headers.LastModified != null)
            {
                if (ds.LastModified != headers.LastModified)
                {
                    ds.Error = "Content changed since last time you download it. Please re-download this file.";
                    ds.DownloadStatus = DownloadStatus.DownloadError;
                    return false;
                }
            }
            if (response.StatusCode == HttpStatusCode.OK)
            {
                // if requested section is not from the beginning and server does not support resuming
                if (ds.Start > 0)
                {
                    ds.Error = "Server does not support resuming, however requested section is not from the beginning of file.";
                    ds.DownloadStatus = DownloadStatus.DownloadError;
                    return false;
                }
                if (headers.ContentLength != null)
                {
                    long contentLength = (headers.ContentLength ?? 0);
                    if (ds.End < 0) ds.End = contentLength - 1;
                    else if (contentLength < ds.Total)
                    {
                        ds.Error = "Content length returned from server is smaller than the section requested. Content length: " + contentLength;
                        ds.DownloadStatus = DownloadStatus.DownloadError;
                        return false;
                    }
                }
                ds.BytesDownloaded = 0;
            }
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                if (headers.ContentLength == null)
                {
                    ds.Error = "HTTP Content-Length missing.";
                    ds.DownloadStatus = DownloadStatus.DownloadError;
                    return false;
                }
                long contentLength = (headers.ContentLength ?? 0);
                if (ds.End >= 0 && ds.Start + ds.BytesDownloaded + contentLength - 1 != ds.End)
                {
                    ds.Error = "Content length from server does not match download section.";
                    ds.DownloadStatus = DownloadStatus.DownloadError;
                    return false;
                }
                // if it is a new download and all goes well
                if (ds.End < 0)
                {
                    ds.End = ds.Start + contentLength - 1;
                }
            }
            if (string.IsNullOrEmpty(ds.SuggestedName)) ds.SuggestedName = headers.ContentDisposition?.FileName;
            ds.ContentType = headers.ContentType?.MediaType;
            ds.LastModified = headers.LastModified ?? DateTimeOffset.MaxValue;

            return true;
        }

        public static void DownloadPreprocess(DownloadSection ds)
        {
            if (!CheckDownloadSectionAgainstLogicalErrors(ds)) return;
            ds.HttpStatusCode = 0;
            ds.Error = string.Empty;
            HttpRequestMessage request = ConstructHttpRequest(ds.Url, ds.Start, ds.End, ds.UserName, ds.Password);
            HttpResponseMessage response = null;
            try
            {
                response = Downloader.Client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                Debug.WriteLine(response.Headers.ToString());
                Debug.WriteLine(response.Content.Headers.ToString());
                ds.HttpStatusCode = response.StatusCode;
                // handle http redirection up to 5 times
                for (int retry = 0; retry < 5; retry++)
                {
                    if (ds.HttpStatusCode == HttpStatusCode.OK || ds.HttpStatusCode == HttpStatusCode.PartialContent) break;
                    else if (ds.HttpStatusCode == HttpStatusCode.MovedPermanently ||
                        ds.HttpStatusCode == HttpStatusCode.Found ||
                        ds.HttpStatusCode == HttpStatusCode.TemporaryRedirect ||
                        ds.HttpStatusCode == HttpStatusCode.PermanentRedirect)
                    {
                        Uri uri;
                        if (response.Headers.Location != null)
                        {
                            uri = response.Headers.Location;
                        }
                        else if (response.Content.Headers.ContentLocation != null)
                        {
                            uri = response.Content.Headers.ContentLocation;
                        }
                        else break;
                        ds.Url = uri.AbsoluteUri;
                        request = ConstructHttpRequest(ds.Url, ds.Start, ds.End, ds.UserName, ds.Password);
                        response = Downloader.Client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                        ds.HttpStatusCode = response.StatusCode;
                    }
                    else break;
                }
                if (SyncDownloadSectionAgainstHTTPResponse(ds, response))
                {
                    ds.DownloadStatus = DownloadStatus.Stopped;
                }
                response.Dispose();
            }
            catch (Exception ex)
            {
                ds.Error = ex.Message;
                ds.DownloadStatus = DownloadStatus.DownloadError;
                throw;
            }
            finally
            {
                if (response != null) response.Dispose();
            }
        }

        public static string CalculateEta(long remaining, long speed)
        {
            if (remaining <= 0 || speed == 0) return string.Empty;
            long seconds = remaining / speed;

            StringBuilder sb = new();
            sb.Append("ETA ");

            if (seconds > 3600)
            {
                sb.Append(seconds / 3600);
                sb.Append('h');
                seconds %= 3600;
            }
            if (seconds > 60)
            {
                sb.Append(seconds / 60);
                sb.Append('m');
                seconds %= 60;
            }
            if (seconds > 0)
            {
                sb.Append(seconds);
                sb.Append('s');
            }
            return sb.ToString();
        }

        public static string GetDownloadFileNameFromDownloadSection(DownloadSection ds)
        {
            if (!string.IsNullOrEmpty(ds.SuggestedName))
            {
                return RemoveInvalidCharsFromFileName(ds.SuggestedName);
            }
            else
            {
                return GetFileNameFromUrl(ds.Url);
            }
        }

        public static string CalculateDurationFromYoutubeUrlParam(string dur)
        {
            int seconds = 0;
            try
            {
                seconds = Convert.ToInt32(decimal.Parse(dur));
            }
            catch { }
            StringBuilder sb = new();
            sb.Append(seconds / 60);
            sb.Append("min");
            sb.Append(seconds % 60);
            sb.Append("sec");
            return sb.ToString();
        }

        public static string RemoveLineBreaks(string s)
        {
            return s.Replace("\n", string.Empty).Replace("\r", string.Empty);
        }

        public static string RemoveSpaces(string s)
        {
            return s.Replace(" ", string.Empty);
        }

        public static string GZipDecompress(byte[] file)
        {
            using (MemoryStream ms = new(file))
            {
                using (MemoryStream ms2 = new())
                {
                    using (GZipStream gs = new(ms, CompressionMode.Decompress))
                    {
                        gs.CopyTo(ms2);
                        return Encoding.UTF8.GetString(ms2.ToArray());
                    }
                }
            }
        }

        public static void BrowseForDownloadedFiles(Button btnBrowse)
        {
            System.Windows.Forms.FolderBrowserDialog dlg = new();
            if (!string.IsNullOrEmpty(App.AppSettings.DownloadFolder))
            {
                if (Directory.Exists(App.AppSettings.DownloadFolder)) dlg.SelectedPath = App.AppSettings.DownloadFolder;
            }
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                btnBrowse.Content = dlg.SelectedPath;
                App.AppSettings.DownloadFolder = dlg.SelectedPath;
            }
        }
    }
}
