using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace partialdownloadgui.Components
{
    public class YoutubeWatchPageParser
    {
        private string watchPageFile;
        private string pageTitle;
        private string playerJsUrl;
        private string adaptiveFormats;
        private List<AdaptiveFormatsJson> videos;

        public string WatchPageFile { get => watchPageFile; set => watchPageFile = value; }
        public string PageTitle { get => pageTitle; set => pageTitle = value; }
        public string PlayerJsUrl { get => playerJsUrl; set => playerJsUrl = value; }
        public string AdaptiveFormats { get => adaptiveFormats; set => adaptiveFormats = value; }
        public List<AdaptiveFormatsJson> Videos { get => videos; set => videos = value; }

        public YoutubeWatchPageParser(string file)
        {
            if (string.IsNullOrEmpty(file)) throw new ArgumentNullException(nameof(file));
            this.watchPageFile = file;
            this.videos = new();
        }

        public void GetPageTitle()
        {
            if (string.IsNullOrEmpty(watchPageFile)) throw new ArgumentNullException(nameof(watchPageFile));
            string pattern = @"<title>(.*)</title>";
            Match m = Regex.Match(watchPageFile, pattern, RegexOptions.Singleline);
            if (m.Success)
            {
                this.pageTitle = Util.RemoveLineBreaks(m.Groups[1].Value);
                Debug.WriteLine(pageTitle);
            }
        }

        public void GetPlayerJsUrl()
        {
            if (string.IsNullOrEmpty(watchPageFile)) throw new ArgumentNullException(nameof(watchPageFile));
            string pattern = @"""PLAYER_JS_URL"":""(.+?)""";
            Match m = Regex.Match(watchPageFile, pattern, RegexOptions.Singleline);
            if (m.Success)
            {
                this.playerJsUrl = m.Groups[1].Value;
                Debug.WriteLine(playerJsUrl);
            }
        }

        public void GetAdaptiveFormats()
        {
            if (string.IsNullOrEmpty(watchPageFile)) throw new ArgumentNullException(nameof(watchPageFile));
            string pattern = @"""adaptiveFormats"":(\[.*?\])";
            Match m = Regex.Match(watchPageFile, pattern, RegexOptions.Singleline);
            if (m.Success)
            {
                this.adaptiveFormats = m.Groups[1].Value;
                Debug.WriteLine(adaptiveFormats);
            }
        }

        public void CreateVideosFromJson()
        {
            if (string.IsNullOrEmpty(adaptiveFormats)) throw new ArgumentNullException(nameof(adaptiveFormats));
            videos = JsonSerializer.Deserialize<List<AdaptiveFormatsJson>>(adaptiveFormats);
            if (videos.Count > 0) Debug.WriteLine(videos[0].signatureCipher ?? videos[0].url);
        }

        public void Parse()
        {
            GetPageTitle();
            GetPlayerJsUrl();
            GetAdaptiveFormats();
            CreateVideosFromJson();
        }
    }
}
