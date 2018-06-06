using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using System.Net;
using Android.Util;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ListenRadio
{
    [Activity(Label = "ListenRadio", MainLauncher = true, Icon = "@drawable/Banner", 
        ConfigurationChanges = Android.Content.PM.ConfigChanges.KeyboardHidden | Android.Content.PM.ConfigChanges.Keyboard | Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    public class MainActivity : Activity
    {
        /// <summary>
        /// Used for get info from Source ancestor value, if with 0 will be null, then icrease them.
        /// </summary>
        private int _sourcepath = 0;

        private TextView titleTextView;
        private TextView infoTextView;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            Button playButton = FindViewById<Button>(Resource.Id.PlayButton);
            Button stopButton = FindViewById<Button>(Resource.Id.StopButton);

            playButton.Click += delegate { SendAudioCommand(StreamService.ActionPlay); };
            stopButton.Click += delegate { SendAudioCommand(StreamService.ActionStop); };

            titleTextView = FindViewById<TextView>(Resource.Id.TitleTextView);
            infoTextView = FindViewById<TextView>(Resource.Id.InfoTextView);
            MakeRequest();
            //TODO: make service delete this!!
            System.Timers.Timer t = new System.Timers.Timer {Interval = 5000};
            t.Elapsed += UpdateSourceData;
            t.Start();

        }

        private void SendAudioCommand(string action)
        {
            var intent = new Intent(action);
            intent.PutExtra("id", Resource.String.RadioUrl);
            StartService(intent);
        }

        //Get request from server
        protected void UpdateSourceData(object sender, System.Timers.ElapsedEventArgs e)
        {
            MakeRequest();
            //To decrease threads. Should work.
            Task.Delay(10000).Wait();
        }

        public void MakeRequest()
        {
            WebClient client = new WebClient();
            string downloadedString = client.DownloadString(@"http://stream.sunlife-fm.org:8000/status-json.xsl");
            var jsonObject = JObject.Parse(downloadedString);
            var sourcedata = jsonObject.Root.SelectToken($"icestats.source[{_sourcepath}]");

            //Check if first source data is empty, then its not work. Then mean it should get data from second source ancestor
            if (sourcedata.SelectToken("title") == null && _sourcepath == 0)
            {
                _sourcepath = 1;
                downloadedString = client.DownloadString(@"http://stream.sunlife-fm.org:8000/status-json.xsl");
                jsonObject = JObject.Parse(downloadedString);
                sourcedata = jsonObject.Root.SelectToken($"icestats.source[{_sourcepath}]");
            }
            RunOnUiThread(() => {
                if (_sourcepath == 0)
                    FillFirstTypeInfo(sourcedata.SelectToken("bitrate").ToString(), sourcedata.SelectToken("channels").ToString(), sourcedata.SelectToken("samplerate").ToString());
                else
                    FillSecondTypeInfo(sourcedata.SelectToken("audio_info").ToString());
                titleTextView.SetText(sourcedata.SelectToken("title").ToString(), TextView.BufferType.Spannable);
            });
            Log.Debug("userdata", sourcedata.ToString());
        }
        
        public void FillFirstTypeInfo(string bitrate, string channels, string samplerate)
        {
            string info = $"bitrate={bitrate} channels={channels} samplerate={samplerate}";
            infoTextView.SetText(info, TextView.BufferType.Spannable);
        }
        
        public void FillSecondTypeInfo(string audio_info)
        {
            string info = audio_info.ToString().Replace(';', ' ');
            infoTextView.SetText(info, TextView.BufferType.Spannable);
        } 

        protected override void OnDestroy()
        {
            var intent = new Intent(StreamService.ActionStop);
            StartService(intent);
            base.OnDestroy();
        }
    }
}

