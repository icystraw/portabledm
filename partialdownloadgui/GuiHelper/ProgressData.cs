using System;
using System.Collections.ObjectModel;

namespace partialdownloadgui.Components
{
    public class ProgressData
    {
        private Guid downloadId;
        private DownloadView downloadView;
        private ObservableCollection<SectionView> sectionViews;
        private string progressBar;

        public Guid DownloadId { get => downloadId; set => downloadId = value; }
        public DownloadView DownloadView { get => downloadView; set => downloadView = value; }
        public ObservableCollection<SectionView> SectionViews { get => sectionViews; set => sectionViews = value; }
        public string ProgressBar { get => progressBar; set => progressBar = value; }

        public ProgressData()
        {
            downloadView = new();
            sectionViews = new();
            progressBar = string.Empty;
        }
    }
}
