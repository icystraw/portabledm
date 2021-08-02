using System.Text.Json.Serialization;

namespace partialdownloadgui.Components
{
    public class ApplicationSettings
    {
        private string downloadFolder;

        public string DownloadFolder { get => downloadFolder; set => downloadFolder = value; }
    }
}
