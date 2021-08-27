using System.Net;

namespace partialdownloadgui.Components
{
    public class SectionView
    {
        private HttpStatusCode httpStatusCode;
        private decimal progress;
        private DownloadStatus status;
        private string error;
        private long bytesDownloaded;
        private long total;

        public HttpStatusCode HttpStatusCode { get => httpStatusCode; set => httpStatusCode = value; }
        public decimal Progress { get => progress; set => progress = value; }
        public DownloadStatus Status { get => status; set => status = value; }
        public string Error { get => error; set => error = value; }
        public long BytesDownloaded { get => bytesDownloaded; set => bytesDownloaded = value; }
        public long Total { get => total; set => total = value; }
    }
}
