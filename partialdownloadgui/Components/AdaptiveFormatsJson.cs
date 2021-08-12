﻿using System;
using System.Collections.Specialized;
using System.Web;

namespace partialdownloadgui.Components
{
    public class AdaptiveFormatsJson
    {
        private int _itag;
        private string _url;
        private string _signatureCipher;
        private string _mimeType;
        private string _contentLength;
        private string _qualityLabel;
        private string _audioQuality;
        private string _paramS;
        private string _paramSp;
        private string _signature;

        public int itag { get => _itag; set => _itag = value; }
        public string url { get => _url; set => _url = value; }
        public string mimeType { get => _mimeType; set => _mimeType = value; }
        public string contentLength { get => _contentLength; set => _contentLength = value; }
        public string qualityLabel { get => _qualityLabel; set => _qualityLabel = value; }
        public string audioQuality { get => _audioQuality; set => _audioQuality = value; }
        public string signatureCipher
        {
            get => _signatureCipher;
            set
            {
                _signatureCipher = value;
                NameValueCollection parameters = HttpUtility.ParseQueryString(value);
                this._url = parameters.Get("url");
                this._paramS = parameters.Get("s");
                this._paramSp = parameters.Get("sp");
            }
        }
        public string paramS { get => _paramS; set => _paramS = value; }
        public string paramSp { get => _paramSp; set => _paramSp = value; }
        public string signature
        {
            get => _signature;
            set
            {
                _signature = value;
                CombineSignatureWithUrl();
            }
        }

        private void CombineSignatureWithUrl()
        {
            this.url += "&" + this._paramSp + "=" + Uri.EscapeDataString(this._signature);
        }
    }
}