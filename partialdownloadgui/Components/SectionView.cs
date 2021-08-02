namespace partialdownloadgui.Components
{
    public class SectionView
    {
        private string description;
        private string size;
        private long progress;
        private DownloadStatus status;

        public string Description { get => description; set => description = value; }
        public string Size { get => size; set => size = value; }
        public long Progress { get => progress; set => progress = value; }
        public DownloadStatus Status { get => status; set => status = value; }
    }
}
