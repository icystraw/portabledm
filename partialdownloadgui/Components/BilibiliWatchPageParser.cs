using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace partialdownloadgui.Components
{
    public class BilibiliWatchPageParser
    {
        private string watchPageFile;
        private string pageTitle;
        private string audioFormats;
        private string videoFormats;
        private List<Video> videos;
        private List<Video> audios;

        public string WatchPageFile { get => watchPageFile; set => watchPageFile = value; }
        public string PageTitle { get => pageTitle; set => pageTitle = value; }
        public List<Video> Videos { get => videos; set => videos = value; }
        public string AudioFormats { get => audioFormats; set => audioFormats = value; }
        public string VideoFormats { get => videoFormats; set => videoFormats = value; }
        public List<Video> Audios { get => audios; set => audios = value; }

        public BilibiliWatchPageParser(string file)
        {
            if (string.IsNullOrEmpty(file)) throw new ArgumentNullException(nameof(file));
            this.watchPageFile = file;
            this.videos = new();
            this.audios = new();
        }

        public void GetPageTitle()
        {
            if (string.IsNullOrEmpty(watchPageFile)) throw new ArgumentNullException(nameof(watchPageFile));
            string pattern = @"<title.*?>(.*)</title>";
            Match m = Regex.Match(watchPageFile, pattern, RegexOptions.Singleline);
            if (m.Success)
            {
                this.pageTitle = Util.RemoveLineBreaks(m.Groups[1].Value);
                Debug.WriteLine(pageTitle);
            }
        }

        public void GetVideoAudioFormats()
        {
            if (string.IsNullOrEmpty(watchPageFile)) throw new ArgumentNullException(nameof(watchPageFile));
            string pattern = @"""video"":(\[.*?\]),""audio"":(\[.*?\])";
            Match m = Regex.Match(watchPageFile, pattern, RegexOptions.Singleline);
            if (m.Success)
            {
                this.videoFormats = m.Groups[1].Value;
                this.audioFormats = m.Groups[2].Value;
                Debug.WriteLine(videoFormats);
                Debug.WriteLine(audioFormats);
            }
        }

        public void CreateVideosFromJson()
        {
            if (string.IsNullOrEmpty(videoFormats)) throw new ArgumentNullException(nameof(videoFormats));
            if (string.IsNullOrEmpty(audioFormats)) throw new ArgumentNullException(nameof(audioFormats));
            videos = JsonSerializer.Deserialize<List<Video>>(videoFormats);
            audios = JsonSerializer.Deserialize<List<Video>>(audioFormats);
            if (videos.Count > 0) Debug.WriteLine(videos[0].baseUrl);
            if (audios.Count > 0) Debug.WriteLine(audios[0].baseUrl);
        }

        public void Parse()
        {
            GetPageTitle();
            GetVideoAudioFormats();
            CreateVideosFromJson();
        }
    }
}
