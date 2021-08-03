namespace partialdownloadgui.Components
{
    public class ApplicationSettings
    {
        private string downloadFolder;
        private bool startTcpServer;
        private double mainWindowWidth;
        private double mainWindowHeight;

        public string DownloadFolder { get => downloadFolder; set => downloadFolder = value; }
        public bool StartTcpServer { get => startTcpServer; set => startTcpServer = value; }
        public double MainWindowWidth { get => mainWindowWidth; set => mainWindowWidth = value; }
        public double MainWindowHeight { get => mainWindowHeight; set => mainWindowHeight = value; }
    }
}
