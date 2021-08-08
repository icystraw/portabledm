using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;

namespace partialdownloadgui.Components
{
    public class YoutubeVideo
    {
        public static YoutubeVideo ParseQuery(string query)
        {
            YoutubeVideo v = new();
            string[] queries = query.Split('/');
            if (queries.Length != 2) return null;
            string encodedUrl = queries[0];
            string escapedTitle = queries[1];
            v.Title = Uri.UnescapeDataString(escapedTitle);
            v.Url = Uri.UnescapeDataString(encodedUrl);

            v.Url = Regex.Replace(v.Url, @"[\?&](range|rn|rbuf)=[^&]+", string.Empty);
            NameValueCollection parameters = HttpUtility.ParseQueryString(new Uri(v.Url).Query);
            if (parameters.Get("dur") != null)
            {
                v.Duration = Util.getDurationFromParam(parameters.Get("dur"));
            }
            else v.Duration = string.Empty;
            if (parameters.Get("mime") != null)
            {
                v.Mime = parameters.Get("mime").Replace('/', '.');
            }
            else v.Mime = string.Empty;

            return v;
        }

        private string url;
        private string mime;
        private string duration;
        private string title;

        public string Url { get => url; set => url = value; }
        public string Mime { get => mime; set => mime = value; }
        public string Duration { get => duration; set => duration = value; }
        public string Title { get => title; set => title = value; }
    }
}
