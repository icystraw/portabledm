using System;
using System.Text;

namespace partialdownloadgui.Components
{
    public class YoutubeVideo
    {
        private int _itag;
        private string _url;
        private string _mimeType;
        private string _contentLength;
        private string _qualityLabel;
        private string _audioQuality;

        public int itag { get => _itag; set => _itag = value; }
        public string url { get => _url; set => _url = value; }
        public string mimeType { get => _mimeType; set => _mimeType = value; }
        public string contentLength { get => _contentLength; set => _contentLength = value; }
        public string qualityLabel { get => _qualityLabel; set => _qualityLabel = value; }
        public string audioQuality { get => _audioQuality; set => _audioQuality = value; }

        public string GetDescription()
        {
            StringBuilder sb = new();
            sb.Append("Type: ");
            sb.Append(_mimeType);
            sb.Append(", ");
            try
            {
                sb.Append(Util.getShortFileSize(Convert.ToInt64(_contentLength)));
            }
            catch
            {
                sb.Append(_contentLength);
            }
            if (!string.IsNullOrEmpty(_qualityLabel))
            {
                sb.Append(", ");
                sb.Append(_qualityLabel);
            }
            if (!string.IsNullOrEmpty(_audioQuality))
            {
                sb.Append(", ");
                sb.Append(_audioQuality);
            }
            return sb.ToString();
        }
    }
}
