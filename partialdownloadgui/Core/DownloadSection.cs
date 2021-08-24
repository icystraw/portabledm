using System;
using System.Net;
using System.Text.Json.Serialization;

namespace partialdownloadgui.Components
{
    public class DownloadSection
    {
        public DownloadSection Copy()
        {
            DownloadSection newSection = new();
            newSection.Url = url;
            newSection.Start = start;
            newSection.End = end;
            newSection.SuggestedName = suggestedName;
            newSection.ContentType = contentType;
            newSection.DownloadStatus = DownloadStatus.Stopped;
            newSection.HttpStatusCode = 0;
            newSection.UserName = userName;
            newSection.Password = password;
            newSection.ParentFile = parentFile;
            newSection.LastModified = lastModified;

            return newSection;
        }

        public DownloadSection Split()
        {
            long _bytesDownloaded = bytesDownloaded;
            DownloadSection newSection = new();
            newSection.Url = url;
            newSection.Start = start + _bytesDownloaded + (end - (start + _bytesDownloaded)) / 2;
            newSection.End = end;
            if (newSection.Start > newSection.End) return null;
            newSection.DownloadStatus = DownloadStatus.Stopped;
            newSection.HttpStatusCode = 0;
            newSection.UserName = userName;
            newSection.Password = password;
            newSection.SuggestedName = suggestedName;
            newSection.ContentType = contentType;
            newSection.ParentFile = parentFile;
            newSection.LastModified = lastModified;
            // store a reference of this section in the new section, in order to add the
            // new section into the section chain in future should the HTTP request succeed.
            newSection.Tag = this;

            return newSection;
        }

        public DownloadSection()
        {
            id = Guid.NewGuid();
            fileName = Util.appDataDirectory + id.ToString();
            bytesDownloaded = 0;
            error = string.Empty;
            lastModified = DateTimeOffset.MaxValue;
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
        private string contentType;
        private string userName;
        private string password;
        private DownloadSection nextSection;
        private Guid nextSectionId;
        private string error;
        private object tag;
        private DateTime lastStatusChange;
        private string parentFile;
        private DateTimeOffset lastModified;

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
        public DownloadStatus DownloadStatus
        {
            get => downloadStatus;
            set
            {
                downloadStatus = value;
                lastStatusChange = DateTime.Now;
            }
        }
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
        public string ContentType { get => contentType; set => contentType = value; }
        [JsonIgnore]
        public object Tag { get => tag; set => tag = value; }
        public DateTime LastStatusChange { get => lastStatusChange; set => lastStatusChange = value; }
        public string ParentFile { get => parentFile; set => parentFile = value; }
        public DateTimeOffset LastModified { get => lastModified; set => lastModified = value; }
    }
}
