using System;
using System.Collections.Generic;

namespace partialdownloadgui.Components
{
    public class Download
    {
        private List<DownloadSection> sections;
        private DownloadSection summarySection;
        private string downloadFolder;
        private int noDownloader;
        private Guid downloadGroup;

        public List<DownloadSection> Sections { get => sections; set => sections = value; }
        public DownloadSection SummarySection { get => summarySection; set => summarySection = value; }
        public string DownloadFolder { get => downloadFolder; set => downloadFolder = value; }
        public int NoDownloader { get => noDownloader; set => noDownloader = value; }
        public Guid DownloadGroup { get => downloadGroup; set => downloadGroup = value; }

        public Download()
        {
            summarySection = new();
            sections = new();
            downloadGroup = Guid.Empty;
        }

        public void SetCredentials(string userName, string password)
        {
            this.summarySection.UserName = userName;
            this.summarySection.Password = password;
            foreach (DownloadSection ds in this.sections)
            {
                ds.UserName = userName;
                ds.Password = password;
            }
        }
    }
}
