namespace partialdownloadgui.Components
{
    public class SectionView
    {
        private string description;
        private string size;
        private long progress;
        private long total;
        private long bytesDownloaded;
        private string status;

        public string Description { get => description; set => description = value; }
        public string Size { get => size; set => size = value; }
        public long Progress { get => progress; set => progress = value; }
        public long Total { get => total; set => total = value; }
        public long BytesDownloaded { get => bytesDownloaded; set => bytesDownloaded = value; }
        public string Status { get => status; set => status = value; }
    }
}
