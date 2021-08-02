﻿using System;
using System.ComponentModel;

namespace partialdownloadgui.Components
{
    public class DownloadView : INotifyPropertyChanged
    {
        private Guid id;
        private string fileName;
        private string size;
        private long progress;
        private string speed;
        private DownloadStatus status;
        private string error;

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

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
