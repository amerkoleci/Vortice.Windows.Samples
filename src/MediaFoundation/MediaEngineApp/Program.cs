// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using static Vortice.MediaFoundation.MediaFactory;

static class Program
{
    private static IMFMediaEngineEx? s_mediaEngineEx;
    private static readonly ManualResetEvent s_eventReadyToPlay = new(false);
    private static bool s_isMusicStopped;

    private static void OnPlaybackCallback(MediaEngineEvent playEvent, nuint param1, int param2)
    {
        Console.Write("PlayBack Event received: {0}", playEvent);
        switch (playEvent)
        {
            case MediaEngineEvent.CanPlay:
                s_eventReadyToPlay.Set();
                break;
            case MediaEngineEvent.TimeUpdate:
                Console.Write(" {0}", TimeSpan.FromSeconds(s_mediaEngineEx!.CurrentTime));
                break;
            case MediaEngineEvent.Error:
            case MediaEngineEvent.Abort:
            case MediaEngineEvent.Ended:
                s_isMusicStopped = true;
                break;
        }

        Console.WriteLine();
    }

    static void Main()
    {
        // Initialize MediaFoundation
        if (MFStartup(true).Failure)
        {
            return;
        }

        // Creates the MediaEngineClassFactory
        using IMFMediaEngineClassFactory mediaEngineFactory = new();

        using IMFAttributes attributes = MFCreateAttributes(1);
        attributes.AudioCategory = Vortice.Multimedia.AudioStreamCategory.GameMedia;

        // Creates MediaEngine for AudioOnly 
        using IMFMediaEngine mediaEngine = mediaEngineFactory.CreateInstance(
            MediaEngineCreateFlags.AudioOnly, attributes, OnPlaybackCallback);

        // Query for MediaEngineEx interface
        s_mediaEngineEx = mediaEngine.QueryInterface<IMFMediaEngineEx>();

        string fileName = Path.Combine(AppContext.BaseDirectory, "ergon.wav");
        using (MFByteStream mfStream = new(fileName))
        {
            // Set the source stream
            s_mediaEngineEx.SetSourceFromByteStream(mfStream, fileName);

            // Wait for MediaEngine to be ready
            if (!s_eventReadyToPlay.WaitOne(1000))
            {
                Console.WriteLine("Unexpected error: Unable to play this file");
            }

            // Play the music
            s_mediaEngineEx.Play();

            // Wait until music is stopped.
            while (!s_isMusicStopped)
            {
                Thread.Sleep(10);
            }
        }

        MFShutdown().CheckError();
    }
}
