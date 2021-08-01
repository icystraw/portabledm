using System.ComponentModel;

namespace partialdownloadgui.Components
{
    public class DownloadView : INotifyPropertyChanged
    {
        private string fileName;
        private string size;
        private long progress;
        private string speed;
        private string status;

        public string FileName { get => fileName; set => fileName = value; }
        public string Size { get => size; set => size = value; }
        public long Progress
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
        public string Status
        {
            get => status;
            set
            {
                status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
