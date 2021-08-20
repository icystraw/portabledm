using System;
using System.ComponentModel;

namespace partialdownloadgui.Components
{
    public class DownloadView : INotifyPropertyChanged
    {
        private Guid id;
        private string url;
        private string fileName;
        private string size;
        private string downloadFolder;
        private decimal progress;
        private string speed;
        private DownloadStatus status;
        private string error;
        private Guid downloadGroup;
        private object tag;
        private string eta;

        public string FileName
        {
            get => fileName;
            set
            {
                fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }
        public string Size
        {
            get => size;
            set
            {
                size = value;
                OnPropertyChanged(nameof(Size));
            }
        }
        public decimal Progress
        {
            get => progress;
            set
            {
                progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }
        public string Speed
        {
            get => speed;
            set
            {
                speed = value;
                OnPropertyChanged(nameof(Speed));
            }
        }
        public DownloadStatus Status
        {
            get => status;
            set
            {
                status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public Guid Id { get => id; set => id = value; }
        public string Error
        {
            get => error;
            set
            {
                error = value;
                OnPropertyChanged(nameof(Error));
            }
        }

        public string Url { get => url; set => url = value; }
        public string DownloadFolder { get => downloadFolder; set => downloadFolder = value; }
        public object Tag { get => tag; set => tag = value; }
        public Guid DownloadGroup { get => downloadGroup; set => downloadGroup = value; }
        public string Eta
        {
            get => eta;
            set
            {
                eta = value;
                OnPropertyChanged(nameof(Eta));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
