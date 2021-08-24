using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;

namespace partialdownloadgui.Components
{
    public class Video
    {
        private int _itag;
        private string _url;
        private int _bitrate;
        private int _width;
        private int _height;
        private string _signatureCipher;
        private string _mimeType;
        private string _contentLength;
        private string _qualityLabel;
        private string _audioQuality;
        private string _paramS;
        private string _paramSp;
        private string _signature;
        private string _duration;
        private string _title;

        public int itag { get => _itag; set => _itag = value; }
        public string url { get => _url; set => _url = value; }
        public string mimeType { get => _mimeType; set => _mimeType = value; }
        public string contentLength { get => _contentLength; set => _contentLength = value; }
        public string qualityLabel { get => _qualityLabel; set => _qualityLabel = value; }
        public string audioQuality { get => _audioQuality; set => _audioQuality = value; }
        public string paramS { get => _paramS; set => _paramS = value; }
        public string paramSp { get => _paramSp; set => _paramSp = value; }
        public string duration { get => _duration; set => _duration = value; }
        public string title { get => _title; set => _title = value; }
        public int bitrate { get => _bitrate; set => _bitrate = value; }
        public int width { get => _width; set => _width = value; }
        public int height { get => _height; set => _height = value; }
        public string signatureCipher
        {
            get => _signatureCipher;
            set
            {
                _signatureCipher = value;
                NameValueCollection parameters = HttpUtility.ParseQueryString(value);
                _url = parameters.Get("url");
                _paramS = parameters.Get("s");
                _paramSp = parameters.Get("sp");
            }
        }
        public string signature
        {
            get => _signature;
            set
            {
                _signature = value;
                // combine signature with url
                _url += "&" + _paramSp + "=" + Uri.EscapeDataString(_signature);
            }
        }

        public static Video ParseYoutubeEncodedUrlFromExtension(string url)
        {
            Video v = new();
            string[] queries = url.Split('/');
            if (queries.Length != 2) return null;
            string encodedUrl = queries[0];
            string escapedTitle = queries[1];
            v.title = Uri.UnescapeDataString(escapedTitle);
            v.url = Uri.UnescapeDataString(encodedUrl);

            v.url = Regex.Replace(v.url, @"[\?&](range|rn|rbuf)=[^&]+", string.Empty);
            NameValueCollection parameters = HttpUtility.ParseQueryString(new Uri(v.url).Query);
            if (parameters.Get("dur") != null)
            {
                v.duration = Util.CalculateDurationFromYoutubeUrlParam(parameters.Get("dur"));
            }
            else v.duration = string.Empty;
            if (parameters.Get("mime") != null)
            {
                v.mimeType = parameters.Get("mime");
            }
            else v.mimeType = string.Empty;

            return v;
        }

        // properties for bilibili
        private string _codecs;
        public int bandwidth { get => _bitrate; set => _bitrate = value; }
        public string baseUrl { get => _url; set => _url = value; }
        public int id { get => _itag; set => _itag = value; }
        public string codecs { get => _codecs; set => _codecs = value; }
    }
}
