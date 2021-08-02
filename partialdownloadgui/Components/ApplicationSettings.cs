namespace partialdownloadgui.Components
{
    public class ApplicationSettings
    {
        private string downloadFolder;
        private bool startTcpServer;

        public string DownloadFolder { get => downloadFolder; set => downloadFolder = value; }
        public bool StartTcpServer { get => startTcpServer; set => startTcpServer = value; }
    }
}
