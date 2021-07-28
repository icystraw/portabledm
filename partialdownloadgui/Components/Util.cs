using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace partialdownloadgui.Components
{
    public class Util
    {
        public static readonly string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\partialdownloadgui\\";

        public static string convertFromBase64(string base64String)
        {
            return Encoding.GetEncoding(28591).GetString(Convert.FromBase64String(base64String));
        }

        public static string getShortFileSize(long fileSize)
        {
            decimal size = fileSize;
            if (size <= 0) return "0";
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
            File.WriteAllText(appDataDirectory + "settings.json", jsonString);
        }

        public static void loadAppSettingsFromFile()
        {
            Directory.CreateDirectory(appDataDirectory);
            string jsonString = File.ReadAllText(appDataDirectory + "settings.json");
            App.AppSettings = JsonSerializer.Deserialize<ApplicationSettings>(jsonString);
        }

        public static void saveSectionsToFile(List<DownloadSection> sections, string fileName)
        {
            if (null == sections || sections.Count == 0) return;
            string jsonString = JsonSerializer.Serialize(sections);
            File.WriteAllText(fileName, jsonString);
        }

        public static List<DownloadSection> retriveSectionsFromFile(string fileName)
        {
            string jsonString = File.ReadAllText(fileName);
            List<DownloadSection> ret = JsonSerializer.Deserialize<List<DownloadSection>>(jsonString);
            if (null == ret) return null;
            // rebuild section chain
            for (int i = 0; i < ret.Count; i++)
            {
                for (int j = 0; j < ret.Count; j++)
                {
                    if (ret[j].Id == ret[i].NextSectionId)
                    {
                        ret[i].NextSection = ret[j];
                        break;
                    }
                }
            }
            return ret;
        }

        public static void downloadPreprocess(DownloadSection ds)
        {
            if (ds.Start < 0 && ds.End >= 0)
            {
                ds.DownloadStatus = DownloadStatus.ParameterError;
                return;
            }
            if (ds.Start >= 0 && ds.End >= 0 && ds.Start > ds.End)
            {
                ds.DownloadStatus = DownloadStatus.ParameterError;
                return;
            }
            if (string.IsNullOrEmpty(ds.Url))
            {
                ds.DownloadStatus = DownloadStatus.ParameterError;
                return;
            }
            ds.HttpStatusCode = 0;
            if (ds.Start < 0 && ds.End < 0)
            {
                ds.Start = 0;
            }
            HttpRequestMessage request = new(HttpMethod.Get, ds.Url);
            long? endParam = (ds.End >= 0 ? ds.End : null);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(ds.Start, endParam);
            if (!string.IsNullOrEmpty(Scheduler.Username) && !string.IsNullOrEmpty(Scheduler.Password))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(Scheduler.Username + ':' + Scheduler.Password)));
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
                        if (response.Content.Headers.ContentLocation != null)
                        {
                            request.RequestUri = response.Content.Headers.ContentLocation;
                            ds.Url = request.RequestUri.AbsoluteUri;
                            response = Downloader.Client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                            ds.HttpStatusCode = response.StatusCode;
                        }
                        else break;
                    }
                    else break;
                }
                if (ds.HttpStatusCode != HttpStatusCode.OK && ds.HttpStatusCode != HttpStatusCode.PartialContent)
                {
                    response.Dispose();
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
                        ds.DownloadStatus = DownloadStatus.DownloadError;
                        return;
                    }
                }
                response.Dispose();
                ds.DownloadStatus = DownloadStatus.Stopped;
            }
            catch
            {
                ds.DownloadStatus = DownloadStatus.DownloadError;
                throw;
            }
            finally
            {
                if (response != null) response.Dispose();
            }
        }
    }
}
