using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace partialdownloadgui.Components
{
    public class Util
    {
        public static readonly string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\partialdownloadgui\\";
        public static readonly string settingsFileName = "settings.json";
        public static readonly string downloadsFileName = "downloads.json";

        public static string convertFromBase64(string base64String)
        {
            try
            {
                return Encoding.GetEncoding(28591).GetString(Convert.FromBase64String(base64String));

            }
            catch
            {
                return string.Empty;
            }
        }

        public static decimal getProgress(long downloaded, long total)
        {
            if (total > 0)
            {
                decimal ret = Math.Round((decimal)downloaded * 100m / (decimal)total, 1, MidpointRounding.AwayFromZero);
                if (ret > 100m) ret = 100m;
                return ret;
            }
            return 0m;
        }

        public static string removeInvalidCharFromFileName(string fileName)
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

        public static string getShortFileSize(long fileSize)
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

        public static string getFileName(string url)
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

        public static void saveAppSettingsToFile()
        {
            string jsonString = JsonSerializer.Serialize(App.AppSettings);
            Directory.CreateDirectory(appDataDirectory);
            File.WriteAllText(appDataDirectory + settingsFileName, jsonString);
        }

        public static void loadAppSettingsFromFile()
        {
            Directory.CreateDirectory(appDataDirectory);
            string jsonString = File.ReadAllText(appDataDirectory + settingsFileName);
            App.AppSettings = JsonSerializer.Deserialize<ApplicationSettings>(jsonString);
        }

        public static void saveDownloadsToFile(List<Download> downloads)
        {
            if (null == downloads) return;
            string jsonString = JsonSerializer.Serialize(downloads);
            File.WriteAllText(appDataDirectory + downloadsFileName, jsonString);
        }

        public static List<Download> retrieveDownloadsFromFile()
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

        public static void downloadPreprocess(DownloadSection ds)
        {
            if (ds.Start < 0 && ds.End >= 0)
            {
                ds.DownloadStatus = DownloadStatus.LogicalErrorOrCancelled;
                return;
            }
            if (ds.Start >= 0 && ds.End >= 0 && ds.Start > ds.End)
            {
                ds.DownloadStatus = DownloadStatus.LogicalErrorOrCancelled;
                return;
            }
            if (string.IsNullOrEmpty(ds.Url))
            {
                ds.DownloadStatus = DownloadStatus.LogicalErrorOrCancelled;
                return;
            }
            ds.HttpStatusCode = 0;
            if (ds.Start < 0 && ds.End < 0)
            {
                ds.Start = 0;
            }
            HttpRequestMessage request = new(HttpMethod.Get, ds.Url);
            request.Headers.Referrer = request.RequestUri;
            long? endParam = (ds.End >= 0 ? ds.End : null);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(ds.Start, endParam);
            if (!string.IsNullOrEmpty(ds.UserName) && !string.IsNullOrEmpty(ds.Password))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(ds.UserName + ':' + ds.Password)));
            }
            HttpResponseMessage response = null;
            try
            {
                response = Downloader.Client.Send(request, HttpCompletionOption.ResponseHeadersRead);
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
                        request = new();
                        if (response.Headers.Location != null)
                        {
                            request.RequestUri = response.Headers.Location;
                        }
                        else if (response.Content.Headers.ContentLocation != null)
                        {
                            request.RequestUri = response.Content.Headers.ContentLocation;
                        }
                        else break;
                        request.Headers.Referrer = request.RequestUri;
                        request.Method = HttpMethod.Get;
                        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(ds.Start, endParam);
                        if (!string.IsNullOrEmpty(ds.UserName) && !string.IsNullOrEmpty(ds.Password))
                        {
                            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(ds.UserName + ':' + ds.Password)));
                        }
                        ds.Url = request.RequestUri.AbsoluteUri;
                        response = Downloader.Client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                        ds.HttpStatusCode = response.StatusCode;
                    }
                    else break;
                }
                if (ds.HttpStatusCode != HttpStatusCode.OK && ds.HttpStatusCode != HttpStatusCode.PartialContent)
                {
                    response.Dispose();
                    ds.Error = "HTTP status is not 200 or 206.";
                    ds.DownloadStatus = DownloadStatus.DownloadError;
                    return;
                }
                if (response.Content.Headers.ContentLength != null)
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        ds.Start = 0;
                        ds.End = (response.Content.Headers.ContentLength ?? 0) - 1;
                    }
                    else
                    {
                        ds.End = ds.Start + (response.Content.Headers.ContentLength ?? 0) - 1;
                    }
                }
                else
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        ds.Start = 0;
                        ds.End = (-1);
                    }
                    else
                    {
                        response.Dispose();
                        ds.Error = "HTTP ContentLength missing.";
                        ds.DownloadStatus = DownloadStatus.DownloadError;
                        return;
                    }
                }
                if (response.Content.Headers.ContentDisposition != null && !string.IsNullOrEmpty(response.Content.Headers.ContentDisposition.FileName))
                {
                    if (string.IsNullOrEmpty(ds.SuggestedName)) ds.SuggestedName = response.Content.Headers.ContentDisposition.FileName;
                }
                response.Dispose();
                ds.DownloadStatus = DownloadStatus.Stopped;
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

        public static string getDownloadFileNameFromDownloadSection(DownloadSection ds)
        {
            if (!string.IsNullOrEmpty(ds.SuggestedName))
            {
                return removeInvalidCharFromFileName(ds.SuggestedName);
            }
            else
            {
                return getFileName(ds.Url);
            }
        }
    }
}
