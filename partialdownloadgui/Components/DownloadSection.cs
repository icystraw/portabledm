using System;
using System.IO;
using System.Net;
using System.Text.Json.Serialization;

namespace partialdownloadgui.Components
{
    public class DownloadSection
    {
        public DownloadSection Split()
        {
            long _bytesDownloaded = this.bytesDownloaded;
            DownloadSection newSection = new();
            newSection.Url = this.url;
            newSection.Start = this.start + _bytesDownloaded + (this.end - (this.start + _bytesDownloaded)) / 2;
            newSection.End = this.end;
            if (newSection.Start > newSection.End) return null;
            newSection.DownloadStatus = DownloadStatus.Stopped;
            newSection.BytesDownloaded = 0;
            newSection.HttpStatusCode = 0;
            newSection.NextSection = this.nextSection;
            if (this.nextSection != null) newSection.NextSectionId = this.nextSection.Id;

            this.nextSection = newSection;
            this.nextSectionId = newSection.Id;
            this.end = newSection.Start - 1;

            return newSection;
        }

        public DownloadSection()
        {
            this.id = Guid.NewGuid();
            this.fileName = Path.GetTempPath() + this.id.ToString();
            this.bytesDownloaded = 0;
        }

        private Guid id;
        private string url;
        private long start;
        private long end;
        private string fileName;
        private DownloadStatus downloadStatus;
        private long bytesDownloaded;
        private HttpStatusCode httpStatusCode;
        private DownloadSection nextSection;
        private Guid nextSectionId;

        [JsonIgnore]
        public long Total
        {
            get
            {
                return end - start + 1;
            }
        }

        public Guid Id { get => id; set => id = value; }
        public long Start { get => start; set => start = value; }
        public long End { get => end; set => end = value; }
        public string FileName { get => fileName; set => fileName = value; }
        public DownloadStatus DownloadStatus { get => downloadStatus; set => downloadStatus = value; }
        [JsonIgnore]
        public DownloadSection NextSection { get => nextSection; set => nextSection = value; }
        public string Url { get => url; set => url = value; }
        public long BytesDownloaded { get => bytesDownloaded; set => bytesDownloaded = value; }
        public HttpStatusCode HttpStatusCode { get => httpStatusCode; set => httpStatusCode = value; }
        public Guid NextSectionId { get => nextSectionId; set => nextSectionId = value; }
    }
}
