﻿namespace partialdownloadgui.Components
{
    public enum DownloadStatus
    {
        Stopped = 0,
        PrepareToDownload = 1,
        Downloading = 2,
        Finished = 3,
        LogicalErrorOrCancelled = 4,
        DownloadError = 5
    }
}
