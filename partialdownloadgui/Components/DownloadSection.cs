using System;
using System.Net;
using System.Text.Json.Serialization;

namespace partialdownloadgui.Components
{
    public class DownloadSection
    {
        public DownloadSection Clone()
        {
            DownloadSection newSection = new();
            newSection.Url = this.url;
            newSection.Start = this.start;
            newSection.End = this.end;
            newSection.SuggestedName = this.suggestedName;
            newSection.DownloadStatus = DownloadStatus.Stopped;
            newSection.BytesDownloaded = 0;
            newSection.HttpStatusCode = 0;
            newSection.UserName = this.userName;
            newSection.Password = this.password;

            return newSection;
        }

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
            newSection.UserName = this.userName;
            newSection.Password = this.password;
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
            this.fileName = Util.appDataDirectory + this.id.ToString();
            this.bytesDownloaded = 0;
            this.error = string.Empty;
        }

        private Guid id;
        private string url;
        private long start;
        private long end;
        private string fileName;
        private string suggestedName;
        private DownloadStatus downloadStatus;
        private long bytesDownloaded;
        private HttpStatusCode httpStatusCode;
        private string userName;
        private string password;
        private DownloadSection nextSection;
        private Guid nextSectionId;
        private string error;

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
        public string SuggestedName { get => suggestedName; set => suggestedName = value; }
        public string UserName { get => userName; set => userName = value; }
        public string Password { get => password; set => password = value; }
        public string Error { get => error; set => error = value; }
    }
}
