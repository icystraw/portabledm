using System.Text.Json.Serialization;

namespace partialdownloadgui.Components
{
    public class ApplicationSettings
    {
        private string downloadFolder;
        private bool shutDownAfterFinished;

        public string DownloadFolder { get => downloadFolder; set => downloadFolder = value; }
        [JsonIgnore]
        public bool ShutDownAfterFinished { get => shutDownAfterFinished; set => shutDownAfterFinished = value; }
    }
}
