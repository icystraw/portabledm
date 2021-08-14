using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace partialdownloadgui.Components
{
    public class TcpServer
    {
        private static readonly int listenPort = 13000;
        private static readonly int maxYoutubeVideoRecordsKept = 6;
        private static string downloadUrl;
        private static Queue<YoutubeVideo> youtubeVideos = new();
        private static Thread serverThread;

        public static string DownloadUrl { get => downloadUrl; set => downloadUrl = value; }
        public static Queue<YoutubeVideo> YoutubeVideos { get => youtubeVideos; }

        public static void Start()
        {
            if (serverThread != null && serverThread.IsAlive) return;
            serverThread = new(new ThreadStart(tcpServerThreadWorker));
            serverThread.Start();
        }

        public static void Stop()
        {
            if (serverThread != null && serverThread.IsAlive)
            {
                try
                {
                    HttpRequestMessage request = new(HttpMethod.Get, "http://localhost:13000/__SERVER_STOP");
                    HttpResponseMessage response = Downloader.Client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                    response.Dispose();
                    serverThread.Join();
                }
                catch { }
            }
        }

        public static void tcpServerThreadWorker()
        {
            TcpListener server = new(IPAddress.Parse("127.0.0.1"), listenPort);
            try
            {
                server.Start();
            }
            catch
            {
                return;
            }

            while (true)
            {
                TcpClient client = null;
                try
                {
                    // Perform a blocking call to accept requests.
                    // You could also use server.AcceptSocket() here.
                    client = server.AcceptTcpClient();

                    int i;
                    byte[] bytes = new byte[2048];
                    StringBuilder sb = new();
                    string request, response;
                    NetworkStream stream = client.GetStream();
                    stream.ReadTimeout = 500;
                    // Loop to receive all the data sent by the client.
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        // Translate data bytes to a ASCII string.
                        sb.Append(Encoding.ASCII.GetString(bytes, 0, i));
                        if (!stream.DataAvailable) break;
                    }
                    request = sb.ToString();
                    response = "HTTP/1.1 403 Forbidden\r\nDate: " + DateTime.Now.ToUniversalTime().ToString("R") + "\r\n\r\n";
                    byte[] msg = Encoding.ASCII.GetBytes(response);
                    stream.Write(msg, 0, msg.Length);
                    client.Close();
                    if (request.Contains("__SERVER_STOP"))
                    {
                        break;
                    }
                    else if (request.StartsWith("GET /") && request.Contains(" HTTP"))
                    {
                        string encodedUrl = request.Substring(5, request.IndexOf(" HTTP") - 5);
                        if (encodedUrl.Contains("/")) // it is a Youtube url
                        {
                            YoutubeVideo video = YoutubeVideo.ParseYoutubeEncodedUrlFromExtension(encodedUrl);
                            if (video != null)
                            {
                                bool foundSameUrl = false;
                                foreach (YoutubeVideo v in youtubeVideos)
                                {
                                    if (v.url == video.url)
                                    {
                                        v.title = video.title;
                                        foundSameUrl = true;
                                        break;
                                    }
                                }
                                if (!foundSameUrl)
                                {
                                    if (youtubeVideos.Count >= maxYoutubeVideoRecordsKept) youtubeVideos.Dequeue();
                                    youtubeVideos.Enqueue(video);
                                }
                            }
                        }
                        else
                        {
                            downloadUrl = Uri.UnescapeDataString(encodedUrl);
                        }
                    }
                }
                catch
                {
                    if (client != null) client.Close();
                    continue;
                }
            }
            server.Stop();
        }
    }
}
