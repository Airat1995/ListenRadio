using System;
using System.Net;

namespace ListenRadio
{
    class StreamInfo
    {
        /// <summary>
        /// Used for get stream info
        /// </summary>
        private WebClient _client;

        /// <summary>
        /// Stream info if requset complete without errors
        /// </summary>
        private string _data;
        public string GetData()
        {
            return _data;
        }

        /// <summary>
        /// Used when server return error to user
        /// </summary>
        private string _errorString; 
        public string GetError()
        {
            return _errorString;
        }

        public StreamInfo(string url)
        {
            _client = new WebClient();
            _client.DownloadStringAsync(new Uri(url));
            _client.DownloadStringCompleted += StreamDataGet;
        }

        private void StreamDataGet(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error == null)
                _data = e.Result;
            else
            {
                _data = null;
                _errorString = e.Error.Message;
            }
        }
    }
}