// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Framework;
using static Vortice.MediaFoundation.MediaFactory;

static class Program
{
    class VideoApp : D3D11Application
    {
        private readonly IMFDXGIDeviceManager _dxgiDeviceManager;
        private readonly IMFMediaEngine _mediaEngine;
        private readonly MFByteStream _mfStream;
        private readonly ManualResetEvent _eventReadyToPlay = new(false);
        private readonly IDXGISurface _colorTextureSurface;
        private readonly Size _videoSize;

        public VideoApp(string videoFile)
            : base(DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport)
        {
            // Add multi thread protection on device
            using (ID3D11Multithread multithread = Device.QueryInterface<ID3D11Multithread>())
            {
                multithread.SetMultithreadProtected(true);
            }

            _dxgiDeviceManager = MFCreateDXGIDeviceManager();
            _dxgiDeviceManager.ResetDevice(Device).CheckError();

            // Creates the MediaEngineClassFactory
            using IMFMediaEngineClassFactory mediaEngineFactory = new();

            using IMFAttributes attributes = MFCreateAttributes(1);
            attributes.VideoOutputFormat = Vortice.DXGI.Format.B8G8R8A8_UNorm;
            attributes.DxgiManager = _dxgiDeviceManager;

            // Creates MediaEngine for AudioOnly 
            _mediaEngine = mediaEngineFactory.CreateInstance(MediaEngineCreateFlags.None, attributes, OnPlaybackCallback);

            // Query for MediaEngineEx interface
            using IMFMediaEngineEx mediaEngineEx = _mediaEngine.QueryInterface<IMFMediaEngineEx>();

            // Create a ByteStream object from it
            _mfStream = new(videoFile);

            // Set the source stream
            mediaEngineEx.SetSourceFromByteStream(_mfStream, videoFile);

            // Wait for MediaEngine to be ready
            if (!_eventReadyToPlay.WaitOne(1000))
            {
                Console.WriteLine("Unexpected error: Unable to play this file");
            }

            // Get DXGI surface to be used by our media engine
            _colorTextureSurface = ColorTexture.QueryInterface<IDXGISurface>();

            //Get our video size
            _videoSize = _mediaEngine.NativeVideoSize;

            // Play the video
            mediaEngineEx.Play();
        }

        protected override void Dispose(bool dispose)
        {
            _colorTextureSurface.Dispose();
            _dxgiDeviceManager.Dispose();
            _mediaEngine.Shutdown().CheckError();
            _mfStream.Dispose();
            _mediaEngine.Dispose();

            base.Dispose(dispose);
        }

        protected override void OnRender()
        {
            // Transfer frame if a new one is available
            if (_mediaEngine.OnVideoStreamTick(out long presentationTime))
            {
                _mediaEngine.TransferVideoFrame(
                    _colorTextureSurface, null,
                    new Vortice.RawRect(0, 0, _videoSize.Width, _videoSize.Height),
                    null);
            }
        }

        private void OnPlaybackCallback(MediaEngineEvent playEvent, nuint param1, int param2)
        {
            switch (playEvent)
            {
                case MediaEngineEvent.CanPlay:
                    _eventReadyToPlay.Set();
                    break;
                case MediaEngineEvent.TimeUpdate:
                    break;
                case MediaEngineEvent.Error:
                case MediaEngineEvent.Abort:
                case MediaEngineEvent.Ended:
                    //isMusicStopped = true;
                    break;
            }
        }
    }

    [STAThread]
    static void Main()
    {
        // Select a File to play
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Title = "Select a file",
            Filter = "Media Files(*.WMV;*.MP4;*.AVI)|*.WMV;*.MP4;*.AVI"
        };

        DialogResult result = openFileDialog.ShowDialog();
        if (result == DialogResult.Cancel)
        {
            return;
        }

        // Initialize MediaFoundation
        if (MFStartup().Failure)
        {
            return;
        }

        using VideoApp app = new(openFileDialog.FileName);
        app.Run();

        MFShutdown().CheckError();
    }
}
