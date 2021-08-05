using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace partialdownloadgui.Components
{
    public class YoutubeUtil
    {
        public static DownloadSection DownloadYoutubePage(string url)
        {
            DownloadSection ds = new();
            ds.Url = url;
            ds.Start = 0;
            ds.End = (-1);
            Downloader downloader = new(ds);
            downloader.StartDownloading();
            downloader.WaitForFinish();

            return ds;
        }

        public static string GetTitleFromJson(DownloadSection ds)
        {
            string f = File.ReadAllText(ds.FileName);
            int titleIndex = f.IndexOf("<title>");
            int titleEndIndex = (-1);
            if (titleIndex >= 0) titleEndIndex = f.IndexOf("</title>", titleIndex);

            if (titleEndIndex > titleIndex && titleIndex >= 0)
                return f.Substring(titleIndex + 7, titleEndIndex - 7 - titleIndex);
            else
                return string.Empty;
        }

        public static string GetVideoInfoJson(DownloadSection ds)
        {
            string f = File.ReadAllText(ds.FileName);

            int formatsIndex = f.IndexOf("\"formats\":");
            if (formatsIndex == (-1)) return string.Empty;
            int formatIndexEnd = (-1);
            for (int i = formatsIndex; i < f.Length; i++)
            {
                if (f[i] == '[')
                {
                    formatsIndex = i;
                }
                else if (f[i] == ']')
                {
                    formatIndexEnd = i;
                    break;
                }
            }
            if (formatIndexEnd == (-1)) return string.Empty;

            int adaptiveFormatsIndex = f.IndexOf("\"adaptiveFormats\":", formatIndexEnd + 1);
            int adaptiveFormatsIndexEnd = (-1);
            if (adaptiveFormatsIndex != (-1))
            {
                for (int i = adaptiveFormatsIndex; i < f.Length; i++)
                {
                    if (f[i] == '[')
                    {
                        adaptiveFormatsIndex = i;
                    }
                    else if (f[i] == ']')
                    {
                        adaptiveFormatsIndexEnd = i;
                        break;
                    }
                }
            }
            StringBuilder sb = new();
            sb.Append(f.Substring(formatsIndex, formatIndexEnd - formatsIndex));
            if (adaptiveFormatsIndex != (-1) && adaptiveFormatsIndexEnd != (-1))
            {
                sb.Append(", ");
                sb.Append(f.Substring(adaptiveFormatsIndex + 1, adaptiveFormatsIndexEnd - adaptiveFormatsIndex));
            }
            else
            {
                sb.Append("]");
            }
            return sb.ToString();
        }

        public static List<YoutubeVideo> GetVideoObjectsFromJson(string json)
        {
            return JsonSerializer.Deserialize<List<YoutubeVideo>>(json);
        }
    }
}
