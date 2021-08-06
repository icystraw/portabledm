namespace partialdownloadgui.Components
{
    public class YoutubeVideo
    {
        private string mimeType;
        private long contentLength;
        private string url;

        public string MimeType { get => mimeType; set => mimeType = value; }
        public long ContentLength { get => contentLength; set => contentLength = value; }
        public string Url { get => url; set => url = value; }
    }
}
