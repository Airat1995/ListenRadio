using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Media;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.IO;
using Console = System.Console;

namespace ListenRadio
{
    [Service]
    [IntentFilter(new [] {ActionPlay, ActionPause, ActionStop })]
    class StreamService : Service, AudioManager.IOnAudioFocusChangeListener
    {
        public const string ActionPlay = "com.action.PLAY";
        public const string ActionPause = "com.action.PAUSE";
        public const string ActionStop = "com.action.STOP";

        private string _radio;

        private MediaPlayer _player;
        private AudioManager _audioManager;
        private WifiManager _wifiManager;
        private WifiManager.WifiLock _wifiLock;
        private NotificationManager _notificationManager;
        private bool _paused;

        private const int NotificationId = 1;

        public override void OnCreate()
        {
            base.OnCreate();
            _audioManager = (AudioManager)GetSystemService(AudioService);
            _wifiManager = (WifiManager)GetSystemService(WifiService);
            _radio = GetString(Resource.String.RadioUrl);
            _notificationManager = GetSystemService(NotificationService) as NotificationManager;
        }

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            switch (intent.Action)
            {
                case ActionPlay: Play(); break;
                case ActionStop: Stop(); break;
                case ActionPause: Pause(); break;
            }

            return StartCommandResult.Sticky;
        }

        private void IntializePlayer()
        {
            _player = new MediaPlayer();
            
            _player.SetWakeMode(ApplicationContext, WakeLockFlags.Partial);

            _player.Prepared += (sender, args) => _player.Start();

            _player.Completion += (sender, args) => Stop();

            _player.Error += (sender, args) =>
            {
                //playback error
                Console.WriteLine("Error in playback resetting: " + args.What);
                Stop();
            };
        }

        private async void Play()
        {
            if (_paused && _player != null)
            {
                _paused = false;
                _player.Start();
                StartForeground();
                return;
            }

            if (_player == null)
            {
                IntializePlayer();
            }

            if (_player.IsPlaying)
                return;

            try
            {
                await _player.SetDataSourceAsync(ApplicationContext, Android.Net.Uri.Parse(_radio));
                AudioFocusRequest audioFocusRequest;
                if (Build.VERSION.SdkInt > BuildVersionCodes.O)
                {
                    audioFocusRequest = _audioManager.RequestAudioFocus(new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                        .SetAudioAttributes(new AudioAttributes.Builder().SetLegacyStreamType(Stream.Music).Build())
                        .SetOnAudioFocusChangeListener(this)
                        .Build());
                }
                else
                {
                    audioFocusRequest = _audioManager.RequestAudioFocus(this, Stream.Music, AudioFocus.Gain);
                }
                if (audioFocusRequest != AudioFocusRequest.Granted)
                {
                    //could not get audio focus
                    Log.Error("Stream Service", "Could not get audio focus");
                    throw new StreamCorruptedException("Could not get audio focus");
                }

                _player.PrepareAsync();
                AquireWifiLock();
                StartForeground();
            }
            catch (Exception ex)
            {
                //unable to start playback log error
                Log.Error("Stream Service", "Unable to start playback: " + ex);
            }
        }

        private void StartForeground()
        {
            //Intent for showing notification
            var pendingIntent = PendingIntent.GetActivity(ApplicationContext, 0,
                            new Intent(ApplicationContext, typeof(MainActivity)),
                            PendingIntentFlags.UpdateCurrent);

            //Custom notification and build it
            var builder = new Notification.Builder(this)
            .SetContentText("Radio is playing")
            .SetContentTitle("Listen Radio")
            .SetContentIntent(pendingIntent)
            .SetSmallIcon(Resource.Drawable.Banner)
            .SetOngoing(true);
            Notification notification = builder.Build();

            //Init notification manager and show notification
            _notificationManager?.Notify(NotificationId, notification);

        }

        //Pause, can use it if you want
        private void Pause()
        {
            if (_player == null)
                return;
            if (_player.IsPlaying)
                _player.Pause();

            StopForeground(true);
            _paused = true;
        }

        //Stop
        private void Stop()
        {
            if (_player == null)
                return;

            if (_player.IsPlaying)
                _player.Stop();

            _player.Reset();
            _paused = false;
            StopForeground(true);
            ReleaseWifiLock();
            _notificationManager.Cancel(NotificationId);
        }

        //Wifi lockers, when device go to sleep still play streaming
        private void AquireWifiLock()
        {
            if (_wifiLock == null)
            {
                _wifiLock = _wifiManager.CreateWifiLock(WifiMode.Full, "xamarin_wifi_lock");
            }
            _wifiLock.Acquire();
        }

        private void ReleaseWifiLock()
        {
            if (_wifiLock == null)
                return;

            _wifiLock.Release();
            _wifiLock = null;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_player == null) return;
            _player.Release();
            _player = null;
        }


        /// <summary>
        /// Focus change, when user change application
        /// </summary>
        /// <param name="focusChange">Check app audio focus</param>
        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            switch (focusChange)
            {
                case AudioFocus.Gain:
                    if (_player == null)
                        IntializePlayer();
                    if (!_player.IsPlaying)
                    {
                        _player.Start();
                        _paused = false;
                    }
                    _player.SetVolume(1.0f, 1.0f);//Turn it up!
                    break;
                case AudioFocus.Loss:
                    //We have lost focus stop!
                    Stop();
                    break;
                case AudioFocus.LossTransient:
                    Pause();
                    break;
                case AudioFocus.LossTransientCanDuck:
                    if (_player.IsPlaying)
                        _player.SetVolume(.1f, .1f);//turn it down!
                    break;

            }
        }
    }
}