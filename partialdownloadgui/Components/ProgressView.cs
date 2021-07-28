namespace partialdownloadgui.Components
{
    public class ProgressView
    {
        private string section;
        private string size;
        private long progress;
        private long total;
        private long bytesDownloaded;
        private string statusImage;

        public string Section { get => section; set => section = value; }
        public string Size { get => size; set => size = value; }
        public long Progress { get => progress; set => progress = value; }
        public long Total { get => total; set => total = value; }
        public long BytesDownloaded { get => bytesDownloaded; set => bytesDownloaded = value; }
        public string StatusImage { get => statusImage; set => statusImage = value; }
    }
}
