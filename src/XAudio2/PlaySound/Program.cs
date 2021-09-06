using System;
using System.IO;
using System.Threading;
using Vortice;
using Vortice.Multimedia;
using Vortice.XAudio2;
using static Vortice.XAudio2.XAudio2;

static class Program
{
    static void Main(string[] args)
    {
        using (IXAudio2 xaudio2 = XAudio2Create())
        {
            using (IXAudio2MasteringVoice masteringVoice = xaudio2.CreateMasteringVoice())
            {
                PlaySoundFile(xaudio2, "1) Playing a standard WAV file", "ergon.wav");
                PlaySoundFile(xaudio2, "2) Playing a XWMA file", "ergon.xwma");
                PlaySoundFile(xaudio2, "3) Playing an ADPCM file", "ergon.adpcm.wav");
            }
        }
    }

    private static void PlaySoundFile(IXAudio2 device, string text, string fileName)
    {
        Console.WriteLine("{0} => {1} (Press esc to skip)", text, fileName);

        using (SoundStream stream = new SoundStream(File.OpenRead(fileName)))
        {
            DataStream dataStream = stream.ToDataStream();
            WaveFormat waveFormat = stream.Format!;
            AudioBuffer buffer = new AudioBuffer(dataStream);

            using (IXAudio2SourceVoice sourceVoice = device.CreateSourceVoice(waveFormat, true))
            {
                // Adds a sample callback to check that they are working on source voices
                sourceVoice.BufferEnd += (context) => Console.WriteLine(" => event received: end of buffer");
                sourceVoice.SubmitSourceBuffer(buffer, stream.DecodedPacketsInfo);
                sourceVoice.Start();

                int count = 0;
                while (sourceVoice.State.BuffersQueued > 0 && !IsKeyPressed(ConsoleKey.Escape))
                {
                    if (count == 50)
                    {
                        Console.Write(".");
                        Console.Out.Flush();
                        count = 0;
                    }
                    Thread.Sleep(10);
                    count++;
                }
                Console.WriteLine();

                sourceVoice.DestroyVoice();
            }
            dataStream.Dispose();
        }
    }

    private static bool IsKeyPressed(ConsoleKey key)
    {
        return Console.KeyAvailable && Console.ReadKey(true).Key == key;
    }
}
