using Vortice.MediaFoundation;
using static Vortice.MediaFoundation.MediaFactory;

static class Program
{
    static void Main()
    {
        // Initialize MediaFoundation
        if (MFStartup().Failure)
        {
            return;
        }

        MFShutdown().CheckError();
    }
}
