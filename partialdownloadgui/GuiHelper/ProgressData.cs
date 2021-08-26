using System;
using System.Collections.ObjectModel;

namespace partialdownloadgui.Components
{
    public class ProgressData
    {
        private DownloadView downloadView;
        private ObservableCollection<SectionView> sectionViews;

        public DownloadView DownloadView { get => downloadView; set => downloadView = value; }
        public ObservableCollection<SectionView> SectionViews { get => sectionViews; set => sectionViews = value; }

        public ProgressData()
        {
            downloadView = new();
            sectionViews = new();
        }
    }
}
