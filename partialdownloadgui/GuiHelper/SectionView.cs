﻿using System.Net;

namespace partialdownloadgui.Components
{
    public class SectionView
    {
        private HttpStatusCode httpStatusCode;
        private string size;
        private decimal progress;
        private DownloadStatus status;
        private string error;

        public HttpStatusCode HttpStatusCode { get => httpStatusCode; set => httpStatusCode = value; }
        public string Size { get => size; set => size = value; }
        public decimal Progress { get => progress; set => progress = value; }
        public DownloadStatus Status { get => status; set => status = value; }
        public string Error { get => error; set => error = value; }
    }
}