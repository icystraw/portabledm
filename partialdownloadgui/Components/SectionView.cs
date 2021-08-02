using System.Net;

namespace partialdownloadgui.Components
{
    public class SectionView
    {
        private HttpStatusCode description;
        private string size;
        private long progress;
        private DownloadStatus status;
        private string error;

        public HttpStatusCode Description { get => description; set => description = value; }
        public string Size { get => size; set => size = value; }
        public long Progress { get => progress; set => progress = value; }
        public DownloadStatus Status { get => status; set => status = value; }
        public string Error { get => error; set => error = value; }
    }
}
